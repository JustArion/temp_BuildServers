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
/// Builds Seanime natively for the host operating system (Windows or macOS; a Linux host is served
/// by <see cref="SeanimeLinuxBuilder"/> through Docker instead).
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
// Build with -p:DefineConstants=SKIP_NATIVE to force this platform off.
public sealed class SeanimeNativeBuilder : PlatformBuilderBase, IPlatformBuilder
{
    public OperatingSystem Target => PrerequisiteManager.HostOperatingSystem;

    public string Name => $"{SeanimeBuilder.ProgramName}/{CreateTargetName()}";

#if SKIP_NATIVE
    public bool IsAvailable => false;
    public string? UnavailableReason => "compiled with SKIP_NATIVE";
#else
    public bool IsAvailable => !HostIs(OperatingSystem.Linux);
    public string? UnavailableReason => IsAvailable ? null : "native builds are not produced on a Linux host";
#endif

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static string CreateTargetName()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "Windows";
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return "OSX";
        return "Linux";
    }

    internal readonly AbsolutePath ArtifactsDirectory = ArtifactsDirectoryFor(SeanimeBuilder.ProgramName) / CreateTargetName();
    internal readonly AbsolutePath OutputDirectory = OutputDirectoryFor(SeanimeBuilder.ProgramName) / CreateTargetName();

    AbsolutePath RepositoryRoot => ArtifactsDirectory / "seanime";
    AbsolutePath WebDirectory => RepositoryRoot / "seanime-web";
    AbsolutePath DenshiDirectory => RepositoryRoot / "seanime-denshi";
    AbsolutePath BinariesDirectory => DenshiDirectory / "binaries";

    public Task CleanAsync()
    {
        ArtifactsDirectory.CreateOrCleanDirectory();
        OutputDirectory.CreateOrCleanDirectory();
        // We only need to clean this if we're doing a dev scrub
        // ElectronBuilderCacheDirectory.DeleteDirectory();
        return Task.CompletedTask;
    }

    public Task RestoreAsync()
    {
        CheckPrerequisites();
        RestoreDirectories();
        RestoreGit();
        RestoreNpm();
        return Task.CompletedTask;
    }

    void CheckPrerequisites()
    {
        Log.Information("Checking prerequisites...");

        // Presence first, so a missing toolchain is a clear message rather than an unattended install.
        PrerequisiteManager.RequireTool("git", "Git", Options.InstallMissingTools);
        PrerequisiteManager.RequireTool("go", "Go", Options.InstallMissingTools, versionArg: "version");
        PrerequisiteManager.RequireTool("node", "Node.js", Options.InstallMissingTools);
        PrerequisiteManager.RequireTool("npm", "npm", Options.InstallMissingTools);

        var goVersion = GoTasks.GetVersion();
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

    void RestoreGit()
    {
        if (Options.LocalFolder is { Exists: true })
            return;

        if (RepositoryRoot.DirectoryExists())
            Git("pull", RepositoryRoot);
        else
            Git($"clone {Options.RemoteGitUrl}", ArtifactsDirectory, logOutput: false);
    }

    void RestoreNpm()
    {
        // Install dependencies in seanime-denshi
        Log.Information("Running npm ci in seanime-denshi...");
        NpmTasks.Npm("ci", DenshiDirectory);

        // Install dependencies in seanime-web
        Log.Information("Running npm install in seanime-web");
        NpmTasks.Install(WebDirectory);
    }

    public Task CompileAsync()
    {
        BuildServer();
        BuildUI();
        return Task.CompletedTask;
    }

    void BuildServer()
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
    }

    void BuildUI()
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
    }

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

    public Task PackageAsync()
    {
        Log.Information("Building Setup Natively...");

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
        {
            FixCodeSigning();
            NpmTasks.RunScript("build:win", DenshiDirectory);
        }
        else if (HostIs(OperatingSystem.MacOS))
            NpmTasks.RunScript("build:mac", DenshiDirectory);

        return Task.CompletedTask;
    }

    public Task CollectAsync()
    {
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

        Log.Information("✓ {Name} complete. Output available in: {Dir}", Name, OutputDirectory);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
