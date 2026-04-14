#nullable enable
using System.Collections.Immutable;
using Microsoft.CodeAnalysis;
using Microsoft.CodeAnalysis.Diagnostics;
using Microsoft.CodeAnalysis.Operations;
using static Microsoft.CodeAnalysis.SymbolEqualityComparer;
using static Rex.Roslyn.Shared.Diagnostics;

namespace Rex.Analyzers;

[DiagnosticAnalyzer(LanguageNames.CSharp)]
public sealed class ByRefEventAnalyzer : DiagnosticAnalyzer
{
    private const string ByRefAttribute = "Rex.Shared.GameObjects.ByRefEventAttribute";

    private static readonly DiagnosticDescriptor s_byRefEventSubscribedByValueRule = new(
        IdByRefEventSubscribedByValue,
        "By-ref event subscribed to by value",
        "Tried to subscribe to a by-ref event '{0}' by value",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure that methods subscribing to a ref event have the ref keyword for the event argument."
    );

    public static readonly DiagnosticDescriptor ByRefEventRaisedByValueRule = new(
        IdByRefEventRaisedByValue,
        "By-ref event raised by value",
        "Tried to raise a by-ref event '{0}' by value",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to use the ref keyword when raising ref events."
    );

    public static readonly DiagnosticDescriptor ByValueEventRaisedByRefRule = new(
        IdValueEventRaisedByRef,
        "Value event raised by-ref",
        "Tried to raise a value event '{0}' by-ref",
        "Usage",
        DiagnosticSeverity.Error,
        true,
        "Make sure to not use the ref keyword when raising value events."
    );

    public override ImmutableArray<DiagnosticDescriptor> SupportedDiagnostics =>
    [
        s_byRefEventSubscribedByValueRule, ByRefEventRaisedByValueRule, ByValueEventRaisedByRefRule
    ];

    public override void Initialize(AnalysisContext context)
    {
        context.ConfigureGeneratedCodeAnalysis(GeneratedCodeAnalysisFlags.Analyze |
                                               GeneratedCodeAnalysisFlags.ReportDiagnostics);
        context.EnableConcurrentExecution();
        context.RegisterCompilationStartAction(compilationContext =>
        {
            IEnumerable<IMethodSymbol>? raiseMethods = compilationContext.Compilation
                .GetTypeByMetadataName("Rex.Shared.GameObjects.EntitySystem")?
                .GetMembers()
                .Where(m => m.Name.Contains("RaiseLocalEvent") && m.Kind == SymbolKind.Method)
                .Cast<IMethodSymbol>();

            IEnumerable<IMethodSymbol>? busRaiseMethods = compilationContext.Compilation
                .GetTypeByMetadataName("Rex.Shared.GameObjects.EntityEventBus")?
                .GetMembers()
                .Where(m => m.Name.Contains("RaiseLocalEvent") && m.Kind == SymbolKind.Method)
                .Cast<IMethodSymbol>();

            if (raiseMethods == null)
            {
                return;
            }

            if (busRaiseMethods != null)
            {
                raiseMethods = raiseMethods.Concat(busRaiseMethods);
            }

            IMethodSymbol[] raiseMethodsArray = raiseMethods.ToArray();

            compilationContext.RegisterOperationAction(
                ctx => CheckEventRaise(ctx, raiseMethodsArray),
                OperationKind.Invocation);
        });
    }

    private static void CheckEventRaise(
        OperationAnalysisContext context,
        IReadOnlyCollection<IMethodSymbol> raiseMethods)
    {
        if (context.Operation is not IInvocationOperation operation)
        {
            return;
        }

        if (!operation.TargetMethod.Name.Contains("RaiseLocalEvent"))
        {
            return;
        }

        if (!raiseMethods.Any(m => m.Equals(operation.TargetMethod.OriginalDefinition, Default)))
        {
            // If you try to do this normally by concatenating like busRaiseMethods above
            // the analyzer does not run without any errors
            // I don't know man
            const string DirectedBusMethod = "Rex.Shared.GameObjects.IDirectedEventBus.RaiseLocalEvent";
            if (!operation.TargetMethod.ToString().StartsWith(DirectedBusMethod))
            {
                return;
            }
        }

        ImmutableArray<IArgumentOperation> arguments = operation.Arguments;
        IArgumentOperation eventArgument;
        switch (arguments.Length)
        {
            case 1:
                eventArgument = arguments[0];
                break;
            case 2:
            case 3:
                eventArgument = arguments[1];
                break;
            default:
                return;
        }

        IParameterSymbol? eventParameter = eventArgument.Parameter;
        // TODO have a way to check generic type parameters
        if (eventParameter == null ||
            eventParameter.Type.SpecialType == SpecialType.System_Object ||
            eventParameter.Type.TypeKind == TypeKind.TypeParameter)
        {
            return;
        }

        INamedTypeSymbol? byRefAttribute = context.Compilation.GetTypeByMetadataName(ByRefAttribute);
        if (byRefAttribute == null)
        {
            return;
        }

        bool isByRefEventType = eventParameter.Type
            .GetAttributes()
            .Any(attribute => attribute.AttributeClass?.Equals(byRefAttribute, Default) ?? false);

        bool parameterIsRef = eventParameter.RefKind == RefKind.Ref;

        if (isByRefEventType != parameterIsRef)
        {
            DiagnosticDescriptor descriptor =
                isByRefEventType ? ByRefEventRaisedByValueRule : ByValueEventRaisedByRefRule;
            var diagnostic = Diagnostic.Create(descriptor, eventArgument.Syntax.GetLocation(), eventParameter.Type);
            context.ReportDiagnostic(diagnostic);
        }
    }
}
