
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime.InteropServices;

namespace slidegridx;

public static class Slideshow
{
    private static List<SlideWindow> Windows = new();

    // when true, a command goes to all grid windows
    private static bool AllWindows = false;
    
    public static void Play()
    {
        SortLists();
        
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // GLFW isn't compatible with Wayland, use X11 or XWayland
            GLFW.InitHint(InitHintPlatform.Platform, Platform.X11);

            var env = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? string.Empty;
            if (env.ToLowerInvariant().Equals("wayland")) Console.WriteLine("wayland may be unreliable; if images are blank, try X11");
        }

        if (!GLFW.Init())
        {
            Console.WriteLine("failed to initialize GLFW");
            Environment.Exit(1);
        }

        // create a window for each grid location
        foreach (var grid in Config.Grids)
        {
            var win = new SlideWindow(grid);
            if (win.Window is null)
            {
                Console.WriteLine($"failed to create window for grid #{grid.Id}: ({grid.X},{grid.Y})-({grid.W},{grid.H})");
                Dispose();
                Environment.Exit(1);
            }
            Windows.Add(win);
        }

        var running = true;
        while (running)
        {
            // gather input
            foreach (var win in Windows)
            {
                win.Window.NewInputFrame();
            }
            NativeWindow.ProcessWindowEvents(waitForEvents: false);

            // process inputs
            foreach (var win in Windows)
            {
                // exiting?
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Escape))
                {
                    running = false;
                    break;
                }
                
                // toggle all windows? (tilde / backtick / reverse-apostrophe key)
                if (win.Window.KeyboardState.IsKeyReleased(Keys.GraveAccent))
                {
                    AllWindows = !AllWindows;
                    break;
                }
                
                // manual advance toggle
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Space))
                {
                    if (AllWindows)
                    {
                        foreach (var w in Windows) w.ToggleManualAdvance();
                    }
                    else
                    {
                        win.ToggleManualAdvance();
                    }
                    break;
                }
                
                // highlights-only toggle
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Enter))
                {
                    if (AllWindows)
                    {
                        foreach (var w in Windows) w.ToggleHighlightsOnly();
                    }
                    else
                    {
                        win.ToggleHighlightsOnly();
                    }
                    break;
                }
                
                // previous
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Left) 
                    || win.Window.MouseState.IsButtonReleased(MouseButton.Left)
                    || win.Window.MouseState.ScrollDelta.Y < 0)
                {
                    if (AllWindows)
                    {
                        foreach (var w in Windows) w.Previous();
                    }
                    else
                    {
                        win.Previous();
                    }
                    break;
                }
                
                // next
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Right) 
                    || win.Window.MouseState.IsButtonReleased(MouseButton.Right)
                    || win.Window.MouseState.ScrollDelta.Y > 0)
                {
                    if (AllWindows)
                    {
                        foreach (var w in Windows) w.Next();
                    }
                    else
                    {
                        win.Next();
                    }
                    break;
                }
            }
            if (!running) continue;
            
            // auto-advance
            foreach (var win in Windows)
            {
                if (DateTime.Now >= win.AutoAdvanceTime) win.Next();
            }
        }

        Dispose();
    }

    public static void SortLists()
    {
        if (Config.RandomizeMode == PlaybackRandomizeMode.Shuffle) return;
        SortImageData(Config.Content);
        SortImageData(Config.Highlights);
    }
    
    private static void SortImageData(List<ImageData> dataList)
    {
        switch(Config.SequenceMode)
        {
            case PlaybackSequenceMode.ByFilename:
                dataList = dataList.OrderBy(i => i.Pathname).ToList();
                break;

            case PlaybackSequenceMode.ByTimestamp:
                dataList = dataList.OrderBy(i => i.Timestamp).ToList();
                break;
        }
    }

    private static void Dispose()
    {
        foreach (var win in Windows)
        {
            if (win.Window.Exists) win.Window.Close();
            win.Window.Dispose();
        }
        Windows.Clear();
        GLFW.Terminate();        
    }
}