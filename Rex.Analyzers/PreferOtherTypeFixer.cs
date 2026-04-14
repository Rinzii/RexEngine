#nullable enable
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Text;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[ExportCodeFixProvider(LanguageNames.CSharp)]
public sealed class PreferOtherTypeFixer : CodeFixProvider
{
    private const string PreferOtherTypeAttributeName = "PreferOtherTypeAttribute";

    public override ImmutableArray<string> FixableDiagnosticIds => [IdPreferOtherType];

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case IdPreferOtherType:
                    return RegisterReplaceType(context, diagnostic);
            }
        }

        return Task.CompletedTask;
    }

    private static async Task RegisterReplaceType(CodeFixContext context, Diagnostic diagnostic)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        TextSpan span = diagnostic.Location.SourceSpan;
        VariableDeclarationSyntax? token = root?.FindToken(span.Start).Parent?.AncestorsAndSelf()
            .OfType<VariableDeclarationSyntax>().First();

        if (token == null)
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create(
            "Replace type",
            c => ReplaceType(context.Document, token, c),
            "Replace type"
        ), diagnostic);
    }

    private static async Task<Document> ReplaceType(Document document, VariableDeclarationSyntax syntax,
        CancellationToken cancellation)
    {
        var root = (CompilationUnitSyntax?)await document.GetSyntaxRootAsync(cancellation);
        SemanticModel? model = await document.GetSemanticModelAsync(cancellation);

        if (model == null || syntax.Type is not GenericNameSyntax genericNameSyntax)
        {
            return document;
        }

        TypeSyntax genericTypeSyntax = genericNameSyntax.TypeArgumentList.Arguments[0];
        if (model.GetSymbolInfo(genericTypeSyntax).Symbol is not { } genericTypeSymbol)
        {
            return document;
        }

        SymbolInfo symbolInfo = model.GetSymbolInfo(syntax.Type);
        if (symbolInfo.Symbol?.GetAttributes() is not { } attributes)
        {
            return document;
        }

        foreach (AttributeData attribute in attributes)
        {
            if (attribute.AttributeClass?.Name != PreferOtherTypeAttributeName)
            {
                continue;
            }

            if (attribute.ConstructorArguments[0].Value is not ITypeSymbol checkedTypeSymbol)
            {
                continue;
            }

            if (!SymbolEqualityComparer.Default.Equals(checkedTypeSymbol, genericTypeSymbol))
            {
                continue;
            }

            if (attribute.ConstructorArguments[1].Value is not ITypeSymbol replacementTypeSymbol)
            {
                continue;
            }

            IdentifierNameSyntax replacementIdentifier = SyntaxFactory.IdentifierName(replacementTypeSymbol.Name);
            VariableDeclarationSyntax replacementSyntax = syntax.WithType(replacementIdentifier);

            root = root!.ReplaceNode(syntax, replacementSyntax);
            return document.WithSyntaxRoot(root);
        }

        return document;
    }
}
