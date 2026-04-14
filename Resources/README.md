# Rex Resources

Shared engine-authored resources live under this global directory.

- `Prototypes` contains prototype definitions.
- `Models` contains 3D asset payloads such as `RDM` packages.
- `Materials` contains shared material assets and authored material payloads.
- `Textures` contains standalone shared textures.
- `Shaders` contains shader sources and related authored shader payloads.
- `Audio` contains shared audio content.
- `Fonts` contains shared font assets.
- `Localization` contains shared localization resources.
- `Maps` contains authored maps and scene-like resource payloads.
- `Scenes` contains scene-specific authored resource payloads when a scene needs sidecar data beyond prototypes.
- `UI` contains shared UI assets and layouts.
- `Vfx` contains shared visual-effects content.
- `Models/MannyRef` is the current shared FBX-backed reference package wired through `RDM` and `model` prototypes.
- `Prototypes` is for shared runtime-authored content that any engine consumer may load by default.
- `TestingSamples` is an opt-in scratch/reference area for sample inputs and sample-authored content that should not be auto-loaded by the shared runtime.
- `TestingSamples/Sandbox/Prototypes` contains the sandbox sample prototypes used only by the sandbox test consumer.
