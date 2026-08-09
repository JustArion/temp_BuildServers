using System.Runtime.InteropServices;
using System.Runtime.Versioning;
using System.Security.Principal;
using Models;
using Fallout.Common;

namespace Tools;

public abstract class FalloutBuildServerBase : FalloutBuild
{
    protected FalloutBuildServerBase()
    {
        NoLogo = true;
    }
    
    [SupportedOSPlatform("windows")]
    protected static bool IsAdmin() => new WindowsPrincipal(WindowsIdentity.GetCurrent()).IsInRole(WindowsBuiltInRole.Administrator);

    protected static bool Targeting(OperatingSystem system)
    {
        switch (system)
        {
            case OperatingSystem.Windows:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return true;
                break;
            case OperatingSystem.MacOS:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return true;
                break;
            case OperatingSystem.Linux:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return true;
                break;
            default:
                return false;
        }
        return false;
    }

    protected static bool IsTarget(OperatingSystem system)
    {
        switch (system)
        {
            case OperatingSystem.Windows:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
                    return true;
                break;
            case OperatingSystem.MacOS:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
                    return true;
                break;
            case OperatingSystem.Linux:
                if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
                    return true;
                break;
            default:
                return false;
        }
        return false;
    }

    public virtual BuildServerOptions Options { get; init; } = new();
}