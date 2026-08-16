using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;

namespace Tools;

/// <summary>
/// Shared helpers for platform builders. The old <c>FalloutBuildServerBase</c> derived from
/// <c>FalloutBuild</c> purely to reach these helpers and to declare targets — but deriving from
/// <c>FalloutBuild</c> is exactly what made the class unusable as a nested build server. None of
/// this needs a build instance, so it does not have one.
/// </summary>
public abstract class PlatformBuilderBase
{
    public BuildServerOptions Options { get; init; } = new();

    [SupportedOSPlatform("windows")]
    protected static bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    /// <summary>Whether the build is currently running on <paramref name="system"/>.</summary>
    protected static bool HostIs(OperatingSystem system) => system switch
    {
        OperatingSystem.Windows => RuntimeInformation.IsOSPlatform(OSPlatform.Windows),
        OperatingSystem.MacOS => RuntimeInformation.IsOSPlatform(OSPlatform.OSX),
        OperatingSystem.Linux => RuntimeInformation.IsOSPlatform(OSPlatform.Linux),
        _ => false
    };
}
