using Fallout.Common.Tooling;

namespace Models;

public record ContainerExecutionOutput(IReadOnlyCollection<Output> Output, long? ExitCode);
