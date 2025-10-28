
namespace slidegridx;

public enum GridAdvanceMode
{
    Automatic = 0,
    Manual = 1
}

public enum GridResizeMode
{
    LargestDimension = 0,
    ByWidth = 1,
    ByHeight = 2
}

public enum PlaybackRandomizeMode
{
    Shuffle = 0,
    Sequential = 1,
    SequentialRandomStart = 2,
    ShuffleSequences = 3
}

public enum PlaybackSequenceMode
{
    NotSequenced = 0,
    ByFilename = 1,
    ByTimestamp = 2
}

public enum PlaybackStaggerMode
{
    //NotShuffled = 0, used by Windows-based slidegrid GUI
    Synchronized = 1,
    Staggered = 2
}

public enum HighlightPlayback
{
    SameAsContent = 0,
    AlwaysShuffle = 1
}
