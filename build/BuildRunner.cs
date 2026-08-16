using Components;
using Fallout.Common;
using Servers.MPV;
using Servers.Seanime;

/// <summary>
/// The one and only <c>FalloutBuild</c> instance.
///
/// <para>
/// Fallout resolves target dependencies against a single build instance, so every target has to be
/// reachable from this type. Rather than nesting build servers (which cannot work today) or
/// daisy-chaining partial classes (which cannot hold two members of the same name), the graph is
/// composed out of build components and the per-program state lives in ordinary fields below.
/// </para>
///
/// <code>
/// ./build.ps1 Seanime --target-os windows linux   # one program, several operating systems
/// ./build.ps1 All --target-os windows             # every program, one operating system
/// ./build.ps1 Seanime_Compile                     # a single phase, matrix included
/// ./build.ps1 --plan                              # see the whole graph
/// </code>
/// </summary>
class BuildRunner : FalloutBuild, ISeanimeBuild, IMpvBuild
{
    public static int Main() => Execute<BuildRunner>(x => x.Build);

    public BuildRunner() => NoLogo = true;

    // Interfaces cannot declare fields, but they can declare a property the build class backs with
    // one. That is where the state the components need actually lives.
    public SeanimeBuilder SeanimeServer { get; } = new();

    public MpvBuilder MpvServer { get; } = new();

    public Target Clean => _ => _
        .Description("Clean every program for every OS in --target-os")
        .DependsOn<ISeanimeBuild>(x => x.Seanime_Clean)
        .DependsOn<IMpvBuild>(x => x.Mpv_Clean);

    public Target All => _ => _
        .Description("Build every program for every OS in --target-os")
        .DependsOn<ISeanimeBuild>(x => x.Seanime)
        .DependsOn<IMpvBuild>(x => x.Mpv);

    public Target Build => _ => _
        .Description("Default target. Same as All.")
        .DependsOn(All);
}
