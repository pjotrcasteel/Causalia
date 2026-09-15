using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Causalia.Analyzers;

/// <summary>
/// Reports APIs that escape Causalia's deterministic execution model.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class DeterminismAnalyzer : DiagnosticAnalyzer
{
    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        ImmutableArray.Create(
            DiagnosticDescriptors.VirtualTime,
            DiagnosticDescriptors.Randomness,
            DiagnosticDescriptors.Concurrency,
            DiagnosticDescriptors.BlockingWait,
            DiagnosticDescriptors.ScenarioMethod,
            DiagnosticDescriptors.EscapedAwait);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzeInvocation, OperationKind.Invocation);
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
        context.RegisterOperationAction(AnalyzeObjectCreation, OperationKind.ObjectCreation);
        context.RegisterOperationAction(AnalyzeMethodReference, OperationKind.MethodReference);
    }

    private static void AnalyzeInvocation(OperationAnalysisContext context)
    {
        var invocation = (IInvocationOperation)context.Operation;

        if (!DeterministicContextClassifier.IsDeterministic(invocation, context.ContainingSymbol) ||
            !NondeterministicApiClassifier.TryClassifyInvocation(invocation, out var descriptor, out var guidance))
        {
            return;
        }

        Report(context, descriptor, invocation.Syntax.GetLocation(), invocation.TargetMethod.ToDisplayString(), guidance);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;

        if (!DeterministicContextClassifier.IsDeterministic(propertyReference, context.ContainingSymbol) ||
            !NondeterministicApiClassifier.TryClassifyProperty(propertyReference, out var descriptor, out var guidance))
        {
            return;
        }

        Report(context, descriptor, propertyReference.Syntax.GetLocation(), propertyReference.Property.ToDisplayString(), guidance);
    }

    private static void AnalyzeMethodReference(OperationAnalysisContext context)
    {
        var methodReference = (IMethodReferenceOperation)context.Operation;
        var method = methodReference.Method;

        if (!DeterministicContextClassifier.IsScenarioMethodReference(methodReference) ||
            DeterministicContextClassifier.IsExplicitlyDeterministic(method) ||
            DeterministicContextClassifier.IsExplicitlyAllowed(method))
        {
            return;
        }

        Report(
            context,
            DiagnosticDescriptors.ScenarioMethod,
            methodReference.Syntax.GetLocation(),
            method.ToDisplayString(),
            "Add [DeterministicSimulation] to the scenario method, or [AllowNondeterminism(\"reason\")] for an intentional boundary.");
    }

    private static void AnalyzeObjectCreation(OperationAnalysisContext context)
    {
        var creation = (IObjectCreationOperation)context.Operation;

        if (!DeterministicContextClassifier.IsDeterministic(creation, context.ContainingSymbol) ||
            !NondeterministicApiClassifier.TryClassifyObjectCreation(creation, out var descriptor, out var guidance))
        {
            return;
        }

        var displayName = creation.Constructor?.ToDisplayString() ?? creation.Type?.ToDisplayString() ?? "object creation";
        Report(context, descriptor, creation.Syntax.GetLocation(), displayName, guidance);
    }

    private static void Report(
        OperationAnalysisContext context,
        DiagnosticDescriptor descriptor,
        Location location,
        string api,
        string guidance)
    {
        context.ReportDiagnostic(Diagnostic.Create(descriptor, location, api, guidance));
    }
}
