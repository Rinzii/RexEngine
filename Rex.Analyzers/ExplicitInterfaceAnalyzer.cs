using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class ExplicitInterfaceAnalyzer : DiagnosticAnalyzer
{
    private const string RequiresExplicitImplementationAttributeMetadataName =
        "Rex.Shared.Analyzers.RequiresExplicitImplementationAttribute";

    [SuppressMessage("ReSharper", "RS2008")]
    private static readonly DiagnosticDescriptor s_rule = new(
        IdExplicitInterface,
        "No explicit interface specified",
        "No explicit interface specified",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "Make sure to specify the interface in your method-declaration.");

    private readonly SyntaxKind[] _excludedModifiers =
    [
        SyntaxKind.VirtualKeyword,
        SyntaxKind.AbstractKeyword,
        SyntaxKind.OverrideKeyword
    ];

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.MethodDeclaration);
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.PropertyDeclaration);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        ISymbol symbol;
        Location location;
        switch (context.Node)
        {
            //we already have a explicit interface specified, no need to check further
            case MethodDeclarationSyntax methodDecl when methodDecl.ExplicitInterfaceSpecifier != null ||
                                                         methodDecl.Modifiers.Any(m =>
                                                             _excludedModifiers.Contains(m.Kind())):
                return;
            case PropertyDeclarationSyntax propertyDecl when propertyDecl.ExplicitInterfaceSpecifier != null ||
                                                             propertyDecl.Modifiers.Any(m =>
                                                                 _excludedModifiers.Contains(m.Kind())):
                return;

            case MethodDeclarationSyntax methodDecl:
                symbol = context.SemanticModel.GetDeclaredSymbol(methodDecl);
                location = methodDecl.Identifier.GetLocation();
                break;
            case PropertyDeclarationSyntax propertyDecl:
                symbol = context.SemanticModel.GetDeclaredSymbol(propertyDecl);
                location = propertyDecl.Identifier.GetLocation();
                break;

            default:
                return;
        }

        INamedTypeSymbol attrSymbol =
            context.Compilation.GetTypeByMetadataName(RequiresExplicitImplementationAttributeMetadataName);

        bool isInterfaceMember = symbol?.ContainingType.AllInterfaces.Any(i =>
            i.GetMembers().Any(m =>
                SymbolEqualityComparer.Default.Equals(symbol,
                    symbol.ContainingType.FindImplementationForInterfaceMember(m)))
            && i.GetAttributes().Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrSymbol))
        ) ?? false;

        if (isInterfaceMember)
        {
            //we do not have an explicit interface specified. bad!
            var diagnostic = Diagnostic.Create(
                s_rule,
                location);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
