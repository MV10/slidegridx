
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
    // and ALL async code can ONLY run in the "async OffMainThread" method. (Most
    // are actually fire-and-forget, but we must await the pipe connection.)
    public static Task Run()
    {
        ForGrid = Config.Grids[GridNumber];

        try
        {
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
                    //Console.WriteLine($"child {GridNumber} render {DateTime.Now:HH:mm:ss.ffff}");
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
        
        _ = SendKeyIfReleased(keyboard, Keys.Escape);         // quit
        _ = SendKeyIfReleased(keyboard, Keys.Space);          // toggle manual
        _ = SendKeyIfReleased(keyboard, Keys.Enter);          // toggle highlights
        _ = SendKeyIfReleased(keyboard, Keys.GraveAccent);    // toggle all-windows
        _ = SendKeyIfReleased(keyboard, Keys.Left);           // previous
        _ = SendKeyIfReleased(keyboard, Keys.Right);          // next

        _ = SendMouseIfReleased(mouse, MouseButton.Left);
        _ = SendMouseIfReleased(mouse, MouseButton.Right);
        _ = SendMouseIfScrolled(mouse);
    }

    private static Task SendKeyIfReleased(KeyboardState keyboardState, Keys key)
    {
        if (keyboardState.IsKeyReleased(key))
        {
            _ = ForGrid.ChildIPC.InvokeAsync(parent => parent.Keystroke(GridNumber, key));
        }
        return Task.CompletedTask;
    }

    private static Task SendMouseIfReleased(MouseState mouseState, MouseButton button)
    {
        if (mouseState.IsButtonReleased(button))
        {
            _ = ForGrid.ChildIPC.InvokeAsync(parent => parent.MouseClick(GridNumber, button));
        }
        return Task.CompletedTask;
    }

    private static Task SendMouseIfScrolled(MouseState mouseState)
    {
        if (mouseState.ScrollDelta.Y != 0)
        {
            _ = ForGrid.ChildIPC.InvokeAsync(parent => parent.MouseWheel(GridNumber, mouseState.ScrollDelta.Y));
        }
        return Task.CompletedTask;
    }
}
