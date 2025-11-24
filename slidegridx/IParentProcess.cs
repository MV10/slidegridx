
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace slidegridx;

public interface IParentProcess
{
    public Task Keystroke(int gridID, Keys key);
    public Task MouseClick(int gridID, MouseButton button);
    public Task MouseWheel(int gridId, float scrollDelta);
}
