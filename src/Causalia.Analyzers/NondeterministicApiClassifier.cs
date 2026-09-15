using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Causalia.Analyzers;

internal static class NondeterministicApiClassifier
{
    public static bool TryClassifyInvocation(
        IInvocationOperation invocation,
        out DiagnosticDescriptor descriptor,
        out string guidance)
    {
        var method = invocation.TargetMethod;
        var containingType = method.ContainingType.ToDisplayString();

        if (IsConfigureAwaitFalse(invocation))
        {
            descriptor = DiagnosticDescriptors.EscapedAwait;
            guidance = "remove ConfigureAwait(false) so the continuation remains under deterministic scheduling.";
            return true;
        }

        if (IsTaskDelayWithoutTimeProvider(method))
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use Task.Delay(..., context.TimeProvider, context.CancellationToken).";
            return true;
        }

        if (IsMethod(containingType, method.Name, "System.Threading.Thread", "Sleep") ||
            containingType == "System.Diagnostics.Stopwatch" && method.Name is "GetTimestamp" or "Start" or "StartNew" or "Restart" ||
            IsMethod(containingType, method.Name, "System.Threading.CancellationTokenSource", "CancelAfter"))
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use SimulationContext.TimeProvider and virtual-time-aware APIs.";
            return true;
        }

        if (IsMethod(containingType, method.Name, "System.Guid", "NewGuid") ||
            containingType == "System.Security.Cryptography.RandomNumberGenerator")
        {
            descriptor = DiagnosticDescriptors.Randomness;
            guidance = "Use SimulationContext.Random; use Random.NextGuid() when a deterministic identifier is required.";
            return true;
        }

        if (IsUncontrolledConcurrencyMethod(containingType, method.Name))
        {
            descriptor = DiagnosticDescriptors.Concurrency;
            guidance = "Use async operations that remain on Causalia's synchronization context and scheduler.";
            return true;
        }

        if (IsBlockingWaitMethod(containingType, method.Name))
        {
            descriptor = DiagnosticDescriptors.BlockingWait;
            guidance = "Await asynchronous work instead of synchronously blocking the simulation thread.";
            return true;
        }

        descriptor = null!;
        guidance = string.Empty;
        return false;
    }

    public static bool TryClassifyProperty(
        IPropertyReferenceOperation propertyReference,
        out DiagnosticDescriptor descriptor,
        out string guidance)
    {
        var property = propertyReference.Property;
        var containingType = property.ContainingType.ToDisplayString();

        if (IsWallClockProperty(containingType, property.Name))
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use context.TimeProvider.GetUtcNow() or another API based on SimulationContext.TimeProvider.";
            return true;
        }

        if (containingType == "System.Random" && property.Name == "Shared")
        {
            descriptor = DiagnosticDescriptors.Randomness;
            guidance = "Use SimulationContext.Random so the value is replayable and isolated from scheduler randomness.";
            return true;
        }

        if (containingType == "System.Diagnostics.Stopwatch" && property.Name is "Elapsed" or "ElapsedMilliseconds" or "ElapsedTicks")
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use SimulationContext.TimeProvider for deterministic elapsed-time measurements.";
            return true;
        }

        if (containingType == "System.Threading.Thread" && property.Name == "CurrentThread" ||
            containingType == "System.Threading.ThreadPool" ||
            containingType == "System.Environment" && property.Name == "CurrentManagedThreadId")
        {
            descriptor = DiagnosticDescriptors.Concurrency;
            guidance = "Do not observe or depend on host thread identity or thread-pool state inside deterministic simulation code.";
            return true;
        }

        if (containingType.StartsWith("System.Threading.Tasks.Task<", StringComparison.Ordinal) && property.Name == "Result")
        {
            descriptor = DiagnosticDescriptors.BlockingWait;
            guidance = "Await the task instead of reading Task.Result inside deterministic simulation code.";
            return true;
        }

        descriptor = null!;
        guidance = string.Empty;
        return false;
    }

    public static bool TryClassifyObjectCreation(
        IObjectCreationOperation creation,
        out DiagnosticDescriptor descriptor,
        out string guidance)
    {
        var type = creation.Type?.ToDisplayString() ?? string.Empty;

        if (type == "System.Random")
        {
            descriptor = DiagnosticDescriptors.Randomness;
            guidance = "Use SimulationContext.Random; System.Random sequences are outside Causalia's replay contract.";
            return true;
        }

        if (InheritsFrom(creation.Type, "System.Security.Cryptography.RandomNumberGenerator"))
        {
            descriptor = DiagnosticDescriptors.Randomness;
            guidance = "Use SimulationContext.Random for deterministic pseudo-random data inside the simulation boundary.";
            return true;
        }

        if (type == "System.Threading.Thread")
        {
            descriptor = DiagnosticDescriptors.Concurrency;
            guidance = "Keep work on Causalia's cooperative scheduler instead of creating a raw thread.";
            return true;
        }

        if (IsRealTimerCreation(creation))
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use TimeProvider-aware timers based on SimulationContext.TimeProvider.";
            return true;
        }

        if (IsTimedCancellationTokenSource(creation))
        {
            descriptor = DiagnosticDescriptors.VirtualTime;
            guidance = "Use virtual-time-aware cancellation built from SimulationContext.TimeProvider.";
            return true;
        }

        descriptor = null!;
        guidance = string.Empty;
        return false;
    }

    private static bool IsTaskDelayWithoutTimeProvider(IMethodSymbol method)
    {
        if (method.ContainingType.ToDisplayString() != "System.Threading.Tasks.Task" || method.Name != "Delay")
        {
            return false;
        }

        return method.Parameters.All(parameter => parameter.Type.ToDisplayString() != "System.TimeProvider");
    }

    private static bool IsConfigureAwaitFalse(IInvocationOperation invocation)
    {
        var method = invocation.TargetMethod;

        if (method.Name != "ConfigureAwait" || invocation.Arguments.Length != 1)
        {
            return false;
        }

        var containingType = method.ContainingType.OriginalDefinition.ToDisplayString();

        if (containingType is not "System.Threading.Tasks.Task" and
            not "System.Threading.Tasks.Task<TResult>" and
            not "System.Threading.Tasks.ValueTask" and
            not "System.Threading.Tasks.ValueTask<TResult>")
        {
            return false;
        }

        var constant = invocation.Arguments[0].Value.ConstantValue;
        return constant.HasValue && constant.Value is false;
    }

    private static bool IsUncontrolledConcurrencyMethod(string containingType, string methodName)
    {
        return IsMethod(containingType, methodName, "System.Threading.Tasks.Task", "Run") ||
               IsMethod(containingType, methodName, "System.Threading.Tasks.TaskFactory", "StartNew") ||
               IsMethod(containingType, methodName, "System.Threading.Thread", "Start") ||
               IsMethod(containingType, methodName, "System.Threading.ThreadPool", "QueueUserWorkItem") ||
               IsMethod(containingType, methodName, "System.Threading.ThreadPool", "UnsafeQueueUserWorkItem") ||
               containingType == "System.Threading.Tasks.Parallel";
    }

    private static bool IsBlockingWaitMethod(string containingType, string methodName)
    {
        if (containingType == "System.Threading.Tasks.Task" && methodName is "Wait" or "WaitAll" or "WaitAny")
        {
            return true;
        }

        return containingType.Contains(".TaskAwaiter", StringComparison.Ordinal) && methodName == "GetResult" ||
               containingType.Contains(".ValueTaskAwaiter", StringComparison.Ordinal) && methodName == "GetResult" ||
               IsMethod(containingType, methodName, "System.Threading.Thread", "Join") ||
               IsMethod(containingType, methodName, "System.Threading.WaitHandle", "WaitOne") ||
               IsMethod(containingType, methodName, "System.Threading.WaitHandle", "WaitAll") ||
               IsMethod(containingType, methodName, "System.Threading.WaitHandle", "WaitAny");
    }

    private static bool IsWallClockProperty(string containingType, string propertyName)
    {
        if (containingType == "System.DateTime" && propertyName is "Now" or "UtcNow" or "Today")
        {
            return true;
        }

        if (containingType == "System.DateTimeOffset" && propertyName is "Now" or "UtcNow")
        {
            return true;
        }

        if (containingType == "System.Environment" && propertyName is "TickCount" or "TickCount64")
        {
            return true;
        }

        return containingType == "System.TimeProvider" && propertyName == "System";
    }

    private static bool IsRealTimerCreation(IObjectCreationOperation creation)
    {
        var type = creation.Type?.ToDisplayString();

        if (type is "System.Threading.Timer" or "System.Timers.Timer")
        {
            return true;
        }

        if (type != "System.Threading.PeriodicTimer")
        {
            return false;
        }

        return creation.Constructor?.Parameters.All(parameter => parameter.Type.ToDisplayString() != "System.TimeProvider") != false;
    }

    private static bool IsTimedCancellationTokenSource(IObjectCreationOperation creation)
    {
        if (creation.Type?.ToDisplayString() != "System.Threading.CancellationTokenSource" || creation.Constructor is null)
        {
            return false;
        }

        var hasTimeProvider = creation.Constructor.Parameters.Any(parameter => parameter.Type.ToDisplayString() == "System.TimeProvider");

        if (hasTimeProvider)
        {
            return false;
        }

        return creation.Constructor.Parameters.Any(
            parameter => parameter.Type.SpecialType == SpecialType.System_Int32 || parameter.Type.ToDisplayString() == "System.TimeSpan");
    }

    private static bool InheritsFrom(ITypeSymbol? type, string metadataName)
    {
        for (var current = type; current is not null; current = current.BaseType)
        {
            if (current.ToDisplayString() == metadataName)
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsMethod(string containingType, string methodName, string expectedType, string expectedMethod)
    {
        return containingType == expectedType && methodName == expectedMethod;
    }
}
