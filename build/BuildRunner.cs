#nullable disable
using Fallout.Common;
using Servers.Seanime;
using Tools;


class BuildRunner : FalloutBuildServerBase, IBuildServer, IAsyncDisposable
{
    public static int Main() => Execute<BuildRunner>(x => x.Build);

    // TODO: Change to parameter based parameters for Seanime Server buiding
    readonly SeanimeServer SeanimeServer = new(OperatingSystem.Linux);

    public Target Build => _ => _
        .DependsOn(SeanimeServer.Build);
    
    
    // -----------------------------------
    // Ignore for now

    public Target Clean => _ => _;

    public Target Restore => _ => _;
    
    public async ValueTask DisposeAsync()
    {
        if (SeanimeServer != null)
            await SeanimeServer.DisposeAsync();
    }
}
