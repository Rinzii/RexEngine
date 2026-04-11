#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferOtherTypeAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeType = "Rex.Shared.Analyzers.PreferOtherTypeAttribute";

    private static readonly DiagnosticDescriptor s_preferOtherTypeDescriptor = new(
        IdPreferOtherType,
        "Use the specific type",
        "Use the specific type {0} instead of {1} when the type argument is {2}",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Use the specific type.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_preferOtherTypeDescriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics |
                                               GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeField, SyntaxKind.VariableDeclaration);
    }

    private void AnalyzeField(SyntaxNodeAnalysisContext context)
    {
        if (context.Node is not VariableDeclarationSyntax node)
        {
            return;
        }

        // Get the type of the generic being used
        if (node.Type is not GenericNameSyntax genericName)
        {
            return;
        }

        TypeSyntax genericSyntax = genericName.TypeArgumentList.Arguments[0];
        if (context.SemanticModel.GetSymbolInfo(genericSyntax).Symbol is not { } genericType)
        {
            return;
        }

        // Look for the PreferOtherTypeAttribute
        SymbolInfo symbolInfo = context.SemanticModel.GetSymbolInfo(node.Type);
        if (symbolInfo.Symbol?.GetAttributes() is not { } attributes)
        {
            return;
        }

        INamedTypeSymbol? preferOtherTypeAttribute = context.Compilation.GetTypeByMetadataName(AttributeType);

        foreach (AttributeData attribute in attributes)
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, preferOtherTypeAttribute))
            {
                continue;
            }

            // See if the generic type argument matches the type the attribute specifies
            if (attribute.ConstructorArguments[0].Value is not ITypeSymbol checkedType)
            {
                return;
            }

            if (!SymbolEqualityComparer.Default.Equals(checkedType, genericType))
            {
                continue;
            }

            if (attribute.ConstructorArguments[1].Value is not ITypeSymbol replacementType)
            {
                continue;
            }

            context.ReportDiagnostic(Diagnostic.Create(s_preferOtherTypeDescriptor,
                context.Node.GetLocation(),
                replacementType.Name,
                symbolInfo.Symbol.Name,
                genericType.Name));
        }
    }
}
