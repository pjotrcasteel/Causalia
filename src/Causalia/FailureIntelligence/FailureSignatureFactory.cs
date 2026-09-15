using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Causalia.Consistency;
using Causalia.Exceptions;
using Causalia.Internal;
using Causalia.Linearizability;
using Causalia.ModelBased;

namespace Causalia.FailureIntelligence;

internal static class FailureSignatureFactory
{
    private const string TokenPrefix = "fi2:";

    public static FailureSignature Create(SimulationFailedException failure)
    {
        ArgumentNullException.ThrowIfNull(failure);
        var exception = failure.InnerException ?? throw new InvalidOperationException("Simulation failure has no inner exception.");
        var kind = Classify(failure, exception);
        var exceptionType = exception.GetType().FullName ?? exception.GetType().Name;
        var semanticKey = CreateSemanticKey(failure, exception, kind);
        var canonical = CanonicalString.Create([((int)kind).ToString(CultureInfo.InvariantCulture), exceptionType, semanticKey]);
        var hash = SHA256.HashData(Encoding.UTF8.GetBytes(canonical));
        var token = TokenPrefix + Convert.ToHexString(hash.AsSpan(0, 16)).ToLowerInvariant();
        return new FailureSignature(token, kind, exceptionType, semanticKey);
    }

    private static FailureKind Classify(SimulationFailedException failure, Exception exception)
    {
        if (failure.InvariantFailure is SimulationInvariantEvaluationException)
        {
            return FailureKind.InvariantEvaluation;
        }

        if (failure.InvariantFailure is not null)
        {
            return FailureKind.InvariantViolation;
        }

        if (failure.LinearizabilityFailure is SimulationLinearizabilityInconclusiveException)
        {
            return FailureKind.LinearizabilityInconclusive;
        }

        if (failure.LinearizabilityFailure is not null)
        {
            return FailureKind.LinearizabilityViolation;
        }

        if (failure.ConsistencyFailure is not null)
        {
            return FailureKind.ConsistencyViolation;
        }

        if (failure.ModelFailure is not null)
        {
            return FailureKind.ModelViolation;
        }

        return exception switch
        {
            SimulationDeadlockException => FailureKind.Deadlock,
            SimulationStepLimitExceededException => FailureKind.StepLimitExceeded,
            _ => FailureKind.UnhandledException
        };
    }

    private static string CreateSemanticKey(SimulationFailedException failure, Exception exception, FailureKind kind)
    {
        return kind switch
        {
            FailureKind.InvariantEvaluation => CreateInvariantEvaluationKey(failure.InvariantFailure!),
            FailureKind.InvariantViolation => CreateInvariantKey(failure.InvariantFailure!),
            FailureKind.LinearizabilityViolation or FailureKind.LinearizabilityInconclusive =>
                CreateLinearizabilityKey(failure.LinearizabilityFailure!),
            FailureKind.ConsistencyViolation => CreateConsistencyKey(failure.ConsistencyFailure!),
            FailureKind.ModelViolation => CreateModelKey(failure.ModelFailure!),
            FailureKind.Deadlock => "deadlock",
            FailureKind.StepLimitExceeded => CreateStepLimitKey((SimulationStepLimitExceededException)exception),
            _ => exception.Message
        };
    }

    private static string CreateInvariantEvaluationKey(SimulationInvariantException invariant)
    {
        var evaluationException = invariant.InnerException;
        var evaluationType = evaluationException?.GetType().FullName ?? "unknown";
        var evaluationMessage = evaluationException?.Message ?? string.Empty;
        return CreateCompositeKey(invariant.InvariantName, invariant.Kind.ToString(), evaluationType, evaluationMessage);
    }

    private static string CreateInvariantKey(SimulationInvariantException invariant)
    {
        return CreateCompositeKey(invariant.InvariantName, invariant.Kind.ToString());
    }

    private static string CreateLinearizabilityKey(SimulationLinearizabilityException linearizability)
    {
        return CreateCompositeKey(linearizability.HistoryName, linearizability.Result.Status.ToString());
    }

    private static string CreateConsistencyKey(SimulationConsistencyViolationException consistency)
    {
        return CreateCompositeKey(consistency.HistoryName, consistency.Kind.ToString());
    }

    private static string CreateModelKey(SimulationModelViolationException model)
    {
        return CreateCompositeKey(model.ModelName, model.CommandName, model.ViolationMessage);
    }

    private static string CreateStepLimitKey(SimulationStepLimitExceededException stepLimit)
    {
        return $"maximum-steps:{stepLimit.MaximumSteps}";
    }

    private static string CreateCompositeKey(params string?[] values)
    {
        return CanonicalString.Create(values);
    }
}
