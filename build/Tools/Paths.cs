using Fallout.Common.IO;
using static Fallout.Common.FalloutBuild;

namespace Tools;

internal static class Paths
{
    static readonly AbsolutePath ServerArtifactsDirectory = RootDirectory / "Artifacts";
    static readonly AbsolutePath ServerOutputDirectory  = RootDirectory / "Output";
    
    public static AbsolutePath ArtifactsDirectoryFor(string serverName) => ServerArtifactsDirectory / serverName;
    public static AbsolutePath OutputDirectoryFor(string serverName) => ServerOutputDirectory / serverName;
}