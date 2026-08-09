using System.IO;

namespace Models;

public record BuildServerOptions(DirectoryInfo? LocalFolder = null, Uri? RemoteGitUrl = null, FileInfo? GitPatchFile = null, Uri? RemotePatchFile = null)
{
    public Uri? RemoteGitUrl { get; set; } = RemoteGitUrl;
    public DirectoryInfo? LocalFolder { get; set; } = LocalFolder;
}