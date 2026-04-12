using System.Collections.Immutable;
using System.Diagnostics;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class PreferGenericVariantAnalyzer : DiagnosticAnalyzer
{
    private const string AttributeType = "Rex.Shared.Analyzers.PreferGenericVariantAttribute";

    private static readonly DiagnosticDescriptor s_useGenericVariantDescriptor = new(
        IdUseGenericVariant,
        "Consider using the generic variant of this method",
        "Consider using the generic variant of this method to avoid potential allocations",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "Consider using the generic variant of this method to avoid potential allocations.");

    private static readonly DiagnosticDescriptor s_useGenericVariantInvalidUsageDescriptor = new(
        IdUseGenericVariantInvalidUsage,
        "Invalid generic variant provided",
        "Generic variant provided mismatches the amount of type parameters of non-generic variant",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "The non-generic variant should have at least as many type parameter at the beginning of the method as there are generic type parameters on the generic variant.");

    private static readonly DiagnosticDescriptor s_useGenericVariantAttributeValueErrorDescriptor = new(
        IdUseGenericVariantAttributeValueError,
        "Failed resolving generic variant value",
        "Failed resolving generic variant value: {0}",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Consider using nameof to avoid any typos.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        s_useGenericVariantDescriptor, s_useGenericVariantInvalidUsageDescriptor,
        s_useGenericVariantAttributeValueErrorDescriptor
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.ReportDiagnostics |
                                               GeneratedCodeAnalysisFlags.Analyze);
        context.EnableConcurrentExecution();
        context.RegisterOperationAction(CheckForGenericVariant, OperationKind.Invocation);
    }

    private void CheckForGenericVariant(OperationAnalysisContext obj)
    {
        if (obj.Operation is not IInvocationOperation invocationOperation)
        {
            return;
        }

        INamedTypeSymbol preferGenericAttribute = obj.Compilation.GetTypeByMetadataName(AttributeType);

        string genericVariant = null;
        AttributeData foundAttribute = null;
        foreach (AttributeData attribute in invocationOperation.TargetMethod.GetAttributes())
        {
            if (!SymbolEqualityComparer.Default.Equals(attribute.AttributeClass, preferGenericAttribute))
            {
                continue;
            }

            genericVariant = attribute.ConstructorArguments[0].Value as string ?? invocationOperation.TargetMethod.Name;
            foundAttribute = attribute;
            break;
        }

        if (genericVariant == null)
        {
            return;
        }

        int maxTypeParams = 0;
        INamedTypeSymbol typeTypeSymbol = obj.Compilation.GetTypeByMetadataName("System.Type");
        foreach (IParameterSymbol parameter in invocationOperation.TargetMethod.Parameters)
        {
            if (!SymbolEqualityComparer.Default.Equals(parameter.Type, typeTypeSymbol))
            {
                break;
            }

            maxTypeParams++;
        }

        if (maxTypeParams == 0)
        {
            obj.ReportDiagnostic(
                Diagnostic.Create(s_useGenericVariantInvalidUsageDescriptor,
                    foundAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation()));
            return;
        }

        IMethodSymbol genericVariantMethod = null;
        foreach (ISymbol member in invocationOperation.TargetMethod.ContainingType.GetMembers())
        {
            if (member is not IMethodSymbol methodSymbol
                || methodSymbol.Name != genericVariant
                || !methodSymbol.IsGenericMethod
                || methodSymbol.TypeParameters.Length > maxTypeParams
                || methodSymbol.Parameters.Length > invocationOperation.TargetMethod.Parameters.Length -
                methodSymbol.TypeParameters.Length
               )
            {
                continue;
            }

            int typeParamCount = methodSymbol.TypeParameters.Length;
            bool failedParamComparison = false;
            INamedTypeSymbol objType = obj.Compilation.GetSpecialType(SpecialType.System_Object);
            for (int i = 0; i < methodSymbol.Parameters.Length; i++)
            {
                if (methodSymbol.Parameters[i].Type is ITypeParameterSymbol &&
                    SymbolEqualityComparer.Default.Equals(
                        invocationOperation.TargetMethod.Parameters[i + typeParamCount].Type, objType))
                {
                    continue;
                }

                if (!SymbolEqualityComparer.Default.Equals(methodSymbol.Parameters[i].Type,
                        invocationOperation.TargetMethod.Parameters[i + typeParamCount].Type))
                {
                    failedParamComparison = true;
                    break;
                }
            }

            if (failedParamComparison)
            {
                continue;
            }

            genericVariantMethod = methodSymbol;
        }

        if (genericVariantMethod == null)
        {
            obj.ReportDiagnostic(Diagnostic.Create(
                s_useGenericVariantAttributeValueErrorDescriptor,
                foundAttribute.ApplicationSyntaxReference?.GetSyntax().GetLocation(),
                genericVariant));
            return;
        }

        string[] typeOperands = new string[genericVariantMethod.TypeParameters.Length];
        for (int i = 0; i < genericVariantMethod.TypeParameters.Length; i++)
        {
            switch (invocationOperation.Arguments[i].Value)
            {
                // TODO: figure out if ILocalReferenceOperation, IPropertyReferenceOperation or IFieldReferenceOperation is referencing static typeof assignments
                case ITypeOfOperation typeOfOperation:
                    typeOperands[i] =
                        typeOfOperation.TypeOperand.ToDisplayString(SymbolDisplayFormat.FullyQualifiedFormat);
                    continue;
                default:
                    return;
            }
        }

        obj.ReportDiagnostic(Diagnostic.Create(
            s_useGenericVariantDescriptor,
            invocationOperation.Syntax.GetLocation(),
            ImmutableDictionary.CreateRange(new Dictionary<string, string>
            {
                { "typeOperands", string.Join(",", typeOperands) }
            })));
    }
}

[ExportCodeFixProvider(LanguageNames.CSharp)]
public class PreferGenericVariantCodeFixProvider : CodeFixProvider
{
    public override ImmutableArray<string> FixableDiagnosticIds =>
        [IdUseGenericVariant];

    private static string Title(string method, string[] types)
    {
        return $"Use {method}<{string.Join(",", types)}>.";
    }

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetSyntaxRootAsync();
        if (root == null)
        {
            return;
        }

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            if (!diagnostic.Properties.TryGetValue("typeOperands", out string typeOperandsRaw)
                || typeOperandsRaw == null)
            {
                continue;
            }

            SyntaxNode node = root.FindNode(diagnostic.Location.SourceSpan);
            if (node is ArgumentSyntax argumentSyntax)
            {
                node = argumentSyntax.Expression;
            }

            if (node is not InvocationExpressionSyntax invocationExpression)
            {
                continue;
            }

            string[] typeOperands = typeOperandsRaw.Split(',');

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title(invocationExpression.Expression.ToString(), typeOperands),
                    c => FixAsync(context.Document, invocationExpression, typeOperands, c),
                    Title(invocationExpression.Expression.ToString(), typeOperands)),
                diagnostic);
        }
    }

    private async Task<Document> FixAsync(
        Document contextDocument,
        InvocationExpressionSyntax invocationExpression,
        string[] typeOperands,
        CancellationToken cancellationToken)
    {
        var memberAccess = (MemberAccessExpressionSyntax)invocationExpression.Expression;

        var root = (CompilationUnitSyntax)await contextDocument.GetSyntaxRootAsync(cancellationToken);

        var arguments = new ArgumentSyntax[invocationExpression.ArgumentList.Arguments.Count - typeOperands.Length];
        var types = new TypeSyntax[typeOperands.Length];

        for (int i = 0; i < typeOperands.Length; i++)
        {
            types[i] = ((TypeOfExpressionSyntax)invocationExpression.ArgumentList.Arguments[i].Expression).Type;
        }

        Array.Copy(
            invocationExpression.ArgumentList.Arguments.ToArray(),
            typeOperands.Length,
            arguments,
            0,
            arguments.Length);

        memberAccess = memberAccess.WithName(SyntaxFactory.GenericName(memberAccess.Name.Identifier,
            SyntaxFactory.TypeArgumentList(SyntaxFactory.SeparatedList(types))));

        Debug.Assert(root != null, nameof(root) + " != null");
        root = root.ReplaceNode(invocationExpression,
            invocationExpression
                .WithArgumentList(
                    invocationExpression.ArgumentList.WithArguments(SyntaxFactory.SeparatedList(arguments)))
                .WithExpression(memberAccess));

        return contextDocument.WithSyntaxRoot(root);
    }
}
