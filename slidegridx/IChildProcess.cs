
namespace slidegridx;

public interface IChildProcess
{
    public void SetNext(string pathname);
    public void SetPrev(string pathname);
    public void SetVisible(string pathname);
    public void Advance(int direction, string pathname);
}
