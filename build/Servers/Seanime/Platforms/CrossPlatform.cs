using System.Runtime.InteropServices;

namespace Servers.Seanime.Platforms;

internal static class CrossPlatform
{
    /// <summary>Get the platform-specific binary name for Seanime server</summary>
    internal static string GetPlatformBinaryName(OperatingSystem? targetSystem = null)
    {
        var architecture = RuntimeInformation.ProcessArchitecture;
        
        if (targetSystem != null)
        {
            return targetSystem switch
            {
                OperatingSystem.Windows => "seanime-server-windows.exe",
                OperatingSystem.MacOS => architecture == Architecture.Arm64
                    ? "seanime-server-darwin-arm64"
                    : "seanime-server-darwin-amd64",
                OperatingSystem.Linux => architecture == Architecture.Arm64
                    ? "seanime-server-linux-arm64"
                    : "seanime-server-linux-amd64",
                _ => throw new ArgumentOutOfRangeException(nameof(targetSystem), targetSystem, null)
            };
        }

        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return "seanime-server-windows.exe";

        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return architecture == Architecture.Arm64
                ? "seanime-server-darwin-arm64"
                : "seanime-server-darwin-amd64";

        if (!RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            throw new NotSupportedException($"Unsupported OS: {RuntimeInformation.OSDescription}");
        
        return architecture == Architecture.Arm64
            ? "seanime-server-linux-arm64"
            : "seanime-server-linux-amd64";

    }
}