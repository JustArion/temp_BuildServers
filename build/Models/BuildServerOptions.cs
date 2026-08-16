using System.IO;

namespace Models;

public record BuildServerOptions(DirectoryInfo? LocalFolder = null, Uri? RemoteGitUrl = null, FileInfo? GitPatchFile = null, Uri? RemotePatchFile = null)
{
    public Uri? RemoteGitUrl { get; set; } = RemoteGitUrl;
    public DirectoryInfo? LocalFolder { get; set; } = LocalFolder;

    /// <summary>
    /// Whether the build may install missing toolchains on the host. Off unless
    /// <c>--install-prerequisites</c> is passed — a build should not change the machine by accident.
    /// </summary>
    public bool InstallMissingTools { get; set; }
}