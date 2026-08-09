using DotNet.Testcontainers.Containers;
using Fallout.Common.Tooling;
using Tools;
using static Tools.NpmTasks;

namespace Extensions.Container;

public static class NPMTasksEx
{
    extension(NpmTasks)
    {
        /// <summary>Get the npm version</summary>
        public static async ValueTask<Version?> GetVersion(IContainer? container)
        {
            var output = new List<string>();
            if (container == null)
                NpmTasks.Npm("--version", logger: (_, msg) => output.Add(msg));
            else
                await NpmTasks.Npm(container, "--version", logger: (_, msg) => output.Add(msg));
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

        /// <summary>Run an npm command with arguments inside a container</summary>
        public static async Task<IReadOnlyCollection<Output>> Npm(
            IContainer container,
            StringBuilderArgumentStringHandler arguments,
            string? workingDirectory = null,
            int? timeout = null,
            bool logOutput = true,
            bool logInvocation = true,
            Action<OutputType, string>? logger = null) => await ContainerHelper.Run("npm", container, arguments, workingDirectory, timeout, logOutput, logInvocation, logger);

        /// <summary>Run npm install inside a container</summary>
        public static async Task<IReadOnlyCollection<Output>> Install(IContainer container,
            string? workingDirectory = null,
            bool production = false)
        {
            var args = production ? "install --production" : "install";
            return await NpmTasks.Npm(container, args, workingDirectory);
        }

        /// <summary>Run npm script inside a container</summary>
        public static async Task<IReadOnlyCollection<Output>> RunScript(IContainer container,
            string? script,
            string? workingDirectory = null) => await NpmTasks.Npm(container, $"run {script}", workingDirectory);
    }
}