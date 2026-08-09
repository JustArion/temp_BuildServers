using DotNet.Testcontainers.Containers;
using Fallout.Common.Tooling;
using Tools;

namespace Extensions.Container;

public static class GoTasksEx
{
    extension(GoTasks)
    {
        /// <summary>Get the Go version</summary>
        public static async ValueTask<Version?> GetVersion(IContainer? container)
        {
            var output = new List<string>();
            if (container == null)
                GoTasks.Go("version", logger: (_, msg) => output.Add(msg));
            else
                await Go(container, "version", logger: (_, msg) => output.Add(msg));
            var versionString = string.Join(" ", output.Where(l => !string.IsNullOrWhiteSpace(l)));
        
            // Parse "go version go1.26.5 windows/amd64" to extract version number
            var match = GoVersionRegex().Match(versionString);
            return match.Success && Version.TryParse(match.Groups[1].Value, out var version) ? version : null;
        }
    }

    /// <summary>Run a Go command with arguments inside a container</summary>
    public static async Task<IReadOnlyCollection<Output>> Go(IContainer container, StringBuilderArgumentStringHandler arguments,
        string? workingDirectory = null,
        int? timeout = null,
        bool logOutput = true,
        bool logInvocation = true,
        Action<OutputType, string>? logger = null) => await ContainerHelper.Run("go", container, arguments, workingDirectory, timeout, logOutput, logInvocation, logger);

    /// <summary>Build a Go project inside a container</summary>
    public static async Task<IReadOnlyCollection<Output>> Build(IContainer container, string outputPath,
        string? workingDirectory = null,
        string? trimPath = "true",
        string? ldFlags = "-s -w",
        string? tags = null)
    {
        if (tags != null)
            return await Go(container, $"build -o {outputPath} -trimpath -ldflags={ldFlags} -tags={tags}", workingDirectory);
        return await Go(container, $"build -o {outputPath} -trimpath -ldflags={ldFlags}", workingDirectory);
    }
}