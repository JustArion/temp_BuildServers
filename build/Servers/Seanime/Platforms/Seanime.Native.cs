#define SKIP_NATIVE
using System.Diagnostics.CodeAnalysis;
using System.IO;
using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using Extensions;
using Fallout.Common;
using Fallout.Common.IO;
using Fallout.Common.Utilities.Collections;
using Serilog;
using Tools;
using static Tools.Paths;
using GoTasks = Tools.GoTasks;
using NodeTasks = Tools.NodeTasks;
using NpmTasks = Tools.NpmTasks;

namespace Servers.Seanime.Platforms;

/// <summary>
/// Build server for Seanime Denshi (Electron-based desktop client).
/// Builds the backend Go server and frontend Node.js/Electron UI.
/// 
/// Prerequisites:
/// - Git
/// - Go 1.24+ (backend server)
/// - Node.js 20+ (frontend and Electron app)
/// - npm (comes with Node.js)
/// - wget*
/// 
/// Repository: https://github.com/5rahim/seanime
/// </summary>
public class Seanime_Native : FalloutBuildServerBase, IBuildServer
{
    #if SKIP_NATIVE
    public bool IsAvailable => false;
    #else
    public bool IsAvailable => true;
    #endif

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static string CreateTargetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Seanime.Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "Seanime.OSX";
        return "Seanime";
    }

    internal readonly AbsolutePath ArtifactsDirectory = ArtifactsDirectoryFor(nameof(SeanimeServer)) / CreateTargetName();
    internal readonly AbsolutePath OutputDirectory = OutputDirectoryFor(nameof(SeanimeServer)) / CreateTargetName();
    
    AbsolutePath RepositoryRoot => ArtifactsDirectory / "seanime";
    AbsolutePath WebDirectory => RepositoryRoot / "seanime-web";
    AbsolutePath DenshiDirectory => RepositoryRoot / "seanime-denshi";
    AbsolutePath BinariesDirectory => DenshiDirectory / "binaries";
    
    public Target Clean => _ => _
        .Before(Restore)
        .Executes(() =>
        {
            ArtifactsDirectory.CreateOrCleanDirectory();
            OutputDirectory.CreateOrCleanDirectory();
            // We only need to clean this if we're doing a dev scrub
            // ElectronBuilderCacheDirectory.DeleteDirectory();
        });
    
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

    Target Restore_Git => _ => _
        .DependsOn(Restore_Directories)
        .OnlyWhenDynamic(() => Options.LocalFolder is not { Exists: true })
        .Executes(() =>
        {
            if (RepositoryRoot.DirectoryExists())
                Git("pull", RepositoryRoot);
            else if (Options.RemoteGitUrl != null)
                Git($"clone {Options.RemoteGitUrl}", ArtifactsDirectory, logOutput: false);
            else
                Git("clone https://github.com/5rahim/seanime", ArtifactsDirectory, logOutput: false);
        });
    
    Target Restore_Project => _ => _
        .DependsOn(Restore_Git);

    Target Restore_NPM => _ => _
        .DependsOn(Restore_Project)
        .Executes(() =>
        {
            // Install dependencies in seanime-denshi
            Log.Information("Running npm ci in seanime-denshi...");
            NpmTasks.Npm("ci", DenshiDirectory);
            
            // Install dependencies in seanime-web
            Log.Information("Running npm install in seanime-web");
            NpmTasks.Install(WebDirectory);
        });

    Target PrerequisitesMet => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Checking prerequisites...");

            var goVersion = GetVersion();
            Log.Information("Found Go: {Version}", goVersion);

            if (!PrerequisiteManager.IsVersionGreaterOrEqual(goVersion, "1.24"))
                throw new InvalidOperationException($"Go 1.24+ is required, but found {goVersion}");

            var nodeVersion = NodeTasks.GetVersion();
            Log.Information("Found Node.js: {Version}", nodeVersion);

            if (!PrerequisiteManager.IsVersionGreaterOrEqual(nodeVersion, "20.0.0"))
                throw new InvalidOperationException($"Node.js 20+ is required, but found {nodeVersion}");

            var npmVersion = NpmTasks.GetVersion();
            Log.Information("Found npm: {Version}", npmVersion);

            Log.Information("✓ All prerequisites are satisfied");
        });

    public Target Restore => _ => _
        .DependsOn(Restore_Project)
        .DependsOn(Restore_NPM)
        .DependsOn(PrerequisitesMet);
    
    Target BuildServer => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Building Go server...");
            
            Log.Information("Ensuring {Directory} is created and clean", BinariesDirectory);
            BinariesDirectory.CreateOrCleanDirectory();
            var binaryName = CrossPlatform.GetPlatformBinaryName();
            var binaryPath = BinariesDirectory / binaryName;

            var tags = RuntimeInformation.IsOSPlatform(OSPlatform.Windows) 
                ? "nosystray" 
                : null;
            
            GoTasks.Build(binaryPath, RepositoryRoot, ldFlags: "-s -w", tags: tags);
            Log.Information("✓ Server binary built: {Binary}", binaryPath);
        });
    
    Target BuildUI => _ => _
        .DependsOn(Restore)
        .Executes(() =>
        {
            Log.Information("Building UI...");

            // Build web UI
            Log.Information("Building web UI...");
            NpmTasks.RunScript("build", WebDirectory);
            NpmTasks.RunScript("build:denshi", WebDirectory);

            Log.Information("✓ UI built successfully");
            
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

            if (!webDenshiOutput.DirectoryExists()) 
                return;
            
            Log.Information("Moving web-denshi output...");
            if (targetDenshiWebDir.DirectoryExists())
                Directory.Delete(targetDenshiWebDir, true);
            webDenshiOutput.Move(targetDenshiWebDir);
        });
    
    AbsolutePath ElectronBuilderCacheDirectory => AbsolutePath.Create(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)) / "electron-builder" / "Cache";
        // We need to extract this ourselves manually to %LOCALAPPDATA%\electron-builder\Cache\winCodeSign as winCodeSign-2.6.0
    // Since the npm script will fail (at extracting Mac related files (symlinks require admin, and we don't want to run the build server as admin))
    // https://github.com/electron-userland/electron-builder-binaries/releases/download/winCodeSign-2.6.0/winCodeSign-2.6.0.7z
    [SuppressMessage("ReSharper", "NestedStringInterpolation")]
    [SupportedOSPlatform("windows")]
    private void FixCodeSigning()
    {
        if (IsAdmin())
            return;
        
        var codeSignDirectory = ElectronBuilderCacheDirectory / "winCodeSign";
        var csDirectory = codeSignDirectory / "winCodeSign-2.6.0";
        if (csDirectory.DirectoryExists())
            return;
        
        Log.Information("Fixing Code Signing permissions");
        
        if (!PrerequisiteManager.IsToolInstalled("wget")) 
            PrerequisiteManager.InstallTool("wget", "WGet");

        codeSignDirectory.CreateDirectory();

        StartProcess("wget", "https://github.com/electron-userland/electron-builder-binaries/releases/download/winCodeSign-2.6.0/winCodeSign-2.6.0.7z", codeSignDirectory, logOutput: false)
            .WaitForExit();
        
        var sevenZip = DenshiDirectory / "node_modules" / "7zip-bin" / "win" / "x64" / "7za.exe";
        Assert.FileExists(sevenZip, "7za.exe not found. Please ensure npm install has been run in seanime-denshi.");
        
        // I was unable to get the exact args with the version tag seanime uses so this is the closest, but observed arguments are passed 1:1 from when electron-builder tried to extract the file on this version
        // https://github.com/electron-userland/electron-builder/blob/release/v26/packages/app-builder-lib/src/util/electronGet.ts#L285
        StartProcess(sevenZip, $"x -snld -bd {codeSignDirectory / "winCodeSign-2.6.0.7z"} {$"-o{csDirectory}"}", codeSignDirectory, logOutput: false)
            .WaitForExit();
        
        (codeSignDirectory / "winCodeSign-2.6.0.7z")
            .DeleteFile(); // Cleanup (approx 5mb)
    }
    Target BuildInstaller => _ => _
        .DependsOn(BuildServer)
        .DependsOn(BuildUI)
        .Executes(() =>
        {
            if (IsTarget(OperatingSystem.Linux))
                return;
            
            Log.Information("Building Setup Natively...");

            if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows) && Targeting(OperatingSystem.Windows))
            {
                FixCodeSigning();
                NpmTasks.RunScript("build:win", DenshiDirectory);
            }
            else if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX) && Targeting(OperatingSystem.MacOS))
                NpmTasks.RunScript("build:mac", DenshiDirectory);
        });

    public Target Build => _ => _
        .DependsOn(BuildInstaller)
        .Executes(() =>
        {
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
            if (!distDir.DirectoryExists()) 
                return;
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
}