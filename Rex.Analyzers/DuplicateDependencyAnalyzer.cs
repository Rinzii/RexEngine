using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.CSharp;
using Microsoft.CodeAnalysis.CSharp.Syntax;
using Microsoft.CodeAnalysis.Diagnostics;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

#nullable enable

/// <summary>
/// Analyzer that detects duplicate <c>[Dependency]</c> fields inside a single type.
/// </summary>
[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class DuplicateDependencyAnalyzer : DiagnosticAnalyzer
{
    private const string DependencyAttributeType = "Rex.Shared.IoC.DependencyAttribute";

    private static readonly DiagnosticDescriptor s_rule = new(
        IdDuplicateDependency,
        "Duplicate dependency field",
        "Another [Dependency] field of type '{0}' already exists in this type with field '{1}'",
        "Usage",
        DiagnosticSeverity.Warning,
        true);

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_rule];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.None);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            INamedTypeSymbol? dependencyAttributeType =
                compilationContext.Compilation.GetTypeByMetadataName(DependencyAttributeType);
            if (dependencyAttributeType == null)
            {
                return;
            }

            compilationContext.RegisterSymbolStartAction(symbolContext =>
                {
                    var typeSymbol = (INamedTypeSymbol)symbolContext.Symbol;
                    // Only instance classes carry dependencies; skip static types.
                    if (typeSymbol.TypeKind != TypeKind.Class || typeSymbol.IsStatic)
                    {
                        return;
                    }

                    var state = new AnalyzerState(dependencyAttributeType);
                    symbolContext.RegisterSyntaxNodeAction(state.AnalyzeField, SyntaxKind.FieldDeclaration);
                    symbolContext.RegisterSymbolEndAction(state.End);
                },
                SymbolKind.NamedType);
        });
    }

    private sealed class AnalyzerState(INamedTypeSymbol dependencyAttributeType)
    {
        private readonly Dictionary<ITypeSymbol, List<IFieldSymbol>> _dependencyFields =
            new(SymbolEqualityComparer.Default);

        public void AnalyzeField(SyntaxNodeAnalysisContext context)
        {
            var field = (FieldDeclarationSyntax)context.Node;
            if (field.AttributeLists.Count == 0)
            {
                return;
            }

            if (context.ContainingSymbol is not IFieldSymbol fieldSymbol)
            {
                return;
            }

            // Cannot use [Dependency] on types that are not reference types.
            if (!fieldSymbol.Type.IsReferenceType)
            {
                return;
            }

            if (!IsDependency(context.ContainingSymbol))
            {
                return;
            }

            lock (_dependencyFields)
            {
                if (!_dependencyFields.TryGetValue(fieldSymbol.Type, out List<IFieldSymbol>? dependencyFields))
                {
                    dependencyFields = [];
                    _dependencyFields.Add(fieldSymbol.Type, dependencyFields);
                }

                dependencyFields.Add(fieldSymbol);
            }
        }

        private bool IsDependency(ISymbol symbol)
        {
            foreach (AttributeData attributeData in symbol.GetAttributes())
            {
                if (SymbolEqualityComparer.Default.Equals(attributeData.AttributeClass, dependencyAttributeType))
                {
                    return true;
                }
            }

            return false;
        }

        public void End(SymbolAnalysisContext context)
        {
            lock (_dependencyFields)
            {
                foreach (KeyValuePair<ITypeSymbol, List<IFieldSymbol>> pair in _dependencyFields)
                {
                    ITypeSymbol fieldType = pair.Key;
                    List<IFieldSymbol> fields = pair.Value;
                    if (fields.Count <= 1)
                    {
                        continue;
                    }

                    // Sort so we can have deterministic order to skip reporting for a single field.
                    // Whichever sorts first doesn't get reported.
                    fields.Sort(static (a, b) => string.Compare(a.Name, b.Name, StringComparison.Ordinal));

                    // Start at index 1 to skip first field.
                    IFieldSymbol firstField = fields[0];
                    for (int i = 1; i < fields.Count; i++)
                    {
                        IFieldSymbol field = fields[i];

                        context.ReportDiagnostic(
                            Diagnostic.Create(s_rule, field.Locations[0], fieldType.ToDisplayString(),
                                firstField.Name));
                    }
                }
            }
        }
    }
}
