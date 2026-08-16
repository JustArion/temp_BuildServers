using System.IO;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using Extensions;
using Extensions.Container;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.Git;
using Fallout.Common.Utilities.Collections;
using Serilog;
using Tools;
using Paths = Tools.Paths;

namespace Servers.Seanime.Platforms;

/// <summary>
/// Builds Seanime for Linux inside a container, so it works from any host with Docker.
///
/// Prerequisites:
/// - Docker
///
/// Repository: https://github.com/5rahim/seanime
/// </summary>
public sealed class SeanimeLinuxBuilder : PlatformBuilderBase, IPlatformBuilder
{
    internal readonly AbsolutePath ArtifactsDirectory = Paths.ArtifactsDirectoryFor(SeanimeBuilder.ProgramName) / "Linux";
    internal readonly AbsolutePath OutputDirectory = Paths.OutputDirectoryFor(SeanimeBuilder.ProgramName) / "Linux";

    AbsolutePath RepositoryRoot => ArtifactsDirectory / "seanime";
    AbsolutePath WebDirectory => RepositoryRoot / "seanime-web";
    AbsolutePath DenshiDirectory => RepositoryRoot / "seanime-denshi";
    AbsolutePath BinariesDirectory => DenshiDirectory / "binaries";

    static readonly AbsolutePath DockerArtifactsDirectory = AbsolutePath.Create("/app");
    static readonly AbsolutePath DockerRepositoryRoot = DockerArtifactsDirectory / "seanime";
    static readonly AbsolutePath DockerWebDirectory = DockerRepositoryRoot / "seanime-web";
    static readonly AbsolutePath DockerDenshiDirectory = DockerRepositoryRoot / "seanime-denshi";
    static readonly AbsolutePath DockerBinariesDirectory = DockerDenshiDirectory / "binaries";

    public OperatingSystem Target => OperatingSystem.Linux;

    public string Name => $"{SeanimeBuilder.ProgramName}/Linux";

#if SKIP_LINUX
    public bool IsAvailable => false;
    public string? UnavailableReason => "compiled with SKIP_LINUX";
#else
    public bool IsAvailable => PrerequisiteManager.IsToolInstalled("docker");
    public string? UnavailableReason => IsAvailable ? null : "docker is not installed";
#endif

    IContainer? BuildContainer { get; set; }

    public Task CleanAsync()
    {
        ArtifactsDirectory.CreateOrCleanDirectory();
        OutputDirectory.CreateOrCleanDirectory();
        return Task.CompletedTask;
    }

    public async Task RestoreAsync()
    {
        RestoreDirectories();
        await RestoreContainer();
        await RestoreGit();
        await RestoreNpm();
        Log.Information("✓ Build container ready");
    }

    void RestoreDirectories()
    {
        ArtifactsDirectory.CreateOrCleanDirectory();
        OutputDirectory.CreateOrCleanDirectory();

        if (Options.LocalFolder is not { Exists: true })
            return;

        Log.Information("Using local repository: {Folder}", Options.LocalFolder);
        Options.LocalFolder.CopyTo(new DirectoryInfo(ArtifactsDirectory));
    }

    async Task RestoreContainer()
    {
        // Create and initialize build container with all dependencies
        Log.Information("Setting up build container...");
        BuildContainer = new ContainerBuilder("golang:latest")
            .WithEnvironment("DEBIAN_FRONTEND", "noninteractive")
            .WithBindMount(ArtifactsDirectory, "/app")
            .WithWorkingDirectory("/app")
            .WithCommand("tail", "-f", "/dev/null")
            .Build();

        try
        {
            await BuildContainer.StartAsync();
            Log.Information("Installing dependencies in container...");

            var result = await BuildContainer.BulkExecute(
            [
                "apt-get update > /dev/null",
                "apt-get install -yqq nodejs npm git wget > /dev/null",
                "echo 'Go version:' && go version",
                "echo 'Node version:' && node --version",
                "echo 'npm version:' && npm --version"
            ]);

            result.Output.ForEach(x =>
            {
                if (x.Type == OutputType.Err)
                    Log.Error("{Output}", x.Text);
                else
                    Log.Information("{Output}", x.Text);
            });
        }
        catch (Exception e)
        {
            Log.Error(e, "Failed to initialize build container");
            await BuildContainer.StopAsync();
            throw;
        }
    }

    async Task RestoreGit()
    {
        if (Options.LocalFolder is { Exists: true })
            return;

        try
        {
            if (RepositoryRoot.DirectoryExists())
                await GitTasks.Git(BuildContainer!, "pull", DockerRepositoryRoot);
            else
                await GitTasks.Git(BuildContainer!, $"clone {Options.RemoteGitUrl}", DockerArtifactsDirectory, logOutput: false);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to fetch sources in build container");
            await BuildContainer!.StopAsync();
            throw;
        }
    }

    async Task RestoreNpm()
    {
        try
        {
            // Install dependencies in seanime-denshi
            Log.Information("Running npm ci in seanime-denshi...");
            await NpmTasks.Npm(BuildContainer!, "ci", DockerDenshiDirectory);

            // Install dependencies in seanime-web
            Log.Information("Running npm install in seanime-web");
            await NpmTasks.Install(BuildContainer!, DockerWebDirectory);
        }
        catch (Exception ex)
        {
            Log.Error(ex, "Failed to install npm dependencies");
            await BuildContainer!.StopAsync();
            throw;
        }
    }

    public async Task CompileAsync()
    {
        await BuildServer();
        await BuildUI();
    }

    async Task BuildServer()
    {
        Log.Information("Building Go server within container...");

        Log.Information("Ensuring {Directory} is created and clean", BinariesDirectory);
        BinariesDirectory.CreateOrCleanDirectory();
        var binaryName = CrossPlatform.GetPlatformBinaryName(OperatingSystem.Linux);
        var binaryPath = DockerBinariesDirectory / binaryName;

        await GoTasksEx.Build(BuildContainer!, binaryPath, DockerRepositoryRoot, ldFlags: "-s -w");
        Log.Information("✓ Server binary built from container: {Binary}", binaryPath);
    }

    async Task BuildUI()
    {
        BuildContainer.NotNull("Build container not initialized. Ensure Restore ran.");

        Log.Information("Building UI from within container...");

        // Build web UI
        Log.Information("Building web UI...");
        await NpmTasks.RunScript(BuildContainer!, "build", DockerWebDirectory);
        await NpmTasks.RunScript(BuildContainer!, "build:denshi", DockerWebDirectory);

        // Move outputs
        var webOutput = WebDirectory / "out";
        var webDenshiOutput = WebDirectory / "out-denshi";
        var targetWebDir = RepositoryRoot / "web";
        var targetDenshiWebDir = DenshiDirectory / "web-denshi";

        if (webOutput.DirectoryExists())
        {
            Log.Information("Moving web output...");
            if (targetWebDir.DirectoryExists())
                Directory.Delete(targetWebDir, true);
            webOutput.Move(targetWebDir);
        }

        if (webDenshiOutput.DirectoryExists())
        {
            Log.Information("Moving web-denshi output...");
            if (targetDenshiWebDir.DirectoryExists())
                Directory.Delete(targetDenshiWebDir, true);
            webDenshiOutput.Move(targetDenshiWebDir);
        }

        Log.Information("✓ UI built successfully");
    }

    public async Task PackageAsync()
    {
        BuildContainer.NotNull("Build container not initialized. Ensure Restore ran.");

        Log.Information("Building Linux Installer...");

        await NpmTasks.RunScript(BuildContainer!, "build:linux", DockerDenshiDirectory);
    }

    public async Task CollectAsync()
    {
        // Stop build container
        if (BuildContainer != null)
        {
            Log.Information("Stopping build container...");
            await BuildContainer.StopAsync();
        }

        // Copy binaries to output
        if (BinariesDirectory.DirectoryExists())
        {
            var outputBinariesDir = OutputDirectory / "binaries";
            outputBinariesDir.CreateOrCleanDirectory();
            BinariesDirectory.GlobFiles()
                .ForEach(x => x.CopyToDirectory(outputBinariesDir));
        }

        // Copy dist folder if it exists
        var distDir = DenshiDirectory / "dist";
        if (distDir.DirectoryExists())
        {
            var outputDistDir = OutputDirectory / "dist";
            if (outputDistDir.DirectoryExists())
                Directory.Delete(outputDistDir, true);

            // only copy the folder that ends with "-unpacked"
            distDir.GlobDirectories("*-unpacked")
                .ForEach(x => x.CopyToDirectory(outputDistDir));

            // we're only copying the setup binary ("seanime-denshi-{VERSION}_{PLATFORM}_{ARCHITECTURE}{EXTENSION}")
            // but not .blockmap files
            distDir.GlobFiles("seanime-denshi-*")
                .Where(x => !x.Extension.Equals(".blockmap", StringComparison.OrdinalIgnoreCase))
                .ForEach(x => x.CopyToDirectory(outputDistDir));
        }

        Log.Information("✓ Seanime/Linux complete. Output available in: {Dir}", OutputDirectory);
    }

    bool disposed;

    public async ValueTask DisposeAsync()
    {
        if (disposed)
            return;

        disposed = true;
        if (BuildContainer != null)
        {
            await BuildContainer.DisposeAsync();
            BuildContainer = null;
        }
    }
}
