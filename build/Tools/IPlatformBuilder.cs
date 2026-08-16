namespace Tools;

/// <summary>
/// One program built for one target operating system.
/// <para>
/// Deliberately <b>not</b> a <c>FalloutBuild</c>. Fallout resolves target dependencies against a
/// single build instance, so a nested <c>FalloutBuild</c> can never contribute targets to the graph.
/// Making the per-platform work a plain class instead means it can hold ordinary fields — which is
/// what build components alone could not give us — while the target graph lives in one place.
/// </para>
/// </summary>
public interface IPlatformBuilder : IAsyncDisposable
{
    /// <summary>The operating system whose artifacts this builder produces.</summary>
    OperatingSystem Target { get; }

    /// <summary>Display name used in logs and build summaries, e.g. <c>Seanime/Linux</c>.</summary>
    string Name { get; }

    /// <summary>Whether this builder can run on the current host.</summary>
    bool IsAvailable { get; }

    /// <summary>Why <see cref="IsAvailable"/> is false, for a useful skip message.</summary>
    string? UnavailableReason { get; }

    /// <summary>Per-builder state: source location, patches, and so on.</summary>
    BuildServerOptions Options { get; }

    Task CleanAsync();

    /// <summary>Fetch sources and install toolchains.</summary>
    Task RestoreAsync();

    /// <summary>Build the server binary and the UI.</summary>
    Task CompileAsync();

    /// <summary>Produce the installer.</summary>
    Task PackageAsync();

    /// <summary>Copy the finished artifacts into the output directory.</summary>
    Task CollectAsync();
}
