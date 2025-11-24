
namespace slidegridx;

public class ChildProcessProxy : IChildProcess
{
    public void SetNext(string pathname)
        => ChildProcessManager.Window.SetNext(pathname);

    public void SetPrev(string pathname)
        => ChildProcessManager.Window.SetPrev(pathname);

    public void SetVisible(string pathname)
        => ChildProcessManager.Window.SetVisible(pathname);

    public void Advance(int direction, string pathname)
        => ChildProcessManager.Window.Advance(direction, pathname);
}
