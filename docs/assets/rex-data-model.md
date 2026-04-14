# Rex Data Model

This document defines the `RDM` package format for authored 3D model assets in `RexEngine`.

## Design Goals

- Editing an `RDM` package must be possible without dedicated editor tooling.
- Metadata must stay deterministic, diffable and source-control friendly.
- Source asset metadata should support fast binary loading as well as readable text editing.
- One package should describe advanced 3D data: states, materials, animation clips, morph targets, attachments, collision and reusable local prototypes.
- Runtime loading rules should be explicit and deterministic.
- Authored model data must stay separate from ECS runtime storage.

## Package Layout

An `RDM` asset is a directory rooted under `Resources/Models` that contains one metadata file plus the authored source payloads it references.

Example:

```text
crate/
  crate.rdm
  crate.rdmb
  sources/
    crate_master.fbx
  animations/
    crate_open.gltf
  collision/
    crate_collision.gltf
  textures/
    crate_albedo.png
```

- `*.rdm` is the canonical human-editable metadata file.
- `*.rdmb` is the optional binary JSON companion for faster runtime reads.
- Source payloads may use multiple authored formats. Version 1 supports: `fbx`, `gltf`, `glb`, `obj`, `blend`, `usd`, `usda`, `usdc`, `usdz`.
- The current shared reference package is `Resources/Models/MannyRef/manny.rdm`.

## Root Metadata

The root document contains:

| Key | Meaning |
| --- | --- |
| `version` | Required. Current supported value is `1`. |
| `license` | Required. SPDX license identifier for the package. |
| `copyright` | Required. Copyright and attribution text. |
| `size` | Required. Authored axis-aligned bounds in meters. |
| `sources` | Required. Declared source assets with kind, format, path and import metadata. |
| `materials` | Optional. Material definitions used by states. |
| `states` | Required. Named 3D states. |
| `prototypes` | Optional. Reusable local package prototypes. |
| `load` | Optional. Runtime import hints. |

## Sources

Each source entry contains:

| Key | Meaning |
| --- | --- |
| `id` | Required. Unique source identifier. |
| `kind` | Required. One of `model`, `animation`, `collision`, `skeleton`, `material`. |
| `format` | Required. Source asset format. `fbx` is the primary expected interchange format. |
| `path` | Required. Relative path to the source asset. |
| `entry` | Optional. Scene/object/clip entry within the source file. |
| `scale` | Optional. Import scale. Defaults to `1.0`. |
| `upAxis` | Optional. Defaults to `y`. |
| `forwardAxis` | Optional. Defaults to `z`. |
| `metadata` | Optional. Free-form source metadata. |

States, LODs, animations, skeletons and collision data reference source ids instead of hard-coding direct file paths.

## States

A state is a named authored presentation of the model, such as `closed`, `open`, `broken`, `folded`, or `deployed`.

Each state supports:

| Key | Meaning |
| --- | --- |
| `name` | Required. Lowercase ASCII letters, digits, `_` and `-` only. |
| `source` | Required. Source id for the primary model scene. |
| `skeletonSource` | Optional. Source id for the shared skeleton. |
| `flags` | Optional. String metadata for renderer or gameplay extensions. |
| `lods` | Optional. Additional state sources ordered by descending `screenCoverage`. |
| `animations` | Optional. Named animation clips for the state. |
| `attachments` | Optional. Named attachment points resolved by node name or bone. |
| `materials` | Optional. Per-slot material bindings. |
| `morphTargets` | Optional. Morph target metadata. |
| `collision` | Optional. Collision source metadata. |

## Materials

Material definitions support shader selection, domain flags, texture references and parameter maps.

Typical use cases:

- Surface overrides per state.
- Shared physically based materials.
- Masked or transparent model variants.

## Animations

Animation definitions support:

- Source id references.
- Optional clip names.
- Looping defaults.
- Root motion flags.
- Timed animation events with payload metadata.

## Prototypes

`RDM` packages may contain local reusable prototypes for multiple authoring concerns:

- `render`
- `physics`
- `interaction`
- `animationSet`
- `materialSet`
- `attachmentSet`

Each local prototype can declare:

- a `name`
- a `kind`
- an optional `inherits` parent
- an optional `defaultState`
- included `states`
- included `animations`
- included `materials`
- free-form `tags`

These package-local prototypes are meant to be consumed by higher-level content/runtime layers without forcing one global prototype structure into the asset format itself.

## Binary JSON

`*.rdmb` stores the same metadata as `*.rdm`, but in a compact binary JSON-style encoding.

This is intended for:

- faster reads
- smaller metadata payloads
- preserving deterministic structured metadata without reparsing text at runtime

The text JSON file remains the source-authoring format. The binary JSON file is the optimized runtime companion.

## Engine Prototype Bridge

Top-level engine `model` prototypes can point at an `RDM` package and select one package-local prototype plus an optional default state override.

This keeps:

- engine-wide prototype ids in `Resources/Prototypes`
- package-local authored model metadata in `Resources/Models`
- runtime ECS storage separate from both

## Boundaries

`RDM` is authored asset metadata. It does not define renderer internals, importer implementation details, or ECS runtime behavior by itself.
