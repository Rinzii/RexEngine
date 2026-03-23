# Rex

Rex is a Rider-first hybrid starter for a C# application with a native C++ backend.

## Layout

- `Rex.sln` is the main entry point
- `src/Client/Rex.Client` is the client host
- `src/Server/Rex.Server` is the server host
- `src/Shared/Rex.Common` is shared managed code
- `src/Shared/Rex.Native` is the managed interop layer
- `src/Native/Rex.Native.Code` exposes native source files in the solution for editing
- `native/Rex.Native` contains the real CMake native project
- `build/Rex.Native.Build` invokes CMake from the solution build
- `src/Tools/Rex.PInvoke.Tool` generates `LibraryImport` bindings from the C header

## Build

Open `Rex.sln` in Rider and build `Rex.Client` or `Rex.Server`.

The build will:

1. build `Rex.Native.Build`
2. configure CMake
3. build the native library
4. run the native smoke test
5. copy the native runtime into the managed output folder

## PInvoke workflow

Edit native exports in `native/Rex.Native/include/rex/rex_native.h`.

Then build the solution. `Rex.Native` regenerates `Generated/Methods.g.cs` before compile by running `Rex.PInvoke.Tool`.

Keep helper code in `src/Shared/Rex.Native/Methods.cs`.
Keep raw imports in generated code only.
