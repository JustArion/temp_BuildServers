using System.Diagnostics.CodeAnalysis;
using System.Runtime.InteropServices;
using Serilog;

namespace Tools;

public class PrerequisiteManager
{
    [Flags]
    public enum OperatingSystem
    {
        Windows,
        MacOS,
        Linux
    }

    [SuppressMessage("ReSharper", "ConvertIfStatementToReturnStatement")]
    private static OperatingSystem GetOperatingSystem()
    {
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Windows))
            return OperatingSystem.Windows;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.OSX))
            return OperatingSystem.MacOS;
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
            return OperatingSystem.Linux;

        throw new NotSupportedException("Unsupported operating system");
    }

    /// <summary>Check if a tool is installed on the system</summary>
    public static bool IsToolInstalled(string toolName, string versionArg = "--version")
    {
        try
        {
            var process = StartProcess(toolName, versionArg, logOutput: false, logInvocation: false);
            process.WaitForExit();
            return process.ExitCode == 0;
        }
        catch
        {
            return false;
        }
    }

    /// <summary>Get the version of a tool</summary>
    public static Version? GetToolVersion(string toolName, string versionArg = "--version")
    {
        try
        {
            var output = new List<string>();
            var process = StartProcess(toolName, versionArg, 
                logOutput: false, 
                logInvocation: false,
                logger: (_, msg) => output.Add(msg));
            process.WaitForExit();
            var versionString = output.FirstOrDefault()?.Trim() ?? string.Empty;
            
            if (string.IsNullOrEmpty(versionString))
                return null;
            
            var (major, minor, patch) = ParseVersion(versionString);
            return new Version(major, minor, patch);
        }
        catch
        {
            // Ignore
        }

        return null;
    }

    /// <summary>Compare semantic versions (e.g., "1.24.0" >= "1.20.0")</summary>
    public static bool IsVersionGreaterOrEqual(Version? actualVersion, string requiredVersion)
    {
        try
        {
            if (actualVersion == null)
                return false;

            var required = ParseVersion(requiredVersion);

            if (actualVersion.Major > required.Major) return true;
            if (actualVersion.Major < required.Major) return false;

            if (actualVersion.Minor > required.Minor) return true;
            if (actualVersion.Minor < required.Minor) return false;

            return actualVersion.Build >= required.Patch;
        }
        catch
        {
            return false;
        }
    }

    private static (int Major, int Minor, int Patch) ParseVersion(string version)
    {
        var parts = version
            .Split([".", "-", "v"], StringSplitOptions.RemoveEmptyEntries)
            .Take(3)
            .ToArray();

        var major = parts.Length > 0 && int.TryParse(parts[0], out var m) ? m : 0;
        var minor = parts.Length > 1 && int.TryParse(parts[1], out var mi) ? mi : 0;
        var patch = parts.Length > 2 && int.TryParse(parts[2], out var p) ? p : 0;

        return (major, minor, patch);
    }

    public void Require(string toolName, string displayName)
    {
        if (IsToolInstalled(toolName))
            return;
        
        InstallTool(toolName, displayName);
    }
    
    /// <summary>Install a tool based on the current OS</summary>
    public static void InstallTool(string toolName, string displayName)
    {
        var os = GetOperatingSystem();
        
        if (os == OperatingSystem.Windows)
        {
            // For Windows, ensure scoop is installed first
            if (!IsToolInstalled("scoop"))
            {
                Log.Information("Scoop not found, installing Scoop...");
                InstallScoop();
                
                if (!IsToolInstalled("scoop"))
                    throw new InvalidOperationException("Failed to install Scoop");
                
                Log.Information("✓ Scoop installed successfully");
            }
        }

        var installCommand = os switch
        {
            OperatingSystem.Windows => GetWindowsInstallCommand(toolName),
            OperatingSystem.MacOS => GetMacOsInstallCommand(toolName),
            OperatingSystem.Linux => GetLinuxInstallCommand(toolName),
            _ => throw new NotSupportedException($"OS not supported: {os}")
        };

        if (string.IsNullOrEmpty(installCommand))
            throw new InvalidOperationException($"Don't know how to install {displayName} on {os}");

        Log.Information("Installing {DisplayName} using: {Command}", displayName, installCommand);
        ExecuteCommand(installCommand);
    }

    /// <summary>Install Scoop package manager on Windows</summary>
    private static void InstallScoop()
    {
        const string SCOOP_INSTALL_COMMAND = """
                                       Set-ExecutionPolicy -ExecutionPolicy RemoteSigned -Scope CurrentUser -Force; 
                                       Invoke-RestMethod -Uri https://get.scoop.sh | Invoke-Expression
                                       """;
        ExecuteCommand(SCOOP_INSTALL_COMMAND);
    }

    private static string GetWindowsInstallCommand(string toolName)
    {
        return toolName switch
        {
            "go" => "scoop install go",
            "node" => "scoop install nodejs",
            _ => string.Empty
        };
    }

    private static string GetMacOsInstallCommand(string toolName)
    {
        return toolName switch
        {
            "go" => "brew install go",
            "node" => "brew install node",
            _ => string.Empty
        };
    }

    private static string GetLinuxInstallCommand(string toolName)
    {
        // We can't support Linux right away since apt-get is Ubuntu specific and we don't wanna install package managers on the user's behalf.
        // Installing toolchains is one thing, but installing package managers is different, also, this would prob need sudo to install, and we're trying to limit permissions to non-root only
        throw new NotSupportedException();  
        // return toolName switch
        // {
        //     "go" => "apt-get update && apt-get install -y golang-go",
        //     "node" => "apt-get update && apt-get install -y nodejs npm",
        //     "npm" => "apt-get update && apt-get install -y npm",
        //     _ => string.Empty
        // };
    }

    private static bool? _isPowershell7Supported;
    /// <summary>Execute a shell command</summary>
    private static void ExecuteCommand(string command)
    {
        var os = GetOperatingSystem();

        if (os == OperatingSystem.Windows)
        {
            if (_isPowershell7Supported == null)
                _isPowershell7Supported = IsToolInstalled("pwsh");
            
            if (_isPowershell7Supported.Value)
                StartProcess("pwsh", $"-NoProfile -Command {command}").WaitForExit();
            else
                StartProcess("powershell", $"-NoProfile -Command {command}").WaitForExit();
        }
        else
            StartProcess("/bin/bash", $"-c \"{command}\"").WaitForExit();
    }
}
