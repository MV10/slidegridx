
using OpenTK.Windowing.GraphicsLibraryFramework;

namespace slidegridx;

// In theory this should implement disposal, but since shutdown
// implies process termination, it really doesn't matter. Since
// the child processes are independent, Task-based operations
// are generally fire-and-forget (not awaited).

public class ParentProcessManager : IParentProcess
{
    // when true, a command goes to all grid windows
    private bool AllWindows = false;
    
    private CancellationTokenSource cts = new();
    
    public async Task Run()
    {
        SortLists();
        
        // create slide management for each grid location
        foreach (var grid in Config.Grids)
        {
            grid.Slide = new SlideManager(grid);
        }
        
        // initialize graphics for each slide and window
        foreach (var grid in Config.Grids)
        {
            _ = grid.Slide.ReloadAll(setNextAdvanceTime: true);
        }

        // loop to check slide auto-advance timestamps
        while (!cts.IsCancellationRequested)
        {
            foreach (var grid in Config.Grids)
            {
                if (DateTime.Now >= grid.Slide.AutoAdvanceTime) _ = grid.Slide.Next();
            }

            await Task.Delay(1);
        }
    }

    public Task Keystroke(int gridId, Keys key)
    {
        // exit? abort the main loop, terminate all child processes
        if (key == Keys.Escape)
        {
            cts.Cancel();
            return Task.CompletedTask;
        }
        
        // toggle all windows? (tilde / backtick / reverse-apostrophe key)
        if (key == Keys.GraveAccent)
        {
            AllWindows = !AllWindows;
            return Task.CompletedTask;
        }
        
        // manual advance toggle
        if (key == Keys.Space)
        {
            if (AllWindows)
            {
                foreach (var g in Config.Grids) g.Slide.ToggleManualAdvance();
            }
            else
            {
                Config.Grids[gridId].Slide.ToggleManualAdvance();
            }
            return Task.CompletedTask;
        }
        
        // highlights-only toggle
        if (key == Keys.Enter)
        {
            if (AllWindows)
            {
                foreach (var g in Config.Grids) _ = g.Slide.ToggleHighlightsOnly();
            }
            else
            {
                _ = Config.Grids[gridId].Slide.ToggleHighlightsOnly();
            }
            return Task.CompletedTask;
        }

        // previous
        if (key == Keys.Left)
        {
            _ = SlideEventPrevious(gridId);
            return Task.CompletedTask;
        }

        // next
        if (key == Keys.Right)
        {
            _ = SlideEventNext(gridId);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public Task MouseClick(int gridId, MouseButton button)
    {
        // previous
        if (button == MouseButton.Left)
        {
            _ = SlideEventPrevious(gridId);
            return Task.CompletedTask;
        }

        // next
        if (button == MouseButton.Right)
        {
            _ = SlideEventNext(gridId);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    public Task MouseWheel(int gridId, float scrollDelta)
    {
        // previous
        if (scrollDelta < 0)
        {
            _ = SlideEventPrevious(gridId);
            return Task.CompletedTask;
        }

        // next
        if (scrollDelta > 0)
        {
            _ = SlideEventNext(gridId);
            return Task.CompletedTask;
        }

        return Task.CompletedTask;
    }

    private Task SlideEventNext(int gridId)
    {
        if (AllWindows)
        {
            foreach (var g in Config.Grids) _ = g.Slide.Next();
        }
        else
        {
            _ = Config.Grids[gridId].Slide.Next();
        }

        return Task.CompletedTask;
    }

    private Task SlideEventPrevious(int gridId)
    {
        if (AllWindows)
        {
            foreach (var g in Config.Grids) _ = g.Slide.Previous();
        }
        else
        {
            _ = Config.Grids[gridId].Slide.Previous();
        }

        return Task.CompletedTask;
    }
    
    private void SortLists()
    {
        if (Config.RandomizeMode == PlaybackRandomizeMode.Shuffle) return;
        SortImageData(Config.Content);
        SortImageData(Config.Highlights);
    }
    
    private void SortImageData(List<ImageData> dataList)
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
}