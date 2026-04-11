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
public sealed class ProxyForFixer : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
    [
        IdPreferProxy,
        IdProxyForRedundantMethodName
    ];

    public override FixAllProvider? GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            switch (diagnostic.Id)
            {
                case IdPreferProxy:
                    return RegisterSubstituteProxy(context, diagnostic);
                case IdProxyForRedundantMethodName:
                    return RegisterRemoveRedundantMethodName(context, diagnostic);
                default:
                    break;
            }
        }

        return Task.CompletedTask;
    }

    private async Task RegisterSubstituteProxy(CodeFixContext context, Diagnostic diagnostic)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        TextSpan span = diagnostic.Location.SourceSpan;
        InvocationExpressionSyntax? token = root?.FindToken(span.Start).Parent?.AncestorsAndSelf()
            .OfType<InvocationExpressionSyntax>().First();

        if (token == null)
        {
            return;
        }

        if (diagnostic.Properties[ProxyForAnalyzer.ProxyMethodName] is not string methodName)
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create(
            "Substitute proxy method",
            c => SubstituteProxy(context.Document, token, methodName, c),
            "Substitute proxy method"
        ), diagnostic);
    }

    private async Task<Document> SubstituteProxy(Document document, InvocationExpressionSyntax token, string methodName,
        CancellationToken cancellation)
    {
        var root = (CompilationUnitSyntax?)await document.GetSyntaxRootAsync(cancellation);
        SemanticModel? model = await document.GetSemanticModelAsync(cancellation);

        if (model == null)
        {
            return document;
        }

        if (token.Expression is not MemberAccessExpressionSyntax expression)
        {
            return document;
        }

        // Create a token with the proxy method name
        SyntaxToken identifierToken = SyntaxFactory.Identifier(methodName);
        // Create a replacement expression using the proxy method
        ExpressionSyntax newExpression = expression.Name switch
        {
            // Copy over any type arguments from the old invocation
            GenericNameSyntax old => SyntaxFactory.GenericName(identifierToken, old.TypeArgumentList),
            // Handle methods with no type arguments
            SimpleNameSyntax => SyntaxFactory.IdentifierName(identifierToken),
            _ => throw new InvalidOperationException()
        };
        // Create a replacement invocation expression
        InvocationExpressionSyntax replacement = token.WithExpression(newExpression).WithTriviaFrom(token);
        // Replace the original expression with the new one
        root = root!.ReplaceNode(token, replacement);

        return document.WithSyntaxRoot(root);
    }

    private async Task RegisterRemoveRedundantMethodName(CodeFixContext context, Diagnostic diagnostic)
    {
        SyntaxNode? root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);
        TextSpan span = diagnostic.Location.SourceSpan;
        AttributeArgumentSyntax? token = root?.FindToken(span.Start).Parent?.AncestorsAndSelf()
            .OfType<AttributeArgumentSyntax>().First();

        if (token == null)
        {
            return;
        }

        context.RegisterCodeFix(CodeAction.Create(
            "Remove method name parameter",
            c => RemoveRedundantMethodName(context.Document, token, c),
            "Remove method name parameter"
        ), diagnostic);
    }

    private async Task<Document> RemoveRedundantMethodName(Document document, AttributeArgumentSyntax token,
        CancellationToken cancellation)
    {
        var root = (CompilationUnitSyntax?)await document.GetSyntaxRootAsync(cancellation);
        SemanticModel? model = await document.GetSemanticModelAsync(cancellation);

        if (model == null)
        {
            return document;
        }

        // Get the argument list containing the offending argument
        if (token.Parent is not AttributeArgumentListSyntax listSyntax)
        {
            return document;
        }

        // Create a new list with the argument removed
        AttributeArgumentListSyntax newListSyntax = listSyntax.WithArguments(listSyntax.Arguments.Remove(token));

        // Replace the original argument list with the new one
        root = root!.ReplaceNode(listSyntax, newListSyntax);
        return document.WithSyntaxRoot(root);
    }
}
