namespace slidegridx;

public static class Config
{
    public static PlaybackRandomizeMode RandomizeMode { get; set; }
    public static PlaybackSequenceMode SequenceMode { get; set; }
    public static HighlightPlayback HighlightMode { get; set; }
    public static PlaybackStaggerMode StaggerMode { get; set; }
    public static double ShuffleTime { get; set; } = 10;
    public static int SequenceLength { get; set; } = 5;
    public static int HighlightFrequency { get; set; } = 5;

    public static List<Grid> Grids = new();
    public static List<ImageData> Content = new();
    public static List<ImageData> Highlights = new();

    private enum SectionNames
    {
        None, Playback, Grids, Content, Highlights
    }
    
    /// <summary>
    /// Returns null if the config is valid, otherwise returns an error message.
    /// </summary>
    public static string ReadConfig(string pathname)
    {
        if (!File.Exists(pathname)) return "File not found";

        var section = SectionNames.None;
        var lines = File.ReadAllLines(pathname);
        foreach (var line in lines)
        {
            var comment = line.IndexOf('#');
            var text = (comment >= 0) ? line.Substring(0, comment).Trim() : line.Trim();
            if (text.Length == 0) continue;

            if (text.StartsWith('['))
            {
                switch (text.ToLowerInvariant())
                {
                    case "[playback]":
                        section = SectionNames.Playback;
                        continue;

                    case "[grids]":
                        section = SectionNames.Grids;
                        continue;

                    case "[content]":
                        section = SectionNames.Content;
                        continue;

                    case "[highlights]":
                        section = SectionNames.Highlights;
                        continue;

                    default:
                        return $"Invalid section entry: {text}";
                }
            }

            switch (section)
            {
                case SectionNames.Playback:
                    var breaker = text.IndexOf(':');
                    if (breaker < 1 || breaker == text.Length - 1) return $"Invalid [Playback] entry: {text}";
                    var key = text.Substring(0, breaker).ToLowerInvariant();
                    var val = text.Substring(breaker + 1).Trim();
                    switch (key)
                    {
                       case "randomizemode":
                           RandomizeMode = val.ToEnum(PlaybackRandomizeMode.Shuffle);
                           break;
                       
                       case "sequencemode":
                           SequenceMode = val.ToEnum(PlaybackSequenceMode.NotSequenced);
                           break;
                       
                       case "highlightmode":
                           HighlightMode = val.ToEnum(HighlightPlayback.SameAsContent);
                           break;
                       
                       case "staggermode":
                           StaggerMode = val.ToEnum(PlaybackStaggerMode.Synchronized);
                           break;
                       
                       case "shuffletime":
                           ShuffleTime = val.ToDouble(10);
                           if (ShuffleTime <= 0) return $"Invalid [Playback] entry: {text}";
                           break;
                       
                       case "sequencelength":
                           SequenceLength = val.ToInt32(7);
                           if (SequenceLength < 1) return $"Invalid [Playback] entry: {text}";
                           break;
                       
                       case "highlightfreq":
                           HighlightFrequency = val.ToInt32(5);
                           if (HighlightFrequency is < 1 or > 99) return $"Invalid [Playback] entry: {text}";
                           break;
                       
                       default:
                           return $"Invalid [Playback] entry: {line}";
                    }
                    break;
                
                case SectionNames.Grids:
                    var slots = text.Split(',');
                    if(slots.Length is < 4 or > 6) return $"Invalid [Grids] entry: {text}";
                    var grid = new Grid()
                    {
                        Id = Grids.Count,
                        X = slots[0].ToInt32(-1),
                        Y = slots[1].ToInt32(-1),
                        W = slots[2].ToInt32(-1),
                        H = slots[3].ToInt32(-1),
                    };
                    if (grid.X == -1 || grid.Y == -1 || grid.W == -1 || grid.H == -1) return $"Invalid [Grids] entry #{grid.Id}: {text}";
                    if (slots.Length > 4) grid.AdvanceMode = slots[4].ToEnum(GridAdvanceMode.Automatic);
                    if (slots.Length > 5) grid.ResizeMode = slots[5].ToEnum(GridResizeMode.LargestDimension);
                    Grids.Add(grid);
                    break;
                
                case SectionNames.Content:
                    if(!Path.IsPathFullyQualified(text)) return $"Invalid [Content] entry: {text}";
                    AddImageData(Content, text);
                    break;
                
                case SectionNames.Highlights:
                    if(!Path.IsPathFullyQualified(text)) return $"Invalid [Content] entry: {text}";
                    AddImageData(Highlights, text);
                    break;
            }
        }

        if (Grids.Count == 0) return "Must define one or more display grids.";
        if (Content.Count == 0) return "Must define one or more content entries.";
        
        return null;
    }

    private static void AddImageData(List<ImageData> targetList, string pathname)
    {
        if (pathname.EndsWith('*'))
        {
            foreach(var file in new DirectoryInfo(pathname.Substring(0, pathname.Length - 1)).EnumerateFiles())
            {
                AddFile(targetList, file);
            }
        }
        else
        {
            if (File.Exists(pathname)) AddFile(targetList, new FileInfo(pathname));
        }
    }
    
    private static void AddFile(List<ImageData> targetList, FileInfo file)
    {
        if ((file.Attributes & FileAttributes.Hidden) == 0 && (file.Attributes & FileAttributes.System) == 0)
        {
            var ext = Path.GetExtension(file.Name);
            if (".jpg|.jpeg|.png|.bmp".Contains(ext, StringComparison.InvariantCultureIgnoreCase))
            {
                targetList.Add(new ImageData
                {
                    Pathname = file.FullName,
                    Timestamp = file.LastWriteTime
                });
            }
        }
    }
}
