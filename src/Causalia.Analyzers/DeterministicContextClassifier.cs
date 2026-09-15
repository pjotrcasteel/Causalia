using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Operations;

namespace Causalia.Analyzers;

internal static class DeterministicContextClassifier
{
    private const string AllowAttributeName = "Causalia.AllowNondeterminismAttribute";
    private const string DeterministicAttributeName = "Causalia.DeterministicSimulationAttribute";

    public static bool IsDeterministic(IOperation operation, ISymbol containingSymbol)
    {
        if (IsExplicitlyAllowed(containingSymbol))
        {
            return false;
        }

        if (IsExplicitlyDeterministic(containingSymbol) || IsSimulationBackgroundServiceExecute(containingSymbol))
        {
            return true;
        }

        return IsInsideScenarioDelegate(operation);
    }

    public static bool IsExplicitlyAllowed(ISymbol symbol)
    {
        return HasAttributeInSymbolChain(symbol, AllowAttributeName);
    }

    public static bool IsExplicitlyDeterministic(ISymbol symbol)
    {
        return HasAttributeInSymbolChain(symbol, DeterministicAttributeName);
    }

    public static bool IsScenarioMethodReference(IMethodReferenceOperation methodReference)
    {
        return IsScenarioArgument(methodReference);
    }

    private static bool IsSimulationBackgroundServiceExecute(ISymbol symbol)
    {
        for (var current = symbol as IMethodSymbol; current is not null; current = current.OverriddenMethod)
        {
            if (current.Name == "ExecuteAsync" &&
                current.ContainingType.ToDisplayString() == "Causalia.AspNetCore.SimulationBackgroundService")
            {
                return true;
            }
        }

        return false;
    }

    private static bool HasAttributeInSymbolChain(ISymbol? symbol, string metadataName)
    {
        while (symbol is not null)
        {
            if (symbol.GetAttributes().Any(attribute => attribute.AttributeClass?.ToDisplayString() == metadataName))
            {
                return true;
            }

            symbol = symbol.ContainingSymbol;
        }

        return false;
    }

    private static bool IsInsideScenarioDelegate(IOperation operation)
    {
        for (var current = operation.Parent; current is not null; current = current.Parent)
        {
            if (current is IAnonymousFunctionOperation anonymousFunction && IsScenarioArgument(anonymousFunction))
            {
                return true;
            }
        }

        return false;
    }

    private static bool IsScenarioArgument(IOperation operation)
    {
        IOperation? current = operation.Parent;

        while (current is IDelegateCreationOperation or IConversionOperation)
        {
            current = current.Parent;
        }

        if (current is not IArgumentOperation argument || argument.Parameter is null)
        {
            return false;
        }

        return IsSimulationScenarioDelegate(argument.Parameter.Type) || IsKnownDeterministicHandler(argument);
    }

    private static bool IsSimulationScenarioDelegate(ITypeSymbol type)
    {
        if (type is not INamedTypeSymbol namedType || namedType.Arity != 2 || namedType.Name != "Func")
        {
            return false;
        }

        if (namedType.ContainingNamespace.ToDisplayString() != "System")
        {
            return false;
        }

        var contextType = namedType.TypeArguments[0].ToDisplayString();
        var taskType = namedType.TypeArguments[1].ToDisplayString();
        return taskType == "System.Threading.Tasks.Task" &&
               contextType is "Causalia.SimulationContext" or
                   "Causalia.Load.LoadIterationContext" or
                   "Causalia.Processes.SimulationProcessGenerationContext";
    }

    private static bool IsKnownDeterministicHandler(IArgumentOperation argument)
    {
        var parameterName = argument.Parameter?.Name;

        if (argument.Parent is IInvocationOperation invocation)
        {
            var containingType = invocation.TargetMethod.ContainingType.ToDisplayString();
            var methodName = invocation.TargetMethod.Name;

            return (parameterName == "handler" &&
                    ((containingType == "Causalia.Dapr.SimulationDaprPubSub" && methodName == "Subscribe") ||
                     (containingType == "Causalia.Grpc.SimulationGrpcServer" && methodName == "RegisterUnary"))) ||
                   (parameterName == "operation" &&
                    containingType == "Causalia.Exploration.SimulationExploration" &&
                    methodName == "RunAsync") ||
                   (parameterName is "transition" or "executeAsync" or "verify" or "precondition" &&
                    containingType == "Causalia.ModelBased.ModelCommand" &&
                    methodName == "Create") ||
                   (parameterName is "capture" or "formatter" &&
                    containingType == "Causalia.TimeTravel.SimulationTimeTravel" &&
                    methodName == "Watch") ||
                   (parameterName == "handler" &&
                    containingType == "Causalia.ProductionReality.SimulationProductionReality" &&
                    methodName == "ReplayIncidentAsync");
        }

        if (argument.Parent is not IObjectCreationOperation creation || creation.Constructor is null)
        {
            return false;
        }

        var constructedType = creation.Constructor.ContainingType.OriginalDefinition;
        return parameterName is "initialStateFactory" or "systemFactory" or "commands" &&
               constructedType.Name == "ModelBasedSpecification" &&
               constructedType.ContainingNamespace.ToDisplayString() == "Causalia.ModelBased";
    }

}
