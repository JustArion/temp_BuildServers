using DotNet.Testcontainers.Containers;
using Fallout.Common.Tooling;
using Tools;
using static Tools.NodeTasks;

namespace Extensions.Container;

public static partial class NodeTasksEx
{
    extension(NodeTasks)
    {
        /// <summary>Get the Node.js version</summary>
        public static async ValueTask<Version?> GetVersion(IContainer? container)
        {
            var output = new List<string>();
            if (container == null)
                NodeTasks.Node("--version", logger: (_, msg) => output.Add(msg));
            else
                await NodeTasks.Node(container, "--version", logger: (_, msg) => output.Add(msg));
        
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
        
        /// <summary>Run a Node.js command with arguments inside a container</summary>
        public static async Task<IReadOnlyCollection<Output>> Node(IContainer container, StringBuilderArgumentStringHandler arguments,
            string? workingDirectory = null,
            int? timeout = null,
            bool logOutput = true,
            bool logInvocation = true,
            Action<OutputType, string>? logger = null) => await ContainerHelper.Run("node", container, arguments, workingDirectory, timeout, logOutput, logInvocation, logger);
    }


}