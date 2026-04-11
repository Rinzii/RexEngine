using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferNonGenericVariantForAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeType = "Rex.Shared.Analyzers.PreferNonGenericVariantForAttribute";

    private static readonly DiagnosticDescriptor s_useNonGenericVariantDescriptor = new(
        IdUseNonGenericVariant,
        "Consider using the non-generic variant of this method",
        "Use the non-generic variant of this method for type {0}",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "Use the generic variant of this method.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_useNonGenericVariantDescriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics |
                                               GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(CheckForNonGenericVariant, OperationKind.Invocation);
    }

    private void CheckForNonGenericVariant(OperationAnalysisContext obj)
    {
        if (obj.Operation is not IInvocationOperation invocationOperation)
        {
            return;
        }

        INamedTypeSymbol preferNonGenericAttribute = obj.Compilation.GetTypeByMetadataName(AttributeType);

        HashSet<ITypeSymbol> forTypes = [];
        foreach (AttributeData attribute in invocationOperation.TargetMethod.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, preferNonGenericAttribute))
            {
                continue;
            }

            foreach (TypedConstant type in attribute.ConstructorArguments[0].Values)
            {
                _ = forTypes.Add((ITypeSymbol)type.Value);
            }

            break;
        }

        if (forTypes == null)
        {
            return;
        }

        foreach (ITypeSymbol typeArg in invocationOperation.TargetMethod.TypeArguments)
        {
            if (forTypes.Contains(typeArg))
            {
                obj.ReportDiagnostic(
                    Diagnostic.Create(s_useNonGenericVariantDescriptor,
                        invocationOperation.Syntax.GetLocation(), typeArg.Name));
            }
        }
    }
}
