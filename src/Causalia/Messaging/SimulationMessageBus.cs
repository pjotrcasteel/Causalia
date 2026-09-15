using Causalia.Faults;
using Causalia.Messaging.Exceptions;
using Causalia.Messaging.Faults;
using Causalia.Messaging.Internal;
using Causalia.Nodes;

namespace Causalia.Messaging;

/// <summary>
/// Provides deterministic in-memory point-to-point message delivery inside a simulation.
/// </summary>
public sealed class SimulationMessageBus
{
    private readonly SimulationContext _context;
    private readonly Dictionary<MessageHandlerKey, MessageHandlerRegistration> _handlers = new();
    private readonly Dictionary<string, MessageEndpointState> _endpoints = new(StringComparer.Ordinal);
    private readonly Dictionary<string, SimulationNode?> _endpointOwners = new(StringComparer.Ordinal);
    private readonly Dictionary<long, MessageDeliveryAttemptState> _deliveryAttempts = new();
    private readonly FaultInjector<MessageFaultContext, MessageFault>? _faultInjector;
    private readonly MessageBusOptions _options;
    private long _nextMessageId;

    internal int DeliveryAttemptStateCount => _deliveryAttempts.Count;

    internal SimulationMessageBus(SimulationContext context, MessageBusOptions options)
    {
        _context = context;
        _options = options;

        if (options.DeliveryLatency < TimeSpan.Zero)
        {
            throw new ArgumentOutOfRangeException(nameof(options), options.DeliveryLatency, "DeliveryLatency cannot be negative.");
        }

        if (options.Faults is not null)
        {
            _faultInjector = context.CreateFaultInjector(options.FaultScope, options.Faults.Plan);
        }
    }

    /// <summary>
    /// Registers one handler for a message type on a logical endpoint.
    /// </summary>
    public void RegisterHandler<TMessage>(string endpoint, Func<TMessage, MessageDeliveryContext, CancellationToken, Task> handler)
    {
        RegisterHandler(endpoint, null, handler);
    }

    /// <summary>
    /// Registers one handler on the endpoint named after the supplied simulated node.
    /// Deliveries are suspended while the node is crashed and interrupted deliveries are retried after restart.
    /// </summary>
    public void RegisterNodeHandler<TMessage>(
        SimulationNode node,
        Func<TMessage, MessageDeliveryContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(node);
        RegisterHandler(node.Name, node, handler);
    }

    /// <summary>
    /// Registers one handler on a logical endpoint owned by the supplied simulated node.
    /// Deliveries are suspended while the node is crashed and interrupted deliveries are retried after restart.
    /// </summary>
    public void RegisterNodeHandler<TMessage>(
        SimulationNode node,
        string endpoint,
        Func<TMessage, MessageDeliveryContext, CancellationToken, Task> handler)
    {
        ArgumentNullException.ThrowIfNull(node);
        RegisterHandler(endpoint, node, handler);
    }

    /// <summary>
    /// Enqueues a message for deterministic delivery and completes when the simulated bus accepts it.
    /// </summary>
    public Task SendAsync<TMessage>(string endpoint, TMessage message, CancellationToken cancellationToken)
    {
        EnqueueLogicalMessage(endpoint, message, false, cancellationToken);
        return Task.CompletedTask;
    }

    /// <summary>
    /// Enqueues a message and completes after every generated delivery has finished processing.
    /// A deterministically dropped message has no generated deliveries and therefore completes immediately.
    /// </summary>
    public Task SendAndWaitAsync<TMessage>(string endpoint, TMessage message, CancellationToken cancellationToken)
    {
        var completions = EnqueueLogicalMessage(endpoint, message, true, cancellationToken);
        return completions.Count == 0 ? Task.CompletedTask : Task.WhenAll(completions);
    }

    private void RegisterHandler<TMessage>(
        string endpoint,
        SimulationNode? node,
        Func<TMessage, MessageDeliveryContext, CancellationToken, Task> handler)
    {
        ValidateEndpoint(endpoint);
        ArgumentNullException.ThrowIfNull(handler);
        ValidateNode(node);
        ValidateEndpointOwner(endpoint, node);

        var messageType = typeof(TMessage);
        var key = new MessageHandlerKey(endpoint, messageType);

        if (_handlers.ContainsKey(key))
        {
            throw new DuplicateMessageHandlerException(endpoint, messageType);
        }

        _handlers.Add(
            key,
            new MessageHandlerRegistration
            {
                Handler = (message, deliveryContext, cancellationToken) =>
                    handler((TMessage)message, deliveryContext, cancellationToken),
                Node = node
            });
    }

    private IReadOnlyList<Task> EnqueueLogicalMessage<TMessage>(
        string endpoint,
        TMessage message,
        bool waitForCompletion,
        CancellationToken cancellationToken)
    {
        ValidateEndpoint(endpoint);
        ArgumentNullException.ThrowIfNull(message);
        cancellationToken.ThrowIfCancellationRequested();

        var messageType = typeof(TMessage);
        var key = new MessageHandlerKey(endpoint, messageType);

        if (!_handlers.TryGetValue(key, out var registration))
        {
            throw new MessageHandlerNotFoundException(endpoint, messageType);
        }

        var logicalMessage = new LogicalMessage
        {
            MessageId = checked(++_nextMessageId),
            Endpoint = endpoint,
            Message = message!,
            MessageType = messageType,
            Registration = registration,
            EnqueuedAt = _context.TimeProvider.GetUtcNow(),
            WaitForCompletion = waitForCompletion,
            CancellationToken = cancellationToken
        };
        var effects = EvaluateFaults(logicalMessage.MessageId, endpoint, messageType, logicalMessage.EnqueuedAt);
        var outcome = AggregateFaults(effects);
        TraceFaults(logicalMessage.MessageId, endpoint, effects);

        if (outcome.Drop)
        {
            _context.TraceEvent(
                $"messaging:dropped:{logicalMessage.MessageId}:{endpoint}:{GetMessageTypeName(messageType)}");
            return Array.Empty<Task>();
        }

        var endpointState = GetOrCreateEndpoint(endpoint);
        var deliveryCount = checked(1 + outcome.AdditionalCopies);
        var queuedMessages = new List<QueuedMessage>(deliveryCount);
        var completions = new List<Task>(deliveryCount);

        for (var attempt = 1; attempt <= deliveryCount; attempt++)
        {
            var queuedMessage = CreateQueuedMessage(logicalMessage, attempt, outcome.AdditionalDelay);
            queuedMessages.Add(queuedMessage);

            if (queuedMessage.Completion is not null)
            {
                completions.Add(queuedMessage.Completion.Task);
            }
        }

        _deliveryAttempts.Add(
            logicalMessage.MessageId,
            new MessageDeliveryAttemptState
            {
                NextAttempt = checked(deliveryCount + 1),
                RemainingDeliveries = deliveryCount
            });
        EnqueueDeliveries(endpointState, queuedMessages, outcome.Reorder);

        foreach (var queuedMessage in queuedMessages)
        {
            Trace("enqueued", queuedMessage);
        }

        EnsureEndpointProcessing(endpointState);
        return completions.AsReadOnly();
    }

    private IReadOnlyList<MessageFault> EvaluateFaults(long messageId, string endpoint, Type messageType, DateTimeOffset enqueuedAt)
    {
        if (_faultInjector is null)
        {
            return Array.Empty<MessageFault>();
        }

        return _faultInjector.Evaluate(new MessageFaultContext(messageId, endpoint, messageType, enqueuedAt));
    }

    private QueuedMessage CreateQueuedMessage(LogicalMessage logicalMessage, int attempt, TimeSpan additionalDelay)
    {
        return new QueuedMessage
        {
            MessageId = logicalMessage.MessageId,
            Attempt = attempt,
            Endpoint = logicalMessage.Endpoint,
            Message = logicalMessage.Message,
            MessageType = logicalMessage.MessageType,
            EnqueuedAt = logicalMessage.EnqueuedAt,
            DueAt = logicalMessage.EnqueuedAt + _options.DeliveryLatency + additionalDelay,
            Registration = logicalMessage.Registration,
            CancellationToken = logicalMessage.CancellationToken,
            Completion = logicalMessage.WaitForCompletion ? new TaskCompletionSource<bool>() : null
        };
    }

    private static MessageFaultOutcome AggregateFaults(IReadOnlyList<MessageFault> effects)
    {
        var drop = false;
        var reorder = false;
        var additionalCopies = 0;
        var additionalDelay = TimeSpan.Zero;

        foreach (var effect in effects)
        {
            switch (effect)
            {
                case DropMessageFault:
                    drop = true;
                    break;
                case ReorderMessageFault:
                    reorder = true;
                    break;
                case DuplicateMessageFault duplicate:
                    additionalCopies = checked(additionalCopies + duplicate.AdditionalCopies);
                    break;
                case DelayMessageFault delay:
                    additionalDelay += delay.Delay;
                    break;
            }
        }

        return new MessageFaultOutcome(drop, reorder, additionalCopies, additionalDelay);
    }

    private static void EnqueueDeliveries(MessageEndpointState endpointState, IReadOnlyList<QueuedMessage> queuedMessages, bool reorder)
    {
        if (!reorder)
        {
            foreach (var queuedMessage in queuedMessages)
            {
                endpointState.Pending.AddLast(queuedMessage);
            }

            return;
        }

        for (var index = queuedMessages.Count - 1; index >= 0; index--)
        {
            endpointState.Pending.AddFirst(queuedMessages[index]);
        }
    }

    private void EnsureEndpointProcessing(MessageEndpointState endpointState)
    {
        if (endpointState.IsProcessing)
        {
            return;
        }

        endpointState.IsProcessing = true;
        var processingTask = ProcessEndpointAsync(endpointState);
        _context.TrackOperation(processingTask);
    }

    private MessageEndpointState GetOrCreateEndpoint(string endpoint)
    {
        if (_endpoints.TryGetValue(endpoint, out var state))
        {
            return state;
        }

        state = new MessageEndpointState();
        _endpoints.Add(endpoint, state);
        return state;
    }

    private async Task ProcessEndpointAsync(MessageEndpointState endpointState)
    {
        try
        {
            await Task.Yield();

            while (endpointState.Pending.First is { } node)
            {
                endpointState.Pending.RemoveFirst();
                await ProcessMessageAsync(node.Value);
            }
        }
        finally
        {
            endpointState.IsProcessing = false;
        }
    }

    private async Task ProcessMessageAsync(QueuedMessage queuedMessage)
    {
        try
        {
            await WaitUntilDueAsync(queuedMessage);

            while (true)
            {
                queuedMessage.CancellationToken.ThrowIfCancellationRequested();
                var node = queuedMessage.Registration.Node;

                if (node is not null)
                {
                    await node.WaitUntilRunningAsync(queuedMessage.CancellationToken);
                }

                var generation = node?.Generation ?? 0;
                using var executionCancellation = node?.CreateExecutionCancellationSource(queuedMessage.CancellationToken);
                var handlerCancellationToken = executionCancellation?.Token ?? queuedMessage.CancellationToken;
                var deliveredAt = _context.TimeProvider.GetUtcNow();
                var deliveryContext = new MessageDeliveryContext(
                    queuedMessage.MessageId,
                    queuedMessage.Endpoint,
                    queuedMessage.Attempt,
                    queuedMessage.EnqueuedAt,
                    deliveredAt);
                Trace("delivered", queuedMessage);

                try
                {
                    await queuedMessage.Registration.Handler(queuedMessage.Message, deliveryContext, handlerCancellationToken);
                }
                catch (OperationCanceledException) when (handlerCancellationToken.IsCancellationRequested
                    && WasInterruptedByNode(queuedMessage, node, generation))
                {
                    Trace("interrupted", queuedMessage);
                    queuedMessage.Attempt = GetNextDeliveryAttempt(queuedMessage.MessageId);
                    continue;
                }

                Trace("completed", queuedMessage);
                ReleaseDeliveryAttemptState(queuedMessage.MessageId);
                queuedMessage.Completion?.TrySetResult(true);
                return;
            }
        }
        catch (OperationCanceledException) when (queuedMessage.CancellationToken.IsCancellationRequested)
        {
            Trace("cancelled", queuedMessage);
            ReleaseDeliveryAttemptState(queuedMessage.MessageId);
            queuedMessage.Completion?.TrySetCanceled(queuedMessage.CancellationToken);
        }
        catch (Exception exception)
        {
            Trace("failed", queuedMessage);
            ReleaseDeliveryAttemptState(queuedMessage.MessageId);
            queuedMessage.Completion?.TrySetException(exception);
            throw;
        }
    }

    private async Task WaitUntilDueAsync(QueuedMessage queuedMessage)
    {
        var delay = queuedMessage.DueAt - _context.TimeProvider.GetUtcNow();

        if (delay > TimeSpan.Zero)
        {
            await Task.Delay(delay, _context.TimeProvider, queuedMessage.CancellationToken);
        }
    }

    private static bool WasInterruptedByNode(QueuedMessage queuedMessage, SimulationNode? node, int generation)
    {
        return node is not null
            && !queuedMessage.CancellationToken.IsCancellationRequested
            && (!node.IsRunning || node.Generation != generation);
    }

    private int GetNextDeliveryAttempt(long messageId)
    {
        var state = _deliveryAttempts[messageId];
        var attempt = state.NextAttempt;
        state.NextAttempt = checked(attempt + 1);
        return attempt;
    }

    private void ReleaseDeliveryAttemptState(long messageId)
    {
        var state = _deliveryAttempts[messageId];
        state.RemainingDeliveries--;

        if (state.RemainingDeliveries == 0)
        {
            _deliveryAttempts.Remove(messageId);
        }
    }

    private void ValidateNode(SimulationNode? node)
    {
        if (node is not null && !node.BelongsTo(_context))
        {
            throw new InvalidOperationException("The simulated node belongs to a different simulation context.");
        }
    }

    private void ValidateEndpointOwner(string endpoint, SimulationNode? node)
    {
        if (!_endpointOwners.TryGetValue(endpoint, out var existingNode))
        {
            _endpointOwners.Add(endpoint, node);
            return;
        }

        if (!ReferenceEquals(existingNode, node))
        {
            throw new InvalidOperationException(
                $"Endpoint '{endpoint}' cannot be registered to multiple simulated node owners.");
        }
    }

    private void TraceFaults(long messageId, string endpoint, IReadOnlyList<MessageFault> effects)
    {
        foreach (var effect in effects)
        {
            var detail = effect switch
            {
                DropMessageFault => "drop",
                ReorderMessageFault => "reorder",
                DuplicateMessageFault duplicate => $"duplicate:{duplicate.AdditionalCopies}",
                DelayMessageFault delay => $"delay:{delay.Delay.Ticks}",
                _ => effect.GetType().Name
            };
            _context.TraceEvent($"fault:messaging:{detail}:{messageId}:{endpoint}");
        }
    }

    private void Trace(string action, QueuedMessage queuedMessage)
    {
        _context.TraceEvent(
            $"messaging:{action}:{queuedMessage.MessageId}:{queuedMessage.Attempt}:{queuedMessage.Endpoint}:{GetMessageTypeName(queuedMessage.MessageType)}");
    }

    private static string GetMessageTypeName(Type messageType)
    {
        return messageType.FullName ?? messageType.Name;
    }

    private static void ValidateEndpoint(string endpoint)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(endpoint);
    }
}
