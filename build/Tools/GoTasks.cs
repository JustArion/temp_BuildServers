
using Fallout.Common.Tooling;

namespace Tools;

[PathTool(Executable = PathExecutable)]
[LogErrorAsStandard]
public partial class GoTasks : ToolTasks, IRequirePathTool
{
    public static string GoPath { get => new GoTasks().GetToolPathInternal(); set => new GoTasks().SetToolPath(value); }
    public const string PathExecutable = "go";

    /// <summary>Run a Go command with arguments</summary>
    public static IReadOnlyCollection<Output> Go(ArgumentStringHandler asyncArguments, 
        string? workingDirectory = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null,
        int? timeout = null,
        bool? logOutput = null,
        bool? logInvocation = null,
        Action<OutputType, string>? logger = null,
        Func<IProcess, object>? exitHandler = null)
    {
        // No auto-install here: this runs on every Go invocation, including a plain version probe,
        // and would silently install Scoop + Go on the host. Presence is asserted up front instead,
        // via PrerequisiteManager.RequireTool.
        var task = new GoTasks();
        
        return task.Run(asyncArguments, workingDirectory, environmentVariables, timeout, logOutput, logInvocation,
            logger ?? task.GetLogger(), exitHandler);
    }

    
    
    /// <summary>Get the Go version</summary>
    public static Version? GetVersion()
    {
        var output = new List<string>();
        Go("version", logger: (_, msg) => output.Add(msg));
        var versionString = string.Join(" ", output.Where(l => !string.IsNullOrWhiteSpace(l)));
        
        // Parse "go version go1.26.5 windows/amd64" to extract version number
        var match = GoVersionRegex().Match(versionString);
        return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
    }

    /// <summary>Build a Go project</summary>
    public static IReadOnlyCollection<Output> Build(string outputPath, 
        string? workingDirectory = null,
        string? ldFlags = "-s -w",
        string? tags = null,
        IReadOnlyDictionary<string, string>? environmentVariables = null) => Go($"build -o {outputPath} -trimpath -ldflags={ldFlags} -tags={tags}", workingDirectory, environmentVariables);



    [GeneratedRegex(@"go(\d+\.\d+\.\d+)")]
    internal static partial Regex GoVersionRegex();
}
