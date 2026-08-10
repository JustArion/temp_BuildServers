using Fallout.Common;
using Servers.Seanime;

namespace Components;

/// <summary>
/// The Seanime build graph.
/// <para>
/// Every target here is a real node in the execution plan, so <c>--plan</c>, <c>--skip</c> and
/// partial invocations all work. The component holds no state of its own — <see cref="SeanimeServer"/>
/// is backed by an ordinary field on the build class, which is how we get around interfaces not
/// being allowed to declare fields.
/// </para>
/// </summary>
public interface ISeanimeBuild : IMatrixBuild
{
    /// <summary>Backed by a plain field on the build class; that field is where the state lives.</summary>
    SeanimeBuilder SeanimeServer { get; }

    Target Seanime_Clean => _ => _
        .Description("Wipe Seanime artifacts and output for every targeted OS")
        .Before(Seanime_Restore)
        .Executes(() => Fanout(SeanimeServer, "Clean", x => x.CleanAsync()));

    Target Seanime_Restore => _ => _
        .Description("Fetch Seanime sources and prepare toolchains for every targeted OS")
        .Executes(async () =>
        {
            var selected = SelectedPlatforms(SeanimeServer, reportSkips: true);
            SeanimeServer.Options.InstallMissingTools = InstallPrerequisites;
            await SeanimeServer.PrepareAsync(selected);
            await Fanout(SeanimeServer, "Restore", selected, x => x.RestoreAsync());
        });

    Target Seanime_Compile => _ => _
        .Description("Build the Seanime server binary and web UI for every targeted OS")
        .DependsOn(Seanime_Restore)
        .Executes(() => Fanout(SeanimeServer, "Compile", x => x.CompileAsync()));

    Target Seanime_Package => _ => _
        .Description("Build the Seanime installer for every targeted OS")
        .DependsOn(Seanime_Compile)
        .Executes(() => Fanout(SeanimeServer, "Package", x => x.PackageAsync()));

    Target Seanime => _ => _
        .Description("Build Seanime for every OS in --target-os")
        .DependsOn(Seanime_Package)
        .Executes(() => Fanout(SeanimeServer, "Collect", x => x.CollectAsync()));

    /// <summary>Stops the build container even when a phase above it threw.</summary>
    Target Seanime_Teardown => _ => _
        .Unlisted()
        .TriggeredBy(Seanime_Restore)
        .After(Seanime)
        .AssuredAfterFailure()
        .Executes(async () => await SeanimeServer.DisposeAsync());
}
