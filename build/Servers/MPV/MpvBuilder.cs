using Serilog;
using Tools;

namespace Servers.MPV;

/// <summary>
/// Placeholder second program. Nothing here builds MPV yet — it exists so the multi-program shape
/// is visible: adding a program is a new <see cref="IProgramBuilder"/> plus a component interface,
/// not a change to any existing one.
/// </summary>
public sealed class MpvBuilder : IProgramBuilder
{
    public const string ProgramName = "MPV";

    public string Name => ProgramName;

    public BuildServerOptions Options { get; } = new();

    public IReadOnlyList<IPlatformBuilder> Platforms { get; } =
    [
        new NotImplementedPlatform(ProgramName, OperatingSystem.Windows),
        new NotImplementedPlatform(ProgramName, OperatingSystem.Linux),
        new NotImplementedPlatform(ProgramName, OperatingSystem.MacOS)
    ];

    public Task PrepareAsync(IReadOnlyList<IPlatformBuilder> selected) => Task.CompletedTask;

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}

/// <summary>A platform that announces itself as unbuilt rather than pretending to work.</summary>
public sealed class NotImplementedPlatform(string program, OperatingSystem target) : IPlatformBuilder
{
    public OperatingSystem Target => target;

    public string Name => $"{program}/{target}";

    public bool IsAvailable => false;

    public string? UnavailableReason => $"{program} is not implemented yet";

    public BuildServerOptions Options { get; } = new();

    public Task CleanAsync() => Skip();
    public Task RestoreAsync() => Skip();
    public Task CompileAsync() => Skip();
    public Task PackageAsync() => Skip();
    public Task CollectAsync() => Skip();

    Task Skip()
    {
        Log.Warning("{Platform}: {Reason}", Name, UnavailableReason);
        return Task.CompletedTask;
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;
}
