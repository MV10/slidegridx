
using OpenTK.Windowing.Desktop;
using OpenTK.Windowing.GraphicsLibraryFramework;
using System.Runtime.InteropServices;

namespace slidegridx;

public static class Slideshow
{
    private static List<SlideWindow> windows = new();
    
    public static void Play()
    {
        SortLists();
        
        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // GLFW isn't compatible with Wayland, use X11 or XWayland
            GLFW.InitHint(InitHintPlatform.Platform, Platform.X11);
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
            windows.Add(win);
        }

        var running = true;
        while (running)
        {
            // gather input
            foreach (var win in windows)
            {
                win.Window.NewInputFrame();
            }
            NativeWindow.ProcessWindowEvents(waitForEvents: false);

            // exiting?
            foreach (var win in windows)
            {
                if (win.Window.KeyboardState.IsKeyReleased(Keys.Escape))
                {
                    running = false;
                    break;
                }
            }
            if (!running) continue;

            // TODO process inputs
            
            // auto-advance
            foreach (var win in windows)
            {
                if (DateTime.Now >= win.NextAdvance) win.Advance();
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
        foreach (var win in windows)
        {
            if (win.Window.Exists) win.Window.Close();
            win.Window.Dispose();
        }
        windows.Clear();
        GLFW.Terminate();        
    }
}