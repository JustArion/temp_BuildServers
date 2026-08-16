using Fallout.Common;
using Serilog;
using Tools;

namespace Components;

/// <summary>
/// The OS matrix shared by every program component.
/// <para>
/// This is the piece that replaces "one nested build server per operating system". The graph stays
/// flat and lives on a single build instance; the fan-out happens <i>inside</i> a target, over plain
/// objects that are free to hold state.
/// </para>
/// </summary>
public interface IMatrixBuild : IFalloutBuild
{
    [Parameter("Operating systems to build for, e.g. --target-os windows linux. Defaults to linux plus the host OS.")]
    OperatingSystem[] TargetOs => TryGetValue(() => TargetOs) ?? DefaultTargetOs;

    [Parameter("Allow the build to install missing toolchains (and a package manager) on this machine.")]
    bool InstallPrerequisites => TryGetValue<bool?>(() => InstallPrerequisites) ?? false;

    /// <summary>
    /// Linux is always in reach because it builds in a container, and the host OS is the only other
    /// one we can produce natively.
    /// </summary>
    sealed OperatingSystem[] DefaultTargetOs =>
        new[] { OperatingSystem.Linux, PrerequisiteManager.HostOperatingSystem }.Distinct().ToArray();

    /// <summary>
    /// The platforms of <paramref name="program"/> that the requested matrix asks for and the host
    /// can actually build. Pass <paramref name="reportSkips"/> once per run — the restore phase does —
    /// so requested-but-unavailable platforms are named instead of silently disappearing.
    /// </summary>
    sealed IReadOnlyList<IPlatformBuilder> SelectedPlatforms(IProgramBuilder program, bool reportSkips = false)
    {
        var requested = program.Platforms.Where(x => TargetOs.Contains(x.Target)).ToList();

        if (reportSkips)
        {
            foreach (var unavailable in requested.Where(x => !x.IsAvailable))
                Log.Warning("Skipping {Platform}: {Reason}", unavailable.Name, unavailable.UnavailableReason ?? "unavailable on this host");

            if (requested.All(x => !x.IsAvailable))
                Log.Warning("{Program}: nothing to do — no platform in --target-os {Matrix} is buildable here",
                    program.Name, string.Join(", ", TargetOs));
        }

        return requested.Where(x => x.IsAvailable).ToList();
    }

    /// <summary>Runs one phase of <paramref name="program"/> across every selected platform.</summary>
    sealed Task Fanout(IProgramBuilder program, string phase, Func<IPlatformBuilder, Task> action)
        => Fanout(program, phase, SelectedPlatforms(program), action);

    /// <summary>Runs one phase across an already-selected set, so selection happens once per target.</summary>
    sealed async Task Fanout(
        IProgramBuilder program,
        string phase,
        IReadOnlyList<IPlatformBuilder> selected,
        Func<IPlatformBuilder, Task> action)
    {
        if (selected.Count == 0)
            return;

        foreach (var platform in selected)
        {
            Log.Information("── {Phase}: {Platform} ──", phase, platform.Name);
            await action(platform);
        }
    }
}
