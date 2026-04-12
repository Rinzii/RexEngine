using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class MeansImplicitAssignmentSuppressor : DiagnosticSuppressor
{
    private const string MeansImplicitAssignmentAttribute = "Rex.Shared.MeansImplicitAssignmentAttribute";

    public override ImmutableArray<SuppressionDescriptor> SupportedSuppressions =>
        [MeansImplicitAssignment];

    public override void ReportSuppressions(SuppressionAnalysisContext context)
    {
        INamedTypeSymbol implAttr = context.Compilation.GetTypeByMetadataName(MeansImplicitAssignmentAttribute);
        if (implAttr == null)
        {
            return;
        }

        foreach (Diagnostic reportedDiagnostic in context.ReportedDiagnostics)
        {
            if (reportedDiagnostic.Id != MeansImplicitAssignment.SuppressedDiagnosticId)
            {
                continue;
            }

            SyntaxTree sourceTree = reportedDiagnostic.Location.SourceTree;
            if (sourceTree == null)
            {
                continue;
            }

            SyntaxNode node = sourceTree.GetRoot(context.CancellationToken)
                .FindNode(reportedDiagnostic.Location.SourceSpan);
            if (node == null)
            {
                continue;
            }

            ISymbol symbol = context.GetSemanticModel(sourceTree).GetDeclaredSymbol(node);

            if (symbol == null || !symbol.GetAttributes().Any(a =>
                    a.AttributeClass?.GetAttributes().Any(attr =>
                        SymbolEqualityComparer.Default.Equals(attr.AttributeClass, implAttr)) == true))
            {
                continue;
            }

            context.ReportSuppression(Suppression.Create(
                MeansImplicitAssignment,
                reportedDiagnostic));
        }
    }
}
