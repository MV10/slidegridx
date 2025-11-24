
using System.Diagnostics;
using PipeMethodCalls;

namespace slidegridx;

public class Grid
{
    // Assigned sequentially (corresponds to List<Grid> index)
    public int Id { get; set; }

    // From .sgx file
    public int X { get; set; }
    public int Y { get; set; }
    public int W { get; set; }
    public int H { get; set; }
    public GridAdvanceMode AdvanceMode { get; set; }
    public GridResizeMode ResizeMode { get; set; }
    
    // Parent process data
    public PipeServerWithCallback<IChildProcess, IParentProcess> ParentIPC;
    public Process ChildProcess;
    public SlideManager Slide;
    
    // Child process data
    public PipeClientWithCallback<IParentProcess, IChildProcess> ChildIPC;
}
