using System.Collections.Immutable;
using System.Diagnostics.CodeAnalysis;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CodeActions;
using Microsoft.CodeAnalysis.CodeFixes;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Text;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public class SerializableAnalyzer : DiagnosticAnalyzer
{
    // Metadata of the analyzer

    // You could use LocalizedString but it's a little more complicated for this sample

    private const string RequiresSerializableAttributeMetadataName =
        "Rex.Shared.Analyzers.RequiresSerializableAttribute";

    private const string SerializableAttributeMetadataName = "System.SerializableAttribute";
    private const string NetSerializableAttributeMetadataName = "Rex.Shared.Serialization.NetSerializableAttribute";

    [SuppressMessage("ReSharper", "RS2008")]
    private static readonly DiagnosticDescriptor s_rule = new(
        IdSerializable,
        "Class not marked as (Net)Serializable",
        "Class not marked as (Net)Serializable",
        "Usage",
        DiagnosticSeverity.Warning,
        true,
        "The class should be marked as (Net)Serializable.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze | GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterSyntaxNodeAction(AnalyzeNode, SyntaxKind.ClassDeclaration);
    }

    private bool Marked(INamedTypeSymbol namedTypeSymbol, INamedTypeSymbol attrSymbol)
    {
        if (namedTypeSymbol == null)
        {
            return false;
        }

        if (namedTypeSymbol.GetAttributes()
            .Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, attrSymbol)))
        {
            return true;
        }

        return Marked(namedTypeSymbol.BaseType, attrSymbol);
    }

    private void AnalyzeNode(SyntaxNodeAnalysisContext context)
    {
        INamedTypeSymbol attrSymbol =
            context.Compilation.GetTypeByMetadataName(RequiresSerializableAttributeMetadataName);
        var classDecl = (ClassDeclarationSyntax)context.Node;
        INamedTypeSymbol classSymbol = context.SemanticModel.GetDeclaredSymbol(classDecl);
        if (classSymbol == null)
        {
            return;
        }

        if (Marked(classSymbol, attrSymbol))
        {
            ImmutableArray<AttributeData> attributes = classSymbol.GetAttributes();
            INamedTypeSymbol serAttr = context.Compilation.GetTypeByMetadataName(SerializableAttributeMetadataName);
            INamedTypeSymbol netSerAttr =
                context.Compilation.GetTypeByMetadataName(NetSerializableAttributeMetadataName);

            bool hasSerAttr = attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, serAttr));
            bool hasNetSerAttr =
                attributes.Any(a => SymbolEqualityComparer.Default.Equals(a.AttributeClass, netSerAttr));

            if (!hasSerAttr || !hasNetSerAttr)
            {
                var requiredAttributes = new List<string>();
                if (!hasSerAttr)
                {
                    requiredAttributes.Add(SerializableAttributeMetadataName);
                }

                if (!hasNetSerAttr)
                {
                    requiredAttributes.Add(NetSerializableAttributeMetadataName);
                }

                context.ReportDiagnostic(
                    Diagnostic.Create(
                        s_rule,
                        classDecl.Identifier.GetLocation(),
                        ImmutableDictionary.CreateRange(new Dictionary<string, string>
                        {
                            {
                                "requiredAttributes", string.Join(",", requiredAttributes)
                            }
                        })));
            }
        }
    }
}

[ExportCodeFixProvider(LanguageNames.CSharp)]
public class SerializableCodeFixProvider : CodeFixProvider
{
    private const string Title = "Annotate class as (Net)Serializable.";

    public sealed override ImmutableArray<string> FixableDiagnosticIds
        => [IdSerializable];

    public override async Task RegisterCodeFixesAsync(CodeFixContext context)
    {
        SyntaxNode root = await context.Document.GetSyntaxRootAsync(context.CancellationToken);

        foreach (Diagnostic diagnostic in context.Diagnostics)
        {
            TextSpan span = diagnostic.Location.SourceSpan;
            ClassDeclarationSyntax classDecl = root.FindToken(span.Start).Parent.AncestorsAndSelf()
                .OfType<ClassDeclarationSyntax>()
                .First();

            if (!diagnostic.Properties.TryGetValue("requiredAttributes", out string requiredAttributes))
            {
                return;
            }

            context.RegisterCodeFix(
                CodeAction.Create(
                    Title,
                    c => FixAsync(context.Document, classDecl, requiredAttributes, c),
                    Title),
                diagnostic);
        }
    }

    private async Task<Document> FixAsync(Document document, ClassDeclarationSyntax classDecl,
        string requiredAttributes, CancellationToken cancellationToken)
    {
        var attributes = new List<AttributeSyntax>();
        var namespaces = new List<string>();
        foreach (string attribute in requiredAttributes.Split(','))
        {
            string[] tempSplit = attribute.Split('.');
            namespaces.Add(string.Join(".", tempSplit.Take(tempSplit.Length - 1)));
            string @class = tempSplit.Last();
            @class = @class[..^9]; //cut out "Attribute" at the end
            attributes.Add(SyntaxFactory.Attribute(SyntaxFactory.ParseName(@class)));
        }

        ClassDeclarationSyntax newClassDecl =
            classDecl.AddAttributeLists(SyntaxFactory.AttributeList(SyntaxFactory.SeparatedList(attributes)));

        var root = (CompilationUnitSyntax)await document.GetSyntaxRootAsync(cancellationToken);
        root = root.ReplaceNode(classDecl, newClassDecl);

        foreach (string ns in namespaces)
        {
            if (root.Usings.Any(u => u.Name.ToString() == ns))
            {
                continue;
            }

            root = root.AddUsings(SyntaxFactory.UsingDirective(SyntaxFactory.ParseName(ns)));
        }

        return document.WithSyntaxRoot(root);
    }

    public override FixAllProvider GetFixAllProvider()
    {
        return WellKnownFixAllProviders.BatchFixer;
    }
}
