
using OpenTK.Windowing.GraphicsLibraryFramework;
using PipeMethodCalls;
using PipeMethodCalls.MessagePack;

namespace slidegridx;

public static class ChildProcessManager
{
    // Since the parent process can send commands via IPC that require
    // a render, and IPC comes in through a background thread, this flag
    // is set to 1 (via Interlocked.Exchange) to tell the main thread
    // when to render the visible slide.
    public static int RenderRequired = 0;
    
    public static int GridNumber = -1;
    public static SlideWindow Window;

    private static Grid ForGrid;
    private static MainThreadSynchronizationContext SyncContext;
    
    // IMPORTANT
    // OpenGL and GLFW are not thread-safe. They MUST run from "the main thread"
    // which in this case is thread #1. Child processes must create and manage a
    // synchronization context. NO async code can run before MainLoop starts,
    // and ALL async code can ONLY run in the "async OffMainThread" method.
    public static Task Run()
    {
        ForGrid = Config.Grids[GridNumber];

        try
        {
            Console.WriteLine($"child {GridNumber} creating window from thread {Environment.CurrentManagedThreadId}");
            Window = new SlideWindow();

            SyncContext = new MainThreadSynchronizationContext();
            SynchronizationContext.SetSynchronizationContext(SyncContext);

            // Parent process will kill the child process; technically disposal
            // never happens, although the CLR actually performs cleanup
            while(true)
            {
                // Drain all queued continuations
                while (SyncContext.Queue.TryDequeue(out var work)) work.Item1(work.Item2);

                // Safe to invoke OpenGL or GLFW
                if (RenderRequired == 1)
                {
                    Console.WriteLine($"child {GridNumber} render {DateTime.Now:HH:mm:ss.ffff}");
                    Interlocked.Exchange(ref RenderRequired, 0);
                    Window.Render();
                    continue;
                }
                
                var (keyboard, mouse) = Window.CheckInputs();

                // Any async operations must be performed inside this method. Although it is
                // async, do not await it (which would deadlock). It is fire-and-forget.
                _ = OffMainThread(keyboard, mouse);
            } 
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            Window.Dispose();
            Window = null;
            
            ForGrid.ChildIPC.Dispose();
            ForGrid.ChildIPC = null;
        }
        
        return Task.CompletedTask;
    }

    // No async operation should be performed in MainLoop. They must be isolated here.
    private static async Task OffMainThread(KeyboardState keyboard, MouseState mouse)
    {
        if (ForGrid.ChildIPC == null)
        {
            var pipeName = $"sgx{GridNumber}";
            ForGrid.ChildIPC = new(new MessagePackPipeSerializer(), pipeName, () => new ChildProcessProxy());
            await ForGrid.ChildIPC.ConnectAsync();
        }
        
        await SendKeyIfReleased(keyboard, Keys.Escape);
        await SendKeyIfReleased(keyboard, Keys.Space);
        await SendKeyIfReleased(keyboard, Keys.GraveAccent);
        await SendKeyIfReleased(keyboard, Keys.Left);
        await SendKeyIfReleased(keyboard, Keys.Right);

        await SendMouseIfReleased(mouse, MouseButton.Left);
        await SendMouseIfReleased(mouse, MouseButton.Right);
        await SendMouseIfScrolled(mouse);
    }

    private static async Task SendKeyIfReleased(KeyboardState keyboardState, Keys key)
    {
        if (keyboardState.IsKeyReleased(key))
        {
            await ForGrid.ChildIPC.InvokeAsync(parent => parent.Keystroke(GridNumber, key));
        }
    }

    private static async Task SendMouseIfReleased(MouseState mouseState, MouseButton button)
    {
        if (mouseState.IsButtonReleased(button))
        {
            await ForGrid.ChildIPC.InvokeAsync(parent => parent.MouseClick(GridNumber, button));
        }
    }

    private static async Task SendMouseIfScrolled(MouseState mouseState)
    {
        if (mouseState.ScrollDelta.Y != 0)
        {
            await ForGrid.ChildIPC.InvokeAsync(parent => parent.MouseWheel(GridNumber, mouseState.ScrollDelta.Y));
        }
    }
}
