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
/// Build server for Seanime Denshi (Electron-based desktop client).
/// Builds the backend Go server and frontend Node.js/Electron UI.
/// 
/// Prerequisites:
/// - Docker
/// 
/// Repository: https://github.com/5rahim/seanime
/// </summary>
public class Seanime_Linux : FalloutBuildServerBase, IBuildServer
{
    internal readonly AbsolutePath ArtifactsDirectory = Paths.ArtifactsDirectoryFor(nameof(SeanimeServer)) / "Linux";
    internal readonly AbsolutePath OutputDirectory = Paths.OutputDirectoryFor(nameof(SeanimeServer)) / "Linux";
    
    AbsolutePath RepositoryRoot => ArtifactsDirectory / "seanime";
    AbsolutePath WebDirectory => RepositoryRoot / "seanime-web";
    AbsolutePath DenshiDirectory => RepositoryRoot / "seanime-denshi";
    AbsolutePath BinariesDirectory => DenshiDirectory / "binaries";
    
    static readonly AbsolutePath DockerArtifactsDirectory = AbsolutePath.Create("/app");
    static readonly AbsolutePath DockerRepositoryRoot = DockerArtifactsDirectory / "seanime";
    static readonly AbsolutePath DockerWebDirectory = DockerRepositoryRoot / "seanime-web";
    static readonly AbsolutePath DockerDenshiDirectory = DockerRepositoryRoot / "seanime-denshi";
    static readonly AbsolutePath DockerBinariesDirectory = DockerDenshiDirectory / "binaries";

    #if SKIP_LINUX 
    public bool IsAvailable => false;
    #endif
    public bool IsAvailable => PrerequisiteManager.IsToolInstalled("docker");

    public Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            OutputDirectory.CreateOrCleanDirectory();
        });

    IContainer? BuildContainer { get; set; }

    Target Restore_Directories => _ => _
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            OutputDirectory.CreateOrCleanDirectory();

            if (Options.LocalFolder is not { Exists: true }) 
                return;
            
            Log.Information("Using local repository: {Folder}", Options.LocalFolder);
            // Clone the local folder to the Artifacts directory
            Options.LocalFolder.CopyTo(new DirectoryInfo(ArtifactsDirectory));
        });
    
    Target Restore_Container => _ => _
        .DependsOn(Restore_Directories)
        .Executes(async () =>
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
            
        });
    
    Target Restore_Git => _ => _
        .DependsOn(Restore_Container)
        .OnlyWhenDynamic(() => Options.LocalFolder is not { Exists: true })
        .Executes(async () =>
        {
            try
            {
                if (RepositoryRoot.DirectoryExists())
                    await GitTasks.Git(BuildContainer!, "pull", DockerRepositoryRoot);
                else if (Options.RemoteGitUrl != null)
                    await GitTasks.Git(BuildContainer!, $"clone {Options.RemoteGitUrl}", DockerArtifactsDirectory, logOutput: false);
                else
                    await GitTasks.Git(BuildContainer!, $"clone https://github.com/5rahim/seanime", DockerArtifactsDirectory, logOutput: false);
            }
            catch (Exception ex)
            {
                Log.Error(ex, "Failed to initialize build container");
                await BuildContainer!.StopAsync();
                throw;
            }
        });

    Target Restore_Project => _ => _
        .DependsOn(Restore_Git);

    Target Restore_NPM => _ => _
        .DependsOn(Restore_Project)
        .Executes(async () =>
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
                Log.Error(ex, "Failed to initialize build container");
                await BuildContainer!.StopAsync();
                throw;
            }
        });

    public Target Restore => _ => _
        .DependsOn(Restore_Container)
        .DependsOn(Restore_Project)
        .DependsOn(Restore_NPM)
        .Executes(() => Log.Information("✓ Build container ready"));
    
    Target BuildServer => _ => _
        .DependsOn(Restore)
        .Executes(async () =>
        {
            Log.Information("Building Go server within container...");
            BinariesDirectory.CreateOrCleanDirectory();
            
            Log.Information("Ensuring {Directory} is created and clean", BinariesDirectory);
            BinariesDirectory.CreateOrCleanDirectory();
            var binaryName = CrossPlatform.GetPlatformBinaryName(OperatingSystem.Linux);
            var binaryPath = DockerBinariesDirectory / binaryName;
            
            await GoTasksEx.Build(BuildContainer!, binaryPath, DockerRepositoryRoot, ldFlags: "-s -w");
            Log.Information("✓ Server binary built from container: {Binary}", binaryPath);
            
        });
    
    Target BuildUI => _ => _
        .DependsOn(Restore)
        .Executes(async () =>
        {
            BuildContainer.NotNull("Build container not initialized. Ensure Restore target ran.");
            
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
        });
    
    Target BuildInstaller => _ => _
        .DependsOn(BuildServer)
        .DependsOn(BuildUI)
        .Executes(async () =>
        {
            BuildContainer.NotNull("Build container not initialized. Ensure Restore target ran.");
            
            Log.Information("Building Linux Installer...");
            
            await NpmTasks.RunScript(BuildContainer!, "build:linux", DockerDenshiDirectory);
        });

    public Target Build => _ => _
        .OnlyWhenDynamic(() => PrerequisiteManager.IsToolInstalled("docker"))
        .DependsOn(BuildInstaller)
        .Executes(async () =>
        {
            // Stop build container
            if (BuildContainer != null)
            {
                Log.Information("Stopping build container...");
                await BuildContainer.StopAsync();
                BuildContainer = null;
            }
            
            Log.Information("✓ Seanime build complete. Output available in: {Dir}", OutputDirectory);
                
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
        });

    private bool _disposed;
    public async ValueTask DisposeAsync()
    {
        if (_disposed)
            return;
        
        _disposed = true;
        GC.SuppressFinalize(this);
        if (BuildContainer != null) 
            await BuildContainer.DisposeAsync();
    }
}