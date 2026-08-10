using System.IO;
using Extensions;
using Fallout.Common.IO;
using Serilog;
using Servers.Seanime.Platforms;
using Tools;
using Paths = Tools.Paths;

namespace Servers.Seanime;

/// <summary>
/// Seanime across every operating system we can produce.
/// <para>
/// A plain class, so it can hold the per-platform builders in ordinary fields. The Fallout target
/// graph for Seanime lives in <c>ISeanimeBuild</c> and calls into this.
/// </para>
/// Linux is built in a container (any host with Docker), the host OS is built natively.
/// Repository: https://github.com/5rahim/seanime
/// </summary>
public sealed class SeanimeBuilder : IProgramBuilder
{
    public const string ProgramName = "Seanime";
    static readonly Uri DefaultRepository = new("https://github.com/5rahim/seanime");

    /// <summary>Sources cloned once here when more than one platform needs them.</summary>
    static AbsolutePath SharedSourceDirectory => Paths.ArtifactsDirectoryFor(ProgramName) / "_shared";

    readonly SeanimeLinuxBuilder linux = new();
    readonly SeanimeNativeBuilder? native;

    public SeanimeBuilder()
    {
        var platforms = new List<IPlatformBuilder> { linux };

        // On a Linux host the container path already covers the host OS, so a second native builder
        // would only duplicate the same target.
        if (PrerequisiteManager.HostOperatingSystem != OperatingSystem.Linux)
        {
            native = new SeanimeNativeBuilder();
            platforms.Add(native);
        }

        Platforms = platforms;
    }

    public string Name => ProgramName;

    public BuildServerOptions Options { get; } = new();

    public IReadOnlyList<IPlatformBuilder> Platforms { get; }

    public Task PrepareAsync(IReadOnlyList<IPlatformBuilder> selected)
    {
        Options.RemoteGitUrl ??= DefaultRepository;

        foreach (var platform in selected)
        {
            platform.Options.RemoteGitUrl ??= Options.RemoteGitUrl;
            platform.Options.LocalFolder ??= Options.LocalFolder;
            platform.Options.InstallMissingTools = Options.InstallMissingTools;
        }

        // Cloning once and handing the same tree to every platform is only worth it when more than
        // one platform is in play and the user has not already pointed us at a local checkout.
        if (selected.Count < 2 || Options.LocalFolder is not null || !PrerequisiteManager.IsToolInstalled("git"))
            return Task.CompletedTask;

        Log.Information("Cloning {Repository} once for {Count} platforms", Options.RemoteGitUrl, selected.Count);

        SharedSourceDirectory.CreateOrCleanDirectory();
        Git($"clone {Options.RemoteGitUrl}", SharedSourceDirectory, logOutput: false);

        var shared = new DirectoryInfo(SharedSourceDirectory);
        foreach (var platform in selected)
            platform.Options.LocalFolder = shared;

        return Task.CompletedTask;
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var platform in Platforms)
            await platform.DisposeAsync();
    }
}
