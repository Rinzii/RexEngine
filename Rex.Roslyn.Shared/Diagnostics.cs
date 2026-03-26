using Microsoft.CodeAnalysis;

namespace Rex.Roslyn.Shared;

public static class Diagnostics
{
    public const string IdExplicitInterface = "RA0000";
    public const string IdSerializable = "RA0001";
    public const string IdAccess = "RA0002";
    public const string IdExplicitVirtual = "RA0003";
    public const string IdTaskResult = "RA0004";
    public const string IdUseGenericVariant = "RA0005";
    public const string IdUseGenericVariantInvalidUsage = "RA0006";
    public const string IdUseGenericVariantAttributeValueError = "RA0007";
    public const string IdByRefEventSubscribedByValue = "RA0008";
    public const string IdByRefEventRaisedByValue = "RA0009";
    public const string IdValueEventRaisedByRef = "RA0010";
    public const string IdDataDefinitionPartial = "RA0011";
    public const string IdNestedDataDefinitionPartial = "RA0012";
    public const string IdDataFieldWritable = "RA0013";
    public const string IdDataFieldPropertyWritable = "RA0014";
    public const string IdDependencyFieldAssigned = "RA0015";
    public const string IdUncachedRegex = "RA0016";
    public const string IdDataFieldRedundantTag = "RA0017";
    public const string IdMustCallBase = "RA0018";
    public const string IdDataFieldNoVVReadWrite = "RA0019";
    public const string IdUseNonGenericVariant = "RA0020";
    public const string IdPreferOtherType = "RA0021";
    public const string IdDuplicateDependency = "RA0022";
    public const string IdForbidLiteral = "RA0023";
    public const string IdObsoleteInheritance = "RA0024";
    public const string IdObsoleteInheritanceWithMessage = "RA0025";
    public const string IdDataFieldYamlSerializable = "RA0026";
    public const string IdPrototypeNetSerializable = "RA0027";
    public const string IdPrototypeSerializable = "RA0028";
    public const string IdPrototypeInstantiation = "RA0029";
    public const string IdPrototypeRedundantType = "RA0030";
    public const string IdPrototypeEndsWithPrototype = "RA0031";
    public const string IdValidateMember = "RA0032";
    public const string IdPreferProxy = "RA0033";
    public const string IdProxyForRedundantMethodName = "RA0034";
    public const string IdProxyForTargetMethodNotFound = "RA0035";

    public static SuppressionDescriptor MeansImplicitAssignment =>
        new SuppressionDescriptor("RADC1000", "CS0649", "Marked as implicitly assigned.");
}
