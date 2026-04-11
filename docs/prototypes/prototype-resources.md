# Prototypes and resources

The initial prototype/resource layer stays aligned with the current Rex shared runtime and keeps authored resource data separate from ECS runtime storage.

## Resource layout

- Shared runtime resources live under `RexEngine/Resources`.
- Prototype files live under `RexEngine/Resources/Prototypes`.
- 3D model resources live under `RexEngine/Resources/Models`.
- Map resources live under `RexEngine/Resources/Maps`.
- Scene sidecar resources live under `RexEngine/Resources/Scenes`.
- `RexEngine/Resources/TestingSamples` is reserved for loose sample assets and development-only reference inputs.
- Sandbox-only authored sample content now lives under `RexEngine/Resources/TestingSamples/Sandbox` so the shared runtime does not auto-load it.

## Prototype format

- Files currently use `*.prototype.json`.
- Binary companion files may also use `*.prototype.bjson`.
- Each file contains a top-level array of prototype mappings.
- Each mapping must include both `type` and `id`.
- `parent` is optional and supports one parent id.
- `parents` is optional and supplies an ordered list of extra parent ids when several ancestors apply.
- When both `parent` and `parents` are present, `parent` composes first and `parents` follow in declared order.

## Entity prototypes

- `EntityPrototype` uses the prototype type name `entity`.
- Component payloads are keyed by registered ECS component names.
- Prototype inheritance composes parent and child mappings before deserialization, so child component fields can override parent fields without replacing the entire component block.
- Public prototype enumeration skips abstract parents, but abstract prototypes still remain available internally for inheritance composition.

## Model prototypes

- `ModelPrototype` uses the prototype type name `model`.
- Model prototypes reference one authored `RDM` package under `Resources/Models`.
- `rdm` stores the stable resource path to the package metadata file, for example `Models/MannyRef/manny.rdm`.
- `packagePrototype` optionally selects one package-local `RDM` prototype.
- `defaultState` optionally overrides the package-local default state.
- The shared `ModelPrototypeResolver` bridges top-level model prototypes to loaded `RDM` packages and package-local prototype metadata.

## Map prototypes

- `MapPrototype` uses the prototype type name `map`.
- Map prototypes reference one shared map resource under `Resources/Maps`.
- `scene` optionally points at a `ScenePrototype` that should accompany the map.

## Scene prototypes

- `ScenePrototype` uses the prototype type name `scene`.
- Scene prototypes can reference one `MapPrototype`.
- Scene prototypes can also declare authored entity placements using entity prototype ids and 3D transforms.

## Prototype categories

- `PrototypeCategoryPrototype` uses the prototype type name `prototypeCategory`.
- Categories group authored prototype ids under one declared prototype kind.
- The shared runtime validates that category kinds are registered prototype types.

## Typed ids and reload

- The shared runtime exposes strongly typed prototype ids for entity, model, map, scene and category kinds.
- `PrototypeManager` now raises a reload event with a monotonic version whenever a prototype set is loaded or reloaded.
- `ReloadResources` clears old definitions before reloading the global prototype tree.

## Resource catalogs

- `RdmCatalog` indexes `*.rdm` and `*.rdmb` files under `Resources/Models`.
- Catalog loading validates that referenced source and texture files actually exist beside the authored metadata.
- The current shared reference asset is `Resources/Models/MannyRef/manny.rdm`, with a paired top-level model prototype in `Resources/Prototypes/base/models.prototype.json`.
- `TestingSamples` is not auto-loaded by `PrototypeManager` or `RdmCatalog`; it is intentionally opt-in so scratch assets and sandbox sample content do not silently become runtime content.

## Current limits

- This pass is JSON-backed rather than YAML-backed.
- Core ECS components can be spawned from prototypes because they are now data definitions as well as protobuf payloads.
- Prototype hot-reload and content-assembly scanning are future work.
- Audio and broader gameplay/content prototype kinds are still future work once the relevant shared runtime surfaces exist.
