using Fallout.Common;

namespace Tools;

public interface IBuildServer
{
    bool IsAvailable => true;
    Target Clean { get; }
    Target Restore { get; }
    Target Build { get; }
}