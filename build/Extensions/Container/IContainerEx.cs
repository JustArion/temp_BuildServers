using DotNet.Testcontainers.Containers;
using Fallout.Common.Tooling;
using Models;

namespace Extensions.Container;

public static class IContainerEx
{
    extension(IContainer container)
    {
        public async Task<ContainerExecutionOutput> Execute(string command, CancellationToken token = default)
        {
            var result = await container.ExecAsync(["sh", "-c", command.ReplaceLineEndings("\n")], token);

            var retVal = result.Stdout.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => new Output { Type = OutputType.Std, Text = x }).ToList();

            if (result.ExitCode != 0) // We only append errors if the command failed 
                retVal.AddRange(result.Stderr.Split('\n', StringSplitOptions.RemoveEmptyEntries).Select(x => new Output { Type = OutputType.Err, Text = x }));
            
            return new(retVal, result.ExitCode);
        }

        public async Task<ContainerExecutionOutput> BulkExecute(string[] commands, CancellationToken token = default) => await container.Execute(string.Join(" && ", commands), token: token);

    }
}