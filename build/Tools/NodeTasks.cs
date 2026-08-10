using Fallout.Common.Tooling;

namespace Tools;

[PathTool(Executable = PathExecutable)]
public partial class NodeTasks : ToolTasks, IRequirePathTool
{
    public static string NodePath { get => new NodeTasks().GetToolPathInternal(); set => new NodeTasks().SetToolPath(value); }
    public const string PathExecutable = "node";

    /// <summary>Run a Node.js command with arguments</summary>
    public static IReadOnlyCollection<Output> Node(ArgumentStringHandler asyncArguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        int? timeout = null,
        bool? logOutput = null,
        bool? logInvocation = null,
        Action<OutputType, string>? logger = null,
        Func<IProcess, object>? exitHandler = null)
    {
        // Presence is asserted up front by PrerequisiteManager.RequireTool — see GoTasks.Go.
        return new NodeTasks().Run(asyncArguments, workingDirectory, environmentVariables, timeout, logOutput, logInvocation,
            logger, exitHandler);
    }
    
    /// <summary>Get the Node.js version</summary>
    public static Version? GetVersion()
    {
        var output = new List<string>();
        Node("--version", logger: (_, msg) => output.Add(msg));
        
        var versionString = output.FirstOrDefault() ?? string.Empty;
        
        // Parse version string like "v16.13.0" to Version
        var match = NodeVersionRegex().Match(versionString);
        if (match.Success && 
            int.TryParse(match.Groups[1].Value, out var major) &&
            int.TryParse(match.Groups[2].Value, out var minor) &&
            int.TryParse(match.Groups[3].Value, out var patch))
        {
            return new Version(major, minor, patch);
        }
        
        return null;
    }
    
    [GeneratedRegex(@"v?(\d+)\.(\d+)\.(\d+)")]
    internal static partial Regex NodeVersionRegex();
}

[PathTool(Executable = PathExecutable)]
[LogErrorAsStandard]
public partial class NpmTasks : ToolTasks, IRequirePathTool
{
    public static string NpmPath { get => new NpmTasks().GetToolPathInternal(); set => new NpmTasks().SetToolPath(value); }
    public const string PathExecutable = "npm";

    /// <summary>Run an npm command with arguments</summary>
    public static IReadOnlyCollection<Output> Npm(ArgumentStringHandler asyncArguments,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        int? timeout = null,
        bool? logOutput = null,
        bool? logInvocation = null,
        Action<OutputType, string>? logger = null,
        Func<IProcess, object>? exitHandler = null)
    {
        // Presence is asserted up front by PrerequisiteManager.RequireTool — see GoTasks.Go.
        return new NpmTasks().Run(asyncArguments, workingDirectory, environmentVariables, timeout, logOutput, logInvocation,
            logger, exitHandler);
    }

    /// <summary>Get the npm version</summary>
    public static Version? GetVersion()
    {
        var output = new List<string>();
        Npm("--version", logger: (_, msg) => output.Add(msg));
        var versionString = output.FirstOrDefault() ?? string.Empty;
        
        // Parse version string like "8.1.0" to Version
        var match = NPMVersionRegex().Match(versionString);
        if (match.Success && 
            int.TryParse(match.Groups[1].Value, out var major) &&
            int.TryParse(match.Groups[2].Value, out var minor) &&
            int.TryParse(match.Groups[3].Value, out var patch))
        {
            return new Version(major, minor, patch);
        }
        
        return null;
    }

    /// <summary>Run npm install</summary>
    public static IReadOnlyCollection<Output> Install(
        string? workingDirectory = null,
        bool production = false,
        IReadOnlyDictionary<string, string>? environmentVariables = null)
    {
        var args = production ? "install --production" : "install";
        return Npm(args, workingDirectory, environmentVariables);
    }

    /// <summary>Run npm script</summary>
    public static IReadOnlyCollection<Output> RunScript(
        string script,
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null) => Npm($"run {script}", workingDirectory, environmentVariables);

    [GeneratedRegex(@"(\d+)\.(\d+)\.(\d+)")]
    internal static partial Regex NPMVersionRegex();
}
