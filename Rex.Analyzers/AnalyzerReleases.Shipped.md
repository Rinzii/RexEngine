; Shipped analyzer releases
; https://github.com/dotnet/roslyn/blob/main/src/RoslynAnalyzers/Microsoft.CodeAnalysis.Analyzers/ReleaseTrackingAnalyzers.Help.md

## Release 1.0

### New Rules

| Rule ID | Category | Severity | Notes |
|--------|----------|----------|--------------------|
| RA0000 | Usage | Warning | ExplicitInterface |
| RA0001 | Usage | Warning | Serializable |
| RA0002 | Usage | Error | Access |
| RA0003 | Usage | Warning | ExplicitVirtual |
| RA0004 | Usage | Error | TaskResult |
| RA0005 | Usage | Warning | UseGenericVariant |
| RA0006 | Usage | Error | UseGenericVariantInvalidUsage |
| RA0007 | Usage | Error | UseGenericVariantAttributeValueError |
| RA0013 | Usage | Error | ByRefEventSubscribedByValue |
| RA0015 | Usage | Error | ByRefEventRaisedByValue |
| RA0016 | Usage | Error | ValueEventRaisedByRef |
| RA0017 | Usage | Error | DataDefinitionPartial |
| RA0018 | Usage | Error | NestedDataDefinitionPartial |
| RA0019 | Usage | Error | DataFieldWritable |
| RA0020 | Usage | Error | DataFieldPropertyWritable |
| RA0025 | Usage | Warning | DependencyFieldAssigned |
| RA0026 | Usage | Warning | UncachedRegex |
| RA0027 | Usage | Info | DataFieldRedundantTag |
| RA0028 | Usage | Warning | MustCallBase |
| RA0029 | Usage | Info | DataFieldNoVVReadWrite |
| RA0030 | Usage | Warning | UseNonGenericVariant |
| RA0031 | Usage | Error | PreferOtherType |
| RA0032 | Usage | Warning | DuplicateDependency |
| RA0033 | Usage | Warning | ForbidLiteral |
| RA0034 | Usage | Warning | ObsoleteInheritance |
| RA0035 | Usage | Warning | ObsoleteInheritanceWithMessage |
| RA0036 | Usage | Error | DataFieldYamlSerializable |
| RA0037 | Usage | Warning | PrototypeNetSerializable |
| RA0038 | Usage | Warning | PrototypeSerializable |
| RA0039 | Usage | Warning | PrototypeInstantiation |
| RA0042 | Usage | Warning | PrototypeRedundantType |
| RA0043 | Usage | Error | PrototypeEndsWithPrototype |
| RA0044 | Usage | Error | ValidateMember |
| RA0045 | Usage | Warning | PreferProxy |
| RA0046 | Usage | Warning | ProxyForRedundantMethodName |
| RA0047 | Usage | Error | ProxyForTargetMethodNotFound |
