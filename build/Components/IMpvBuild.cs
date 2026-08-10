using Fallout.Common;
using Servers.MPV;

namespace Components;

/// <summary>
/// The MPV build graph. Structurally identical to <see cref="ISeanimeBuild"/> — that repetition is
/// the honest cost of today's flat graph, and the thing a nested-target feature would remove.
/// </summary>
public interface IMpvBuild : IMatrixBuild
{
    MpvBuilder MpvServer { get; }

    Target Mpv_Clean => _ => _
        .Description("Wipe MPV artifacts and output for every targeted OS")
        .Before(Mpv_Restore)
        .Executes(() => Fanout(MpvServer, "Clean", x => x.CleanAsync()));

    Target Mpv_Restore => _ => _
        .Description("Fetch MPV sources and prepare toolchains for every targeted OS")
        .Executes(async () =>
        {
            var selected = SelectedPlatforms(MpvServer, reportSkips: true);
            MpvServer.Options.InstallMissingTools = InstallPrerequisites;
            await MpvServer.PrepareAsync(selected);
            await Fanout(MpvServer, "Restore", selected, x => x.RestoreAsync());
        });

    Target Mpv_Compile => _ => _
        .Description("Build MPV for every targeted OS")
        .DependsOn(Mpv_Restore)
        .Executes(() => Fanout(MpvServer, "Compile", x => x.CompileAsync()));

    Target Mpv_Package => _ => _
        .Description("Build the MPV installer for every targeted OS")
        .DependsOn(Mpv_Compile)
        .Executes(() => Fanout(MpvServer, "Package", x => x.PackageAsync()));

    Target Mpv => _ => _
        .Description("Build MPV for every OS in --target-os")
        .DependsOn(Mpv_Package)
        .Executes(() => Fanout(MpvServer, "Collect", x => x.CollectAsync()));
}
