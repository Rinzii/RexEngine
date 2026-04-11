using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class TaskResultAnalyzer : DiagnosticAnalyzer
{
    [SuppressMessage("ReSharper", "RS2008")]
    private static readonly DiagnosticDescriptor s_resultRule = new(
        IdTaskResult,
        "Risk of deadlock from accessing Task<T>.Result",
        "Accessing Task<T>.Result is dangerous and can cause deadlocks in some contexts. If you understand how this works and are certain that you aren't causing a deadlock here, mute this error with #pragma.",
        "Usage",
        DiagnosticSeverity.Error,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
        [s_resultRule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(Check, OperationKind.PropertyReference);
    }

    private static void Check(OperationAnalysisContext context)
    {
        INamedTypeSymbol taskType = context.Compilation.GetTypeByMetadataName("System.Threading.Tasks.Task`1");

        var operation = (IPropertyReferenceOperation)context.Operation;
        ISymbol member = operation.Member;

        if (member.Name == "Result" &&
            taskType.Equals(member.ContainingType.ConstructedFrom, SymbolEqualityComparer.Default))
        {
            var diag = Diagnostic.Create(s_resultRule, operation.Syntax.GetLocation());
            context.ReportDiagnostic(diag);
        }
    }
}
