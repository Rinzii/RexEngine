; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

| Rule ID | Category | Severity | Notes                                |
|---------|----------|----------|--------------------------------------|
| RA0000  | Usage    | Warning  | ExplicitInterface                    |
| RA0001  | Usage    | Warning  | Serializable                         |
| RA0002  | Usage    | Error    | Access                               |
| RA0003  | Usage    | Warning  | ExplicitVirtual                      |
| RA0004  | Usage    | Error    | TaskResult                           |
| RA0005  | Usage    | Warning  | UseGenericVariant                    |
| RA0006  | Usage    | Error    | UseGenericVariantInvalidUsage        |
| RA0007  | Usage    | Error    | UseGenericVariantAttributeValueError |
| RA0008  | Usage    | Error    | ByRefEventSubscribedByValue          |
| RA0009  | Usage    | Error    | ByRefEventRaisedByValue              |
| RA0010  | Usage    | Error    | ValueEventRaisedByRef                |
| RA0011  | Usage    | Error    | DataDefinitionPartial                |
| RA0012  | Usage    | Error    | NestedDataDefinitionPartial          |
| RA0013  | Usage    | Error    | DataFieldWritable                    |
| RA0014  | Usage    | Error    | DataFieldPropertyWritable            |
| RA0015  | Usage    | Warning  | DependencyFieldAssigned              |
| RA0016  | Usage    | Warning  | UncachedRegex                        |
| RA0017  | Usage    | Info     | DataFieldRedundantTag                |
| RA0018  | Usage    | Warning  | MustCallBase                         |
| RA0019  | Usage    | Info     | DataFieldNoVVReadWrite               |
| RA0020  | Usage    | Warning  | UseNonGenericVariant                 |
| RA0021  | Usage    | Error    | PreferOtherType                      |
| RA0022  | Usage    | Warning  | DuplicateDependency                  |
| RA0023  | Usage    | Warning  | ForbidLiteral                        |
| RA0024  | Usage    | Warning  | ObsoleteInheritance                  |
| RA0025  | Usage    | Warning  | ObsoleteInheritanceWithMessage       |
| RA0026  | Usage    | Error    | DataFieldYamlSerializable            |
| RA0027  | Usage    | Warning  | PrototypeNetSerializable             |
| RA0028  | Usage    | Warning  | PrototypeSerializable                |
| RA0029  | Usage    | Warning  | PrototypeInstantiation               |
| RA0030  | Usage    | Warning  | PrototypeRedundantType               |
| RA0031  | Usage    | Error    | PrototypeEndsWithPrototype           |
| RA0032  | Usage    | Error    | ValidateMember                       |
| RA0033  | Usage    | Warning  | PreferProxy                          |
| RA0034  | Usage    | Warning  | ProxyForRedundantMethodName          |
| RA0035  | Usage    | Error    | ProxyForTargetMethodNotFound         |
