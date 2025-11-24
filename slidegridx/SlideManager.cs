
using PipeMethodCalls;

namespace slidegridx;
        
public class SlideManager
{
    public Grid ForGrid { get; private set; }

    public DateTime AutoAdvanceTime = DateTime.MaxValue;
    
    private Random random = new();
    private List<int> PlaybackSequence = new();
    private bool AutoAdvance = false;
    private bool HighlightsOnly = false;

    private int PlaybackIndex;

    public SlideManager(Grid grid)
    {
        ForGrid = grid;
        AutoAdvance = (ForGrid.AdvanceMode == GridAdvanceMode.Automatic);
        GetPlaybackSequence();
        PlaybackIndex = 0;
        // caller: await ReloadAll(true) after ctor
    }

    public async Task Next()
    {
        SetNextAdvanceTime();
        await ShowSlide(+1);
    }

    public async Task Previous()
    {
        SetNextAdvanceTime();
        await ShowSlide(-1);
    }

    public void ToggleManualAdvance()
    {
        AutoAdvance = !AutoAdvance;
        SetNextAdvanceTime();
    }

    public async Task ToggleHighlightsOnly()
    {
        HighlightsOnly = !HighlightsOnly;
        await ReloadAll();
    }

    public async Task ReloadAll(bool setNextAdvanceTime = false)
    {
        // To attach a debugger to a child process, set a breakpoint here.
        // This is the first time a call is made to any child process.
        // In Rider, choose Run -> Attach to process, and find by command line.
        // Set your breakpoint in the child process code (such as SetVisible in
        // ChildProcessProxy, then switch back to this debugger and resume.
        
        var pathname = ResolvePathname(PlaybackIndex);
        await ForGrid.ParentIPC.InvokeAsync(child => child.SetVisible(pathname));

        var index = PlaybackIndex;
        AdvanceIndex(ref index, -1);
        pathname = ResolvePathname(PlaybackIndex);
        await ForGrid.ParentIPC.InvokeAsync(child => child.SetPrev(pathname));

        index = PlaybackIndex;
        AdvanceIndex(ref index, +1);
        pathname = ResolvePathname(PlaybackIndex);
        await ForGrid.ParentIPC.InvokeAsync(child => child.SetNext(pathname));

        if (setNextAdvanceTime) SetNextAdvanceTime();
    }

    private void GetPlaybackSequence()
    {
        // Each slide stores its own separate playback sequence which the show references.
        // The entire playlist series is pre-determined. The PlaybackSequence list of
        // integers identifies the Content index (positive) or Highlights index (negative)
        // to be played. This way, it is possible to manually step forwards and backwards
        // through the entire sequence at any time, and also to toggle between content-only
        // and highlight-only display modes. Because there is no "negative zero" which would
        // make it impossible to reference Highlights[0] in this scheme, int.MinValue is used
        // to represent that special case.
        
        // All Content items are guaranteed to be included. For short Content lists, some or
        // even none of the Highlights may appear as those sequences are added randomly
        // according to the HighlightFreq percentage-chance setting.

        var seqlen = (Config.RandomizeMode != PlaybackRandomizeMode.Shuffle) ? Config.SequenceLength : 1;
        var contentIDs = Enumerable.Range(0, Config.Content.Count).ToList();
        var highlightIDs = Config.Highlights.Count > 0 ? Enumerable.Range(0, Config.Highlights.Count).ToList() : new List<int>(1);
        PlaybackSequence.Clear();
        
        while (contentIDs.Count > 0)
        {
            List<int> target;
            int multiplier;
            
            // decide if we're adding Highlight index values or Content index values
            if(contentIDs.Count == 0 || (highlightIDs.Count > 0 && random.Next(1,101) <= Config.HighlightFrequency))
            {
                multiplier = -1;
                target = highlightIDs;
            }
            else
            {
                multiplier = 1;
                target = contentIDs;
            }

            // add indexes to the playlist according to the sequence length 
            var index = random.Next(target.Count);
            var countdown = random.Next(1, seqlen + 1) + 2;

            while (countdown > 0 && index < target.Count)
            {
                // highlights might always be randomized (no sequencing)
                if (multiplier == -1 && Config.HighlightMode == HighlightPlayback.AlwaysShuffle)
                {
                    index = random.Next(target.Count);
                }

                var storedIndex = target[index] * multiplier;

                // special case for Highlights[0] since "negative zero" isn't possible
                if (storedIndex == 0 && multiplier == -1) storedIndex = int.MinValue;

                PlaybackSequence.Add(storedIndex);

                // by removing the selected entry, the next item "moves into" the [index] slot
                // making it the next one added in the sequence in the next pass (unless randomized)
                target.RemoveAt(index);

                countdown--;
            }

            // highlights should never "run out"
            if (Config.Highlights.Count > 0 && highlightIDs.Count == 0)
            {
                highlightIDs = Enumerable.Range(0, Config.Highlights.Count).ToList();
            }
        }        
    }

    private string ResolvePathname(int index)
    {
        if (PlaybackSequence[index] > -1)
        {
            return Config.Content[PlaybackSequence[index]].Pathname;
        }

        // special case for Highlights[0] since "negative zero" isn't possible
        var i = PlaybackSequence[index] > int.MinValue ? PlaybackSequence[index] * -1 : 0;
        return Config.Highlights[i].Pathname;    
    }
    
    private void AdvanceIndex(ref int index, int advance)
    {
        // when highlights-only mode is active, the current index is randomly selected,
        // then it skips forward until it finds a highlight entry; note this means you can't
        // manually move forward and backward in this mode
        if(HighlightsOnly)
        {
            var nodupe = index;
            index = random.Next(PlaybackSequence.Count);
            do
            {
                index += 1;
                WrapIndex(ref index);
            } while (PlaybackSequence[index] > -1 && index != nodupe);
        }
        // otherwise we're just incrementing forward or backwards (or not at all)
        else
        {
            index += advance;
            WrapIndex(ref index);
        }
    }
    
    private void WrapIndex(ref int index)
    {
        if (index < 0) index = PlaybackSequence.Count - 1;
        if (index == PlaybackSequence.Count) index = 0;
    }
    
    private void SetNextAdvanceTime()
    {
        if (!AutoAdvance)
        {
            AutoAdvanceTime = DateTime.MaxValue;
            return;
        }

        double stagger = (Config.StaggerMode == PlaybackStaggerMode.Staggered) ? random.Next(1000) - 500 : 0;
        AutoAdvanceTime = DateTime.Now.AddSeconds(Config.ShuffleTime + stagger);
    }

    private async Task ShowSlide(int direction = 0, bool changeHighlightsMode = false)
    {
        // advance should be 0 when changing highlights mode, but index can
        // still change if the current index is not already a highlight image

        AdvanceIndex(ref PlaybackIndex, direction);

        if (!changeHighlightsMode)
        {
            if (direction == +1)
            {
                //SlidePrev = SlideVisible;
                //SlideVisible = SlideNext;
                var index = PlaybackIndex;
                AdvanceIndex(ref index, direction);
                //SlideNext = LoadImage(ResolvePathname(index));
                await ForGrid.ParentIPC.InvokeAsync(child => child.Advance(direction, ResolvePathname(index)));
            }

            if (direction == -1)
            {
                //SlideNext = SlideVisible;
                //SlideVisible = SlidePrev;
                var index = PlaybackIndex;
                AdvanceIndex(ref index, direction);
                //SlidePrev = LoadImage(ResolvePathname(index));
                await ForGrid.ParentIPC.InvokeAsync(child => child.Advance(direction, ResolvePathname(index)));
            }

            // should render
        }
        else
        {
            // reload all three when changing highlights mode
            await ReloadAll();
        }
    }
}