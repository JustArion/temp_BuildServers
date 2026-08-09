using System.Diagnostics.CodeAnalysis;
using DotNet.Testcontainers.Containers;
using Fallout.Common.Tooling;
using Fallout.Common.Tools.Git;
using Tools;

namespace Extensions.Container;

public static class GitTasksEx
{
    extension(GitTasks)
    {
        [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
        public static async Task<IReadOnlyCollection<Output>> Git(
            IContainer container,
            StringBuilderArgumentStringHandler arguments,
            string? workingDirectory = null,
            int? timeout = null,
            bool logOutput = true,
            bool logInvocation = true,
            Action<OutputType, string>? logger = null)
        {
            return await ContainerHelper.Run("git", container, arguments, workingDirectory, timeout, logOutput, logInvocation, logger);
        }
    }
}