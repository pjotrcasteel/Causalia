using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;

namespace Causalia.Analyzers;

/// <summary>
/// Reports cancellation tokens that escape deterministic simulation cancellation flow.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp, LanguageNames.VisualBasic)]
public sealed class CancellationPropagationAnalyzer : DiagnosticAnalyzer
{
    private static readonly DiagnosticDescriptor Rule = new(
        "CAU1007",
        "Propagate simulation cancellation",
        "'{0}' bypasses the simulation cancellation token; {1}",
        "Causalia.Determinism",
        DiagnosticSeverity.Warning,
        isEnabledByDefault: true,
        description: "Deterministic simulation operations should remain cancellable through SimulationContext.CancellationToken.");

    /// <inheritdoc />
    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => ImmutableArray.Create(Rule);

    /// <inheritdoc />
    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(AnalyzePropertyReference, OperationKind.PropertyReference);
    }

    private static void AnalyzePropertyReference(OperationAnalysisContext context)
    {
        var propertyReference = (IPropertyReferenceOperation)context.Operation;

        if (!DeterministicContextClassifier.IsDeterministic(propertyReference, context.ContainingSymbol)
            || !IsCancellationTokenNone(propertyReference.Property))
        {
            return;
        }

        context.ReportDiagnostic(
            Diagnostic.Create(
                Rule,
                propertyReference.Syntax.GetLocation(),
                propertyReference.Property.ToDisplayString(),
                "pass context.CancellationToken or the cancellation token supplied to the current deterministic operation."));
    }

    private static bool IsCancellationTokenNone(IPropertySymbol property)
    {
        return property.Name == "None"
            && property.ContainingType.ToDisplayString() == "System.Threading.CancellationToken";
    }
}
