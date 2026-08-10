namespace Tools;

/// <summary>
/// One open-source program (Seanime, MPV, …) that can be built for several operating systems.
/// A program owns its per-platform builders and decides which of them apply to a requested matrix.
/// </summary>
public interface IProgramBuilder : IAsyncDisposable
{
    /// <summary>Display name used in logs and build summaries.</summary>
    string Name { get; }

    /// <summary>Shared options handed down to every platform builder.</summary>
    BuildServerOptions Options { get; }

    /// <summary>Every platform this program knows how to build, available or not.</summary>
    IReadOnlyList<IPlatformBuilder> Platforms { get; }

    /// <summary>
    /// Work that is worth doing once for the whole matrix rather than once per platform —
    /// cloning the repository a single time when several platforms need the same sources.
    /// </summary>
    Task PrepareAsync(IReadOnlyList<IPlatformBuilder> selected);
}
