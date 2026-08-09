using Extensions;
using Fallout.Common;
using Fallout.Common.IO;
using Servers.Seanime.Platforms;
using Tools;

namespace Servers.Seanime;

// We can currently target the host OS and Linux, if the host or target is Linux, we use docker runner via Testcontainers to build it
// If there is a target for the host-os, we try to build it, installing toolchains as we need it via 
// Windows - Scoop
// Mac-OS - Brew (Untested)
public class SeanimeServer(OperatingSystem targetSystems) : FalloutBuildServerBase, IBuildServer, IAsyncDisposable
{
    // Stubs
    public Target Restore => _ => _;
    public bool IsAvailable => NativeBuildServer.IsAvailable || LinuxBuildServer.IsAvailable;
    
    readonly Seanime_Native NativeBuildServer = new();
    readonly Seanime_Linux LinuxBuildServer = new();

    public Target Build => _ => _
        .DependsOn(Build_Native)
        .DependsOn(Build_Linux);
    
    Target Build_Linux => _ => _
        .DependsOn(PrepareBuild)
        .OnlyWhenDynamic(() => ShouldServeLinux)
        .DependsOn(LinuxBuildServer.Build);

    Target Build_Native => _ => _
        .DependsOn(PrepareBuild)
        .OnlyWhenDynamic(() => ShouldServeNative)
        .DependsOn(LinuxBuildServer.Build);
    
    // A pre-build optimization, if we're serving linux & native (win, mac), we check if we have Git installed, then copy over the contents to both, we do this so we don't need to git clone twice if building double
    Target PrepareBuild => _ => _
        .OnlyWhenDynamic(()=> PrerequisiteManager.IsToolInstalled("git") && Options.LocalFolder == null && ShouldServeNative && ShouldServeLinux)
        .Executes(() =>
        {
            Options.RemoteGitUrl ??= new("https://github.com/5rahim/seanime");
            var artifactsDir = NativeBuildServer.ArtifactsDirectory;
            var artifactsDi = artifactsDir.ToDirectoryInfo();
            Git($"clone {Options.RemoteGitUrl}", artifactsDir, logOutput: false);
            NativeBuildServer.Options.LocalFolder = artifactsDi;
            
            // Now we copy everything over to the LinuxBuildServer.ArtifactsDirectory
            LinuxBuildServer.Options.LocalFolder = artifactsDi;
            artifactsDi.CopyTo(LinuxBuildServer.ArtifactsDirectory.ToDirectoryInfo());
        });
    

    // private enum Server
    // {
    //     Linux,
    //     Native
    // }

    // private int BulkExecute(Func<IBuildServer, Target> param)
    // {
    //     int[] retVals = [0, 0];
    //     
    //     if (LinuxBuildServer.IsAvailable && targetSystems.HasFlag(OperatingSystem.Linux))
    //         retVals[(int)Server.Linux] = Execute<Seanime_Linux>(x => param(x));
    //     
    //     if (NativeBuildServer.IsAvailable && IsNativeBuild)
    //         retVals[(int)Server.Native] = Execute<Seanime_Native>(x => param(x));
    //
    //     return retVals.FirstOrDefault(x => x != 0);
    // }
    
    private bool IsNativeBuild =>
        targetSystems.HasFlag(OperatingSystem.Windows) || targetSystems.HasFlag(OperatingSystem.MacOS);
    private bool ShouldServeLinux => LinuxBuildServer.IsAvailable && targetSystems.HasFlag(OperatingSystem.Linux);
    private bool ShouldServeNative => NativeBuildServer.IsAvailable && IsNativeBuild;
    
    Target Clean_Linux => _ => _
        .OnlyWhenDynamic(() => ShouldServeLinux)
        .DependsOn(LinuxBuildServer.Clean);

    Target Clean_Native => _ => _
        .OnlyWhenDynamic(() => ShouldServeNative)
        .DependsOn(NativeBuildServer.Clean);

    public Target Clean => _ => _
        .DependsOn(Clean_Linux)
        .DependsOn(Clean_Native);
    

    public async ValueTask DisposeAsync()
    {
        GC.SuppressFinalize(this);
        await LinuxBuildServer.DisposeAsync();
    }
}