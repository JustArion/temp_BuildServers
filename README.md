# temp_BuildServers — flat-graph alternative

A fork of [JustArion/temp_BuildServers](https://github.com/JustArion/temp_BuildServers), restructured
to answer [Fallout-build/Fallout#626](https://github.com/orgs/Fallout-build/discussions/626): building
several open-source programs for several operating systems from one Fallout build.

```bash
./build.ps1 Seanime --target-os windows linux   # one program, several operating systems
./build.ps1 All --target-os windows             # every program, one operating system
./build.ps1 Seanime_Compile                     # a single phase, matrix included
./build.ps1 --help                              # the whole graph
```

## Why the original could not work

Fallout builds the execution graph from **one** build instance:

```csharp
// Fallout/src/Fallout.Build/Execution/ExecutableTargetFactory.cs
var targetProperties = GetTargetProperties(build.GetType()).ToList();   // the root type only
...
yield return executables.Single(x => x.Factory == factoryDependency);   // Sequence contains no matching element
```

Targets on a nested `FalloutBuild` never become `ExecutableTarget`s, so `DependsOn(LinuxBuildServer.Build)`
has nothing to resolve against and the `Single` throws. Nesting is not a usage mistake — it is
outside what the graph can currently express.

## What this fork does instead

**One `FalloutBuild`. Everything else is a plain object.**

The nested classes were never defining graphs, they were doing work. Once they stop deriving from
`FalloutBuild` they become ordinary classes — which means they can hold ordinary fields, and the
state problem raised in the discussion disappears.

```
BuildRunner : FalloutBuild, ISeanimeBuild, IMpvBuild   the only build instance; owns the state
├── Components/IMatrixBuild.cs      --target-os, platform selection, fan-out helper
├── Components/ISeanimeBuild.cs     the Seanime targets
├── Components/IMpvBuild.cs         the MPV targets
└── Servers/
    ├── Seanime/SeanimeBuilder.cs           IProgramBuilder — owns the platform builders
    │   └── Platforms/SeanimeLinuxBuilder.cs    IPlatformBuilder — Docker, holds the container
    │       Platforms/SeanimeNativeBuilder.cs   IPlatformBuilder — host toolchain
    └── MPV/MpvBuilder.cs                   placeholder, to show adding a program
```

### State lives in fields, reached through the component

Interfaces cannot declare fields, but they can declare a property the build class backs with one:

```csharp
public interface ISeanimeBuild : IMatrixBuild
{
    SeanimeBuilder SeanimeServer { get; }              // no state here

    Target Seanime_Compile => _ => _
        .DependsOn(Seanime_Restore)
        .Executes(() => Fanout(SeanimeServer, "Compile", x => x.CompileAsync()));
}

class BuildRunner : FalloutBuild, ISeanimeBuild
{
    public SeanimeBuilder SeanimeServer { get; } = new();   // the state actually lives here
}
```

No partial classes, no daisy-chaining, and two programs can both have a "restore" because the
*builders* carry the name, not the members.

### The OS matrix is a loop inside a target, not a build per OS

```csharp
[Parameter] OperatingSystem[] TargetOs => TryGetValue(() => TargetOs) ?? DefaultTargetOs;
```

`IMatrixBuild.SelectedPlatforms` intersects what was requested with what the host can build, and
`Fanout` runs one phase across the result. Requested-but-unbuildable platforms are named, not
silently dropped:

```
[WRN] Skipping Seanime/Linux: docker is not installed
[INF] ── Restore: Seanime/Windows ──
```

Linux always builds in a container, so any host with Docker can produce it. The host OS builds
natively. Asking for macOS from Windows reports that it cannot be done rather than failing obscurely.

## What this does not give you

Worth being clear about, because these are the things a nested-target feature *would* fix:

- **Target names are prefixed by hand.** `Seanime_Compile`, `Mpv_Compile` — one flat namespace, so
  every program has to disambiguate itself. `ISeanimeBuild` and `IMpvBuild` are structurally
  identical files as a result.
- **The matrix runs sequentially.** One process, one graph, one target at a time. Real per-OS
  parallelism needs concurrent build instances, which is the larger architecture work.
- **`--plan` shows phases, not the matrix.** The fan-out happens inside a target, so the OS
  dimension is invisible to the execution plan and cannot be skipped or partitioned per OS.

## Changes to the original beyond the restructure

- `OperatingSystem` was `[Flags]` with values `0, 1, 2`, so `HasFlag(Windows)` was always true and
  `MacOS | Linux` collided with a third member. Now powers of two.
- `GoTasks.Go`, `NodeTasks.Node` and `NpmTasks.Npm` installed missing toolchains on every
  invocation — including a bare version probe, which meant running the build could install Scoop
  and Go without being asked. Presence is now asserted up front by
  `PrerequisiteManager.RequireTool`, and installing anything requires `--install-prerequisites`.
- `Seanime.Native.cs` had `#define SKIP_NATIVE` hardcoded at the top, so native builds never ran.
  It is a build constant now (`-p:DefineConstants=SKIP_NATIVE`).
- `Seanime_Linux` declared `IsAvailable` twice when `SKIP_LINUX` was defined.
- `SeanimeServer.Build_Native` depended on `LinuxBuildServer.Build`.
- The clone-once optimisation applies to any number of platforms rather than exactly two, and
  `Seanime_Teardown` stops the build container even when an earlier phase threw.
