#nullable enable

using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using Rex.Roslyn.Shared;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ValidateMemberAnalyzer : DiagnosticAnalyzer
{
    private const string ValidateMemberType = "Rex.Shared.Analyzers.ValidateMemberAttribute";

    private static readonly DiagnosticDescriptor s_validateMemberDescriptor = new(
        IdValidateMember,
        "Invalid member name",
        "{0} is not a member of {1}",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Be sure the type and member name are correct.");

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics => [s_validateMemberDescriptor];

    public override void Initialize(AnalysisContext context)
    {
        context.EnableConcurrentExecution();
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.RegisterOperationAction(AnalyzeOperation, OperationKind.Invocation);
    }

    private void AnalyzeOperation(OperationAnalysisContext context)
    {
        if (context.Operation is not IInvocationOperation node)
        {
            return;
        }

        IMethodSymbol methodSymbol = node.TargetMethod;

        // We need at least one type argument for context
        if (methodSymbol.TypeArguments.Length < 1)
        {
            return;
        }

        // We'll be checking members of the first type argument
        if (methodSymbol.TypeArguments[0] is not INamedTypeSymbol targetType)
        {
            return;
        }

        // Check each parameter of the method
        foreach (IArgumentOperation op in node.Arguments)
        {
            if (op.Parameter is null)
            {
                continue;
            }

            IParameterSymbol parameterSymbol = op.Parameter.OriginalDefinition;

            // Make sure the parameter has the ValidateMember attribute
            if (!AttributeHelper.HasAttribute(parameterSymbol, ValidateMemberType, out _))
            {
                continue;
            }

            // Find the value passed for this parameter.
            // GetConstantValue resolves constant expressions such as nameof() results.
            if (op.Value.ConstantValue is not { HasValue: true, Value: string fieldName })
            {
                continue;
            }

            // Check each member of the target type to see if it matches our passed in value
            bool found = false;
            foreach (ISymbol member in targetType.GetMembers())
            {
                if (member.Name == fieldName)
                {
                    found = true;
                    break;
                }
            }

            // If we didn't find it, report the violation
            if (!found)
            {
                context.ReportDiagnostic(Diagnostic.Create(
                    s_validateMemberDescriptor,
                    op.Syntax.GetLocation(),
                    fieldName,
                    targetType.Name
                ));
            }
        }
    }
}
