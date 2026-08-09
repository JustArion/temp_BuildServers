using System.Diagnostics.CodeAnalysis;
using DotNet.Testcontainers.Containers;
using Extensions.Container;
using Models;
using Fallout.Common.Tooling;
using Serilog;

namespace Tools;

public static class ContainerHelper
{
        [SuppressMessage("Usage", "CA2208:Instantiate argument exceptions correctly")]
        public static async Task<IReadOnlyCollection<Output>> Run(
            string toolName,
            IContainer container,
            StringBuilderArgumentStringHandler argumentHandler,
            string? workingDirectory = null,
            int? timeout = null,
            bool logOutput = true,
            bool logInvocation = true,
            Action<OutputType, string>? logger = null)
        {
            workingDirectory ??= Environment.CurrentDirectory;

            var arguments = argumentHandler.ToStringAndClear();
            
            if (logInvocation is not false)
            {
                Log.Information("> {ToolName} {Arguments}", toolName, arguments);
                Log.Information("@ {WorkingDirectory}", workingDirectory);
            }

            var token = CancellationToken.None;
            if (timeout.HasValue)
                token = new CancellationTokenSource(TimeSpan.FromMilliseconds(timeout.Value)).Token;
            
            ContainerExecutionOutput output;
            
            if (!string.IsNullOrWhiteSpace(workingDirectory))
                output = await container.BulkExecute([$"cd {workingDirectory}", $"{toolName} {arguments}"], token: token);
            else 
                output = await container.Execute($"{toolName} {arguments}", token);

            if (!logOutput)
            {
                return output.ExitCode != 0 
                    ? throw new Exception($"Execution returned {output.ExitCode}", new(string.Join("\n", output.Output.Where(o => o.Type == OutputType.Err).Select(o => o.Text)))) 
                    : output.Output;
            }
            
            foreach (var output1 in output.Output)
            {
                switch (output1.Type)
                {
                    case OutputType.Std when logger != null:
                        logger.Invoke(OutputType.Std, output1.Text);
                        break;
                    case OutputType.Std:
                        Log.Information("{Output}", output1.Text);
                        break;
                    case OutputType.Err when logger != null:
                        logger.Invoke(OutputType.Err, output1.Text);
                        break;
                    case OutputType.Err:
                        Log.Error("{Output}", output1.Text);
                        break;
                    default:
                        throw new ArgumentOutOfRangeException(nameof(output1));
                }
            }

            return output.ExitCode != 0 
                ? throw new Exception($"Execution returned {output.ExitCode}", new(string.Join("\n", output.Output.Where(o => o.Type == OutputType.Err).Select(o => o.Text)))) 
                : output.Output;
        }
}