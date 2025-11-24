
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace slidegridx;

public class ParentProcessProxy : IParentProcess
{
    public async Task Keystroke(int gridID, Keys key)
        => await Program.ParentProcess.Keystroke(gridID, key);

    public async Task MouseClick(int gridID, MouseButton button)
        => await Program.ParentProcess.MouseClick(gridID, button);

    public async Task MouseWheel(int gridId, float scrollDelta)
        => await Program.ParentProcess.MouseWheel(gridId, scrollDelta);
}
