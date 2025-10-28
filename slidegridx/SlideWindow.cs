
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using StbImageSharp;

namespace slidegridx;
        
// These are the same algorithms used in the Windows GUI-based slidegrid.

public class SlideWindow : IDisposable
{
    public NativeWindow Window { get; private set; }
    public Grid ForGrid { get; private set; }
    public DateTime NextAdvance = DateTime.MaxValue;

    private int ShaderHandle = -1;
    private int TextureHandle = -1;
    private int UniformSlide = -1;
    private int UniformResolution = -1;
    private int UniformImageSize = -1;
    private int UniformSizeMode = -1;
    
    private Random random = new();
    private List<int> PlaybackSequence = new();
    private bool AutoAdvance = false;
    private bool HighlightsOnly = false;

    private ImageResult Slide;
    private ImageResult SlidePrev;
    private ImageResult SlideNext;
    private int PlaybackIndex;

    private Vector2 Resolution;
    private Vector2 ImageSize;
    private int SizeMode;

    private static DebugProcKhr DebugMessageDelegate = OpenGLUtils.ErrorCallback;
    
    public SlideWindow(Grid grid)
    {
        ForGrid = grid;
        
        var xy = new Vector2i(ForGrid.X, ForGrid.Y);
        var wh = new Vector2i(ForGrid.W, ForGrid.H);
        
        var settings = new NativeWindowSettings
        {
            Location = xy,
            ClientSize = wh,
            WindowBorder = WindowBorder.Hidden,
            API = ContextAPI.OpenGL,
            APIVersion = new Version(4, 5),
            Profile = ContextProfile.Core,
            Flags = ContextFlags.Debug,
        };
        Window = new NativeWindow(settings);

        // When testing in KDE Plasma, the window manager won't allow overlap with the taskbars
        // and that triggers some default size and location. If we set AlwaysOnTop then reapply
        // the location and size, it seems to "stick" and we can then disable AlwaysOnTop.
        if (Window.Location != xy || Window.ClientSize != wh)
        {
            Window.AlwaysOnTop = true;
            Window.Location = xy;
            Window.ClientSize = wh;
            Window.AlwaysOnTop = false;
            if (Window.Location != xy || Window.ClientSize != wh) Console.WriteLine($"system changed grid #{ForGrid.Id} from {xy}-{wh} to {Window.Location}-{Window.ClientSize}");
        }

        ShaderHandle = OpenGLUtils.CompileShader();
        if (ShaderHandle == -1)
        {
            Console.WriteLine("shader compile failed");
            Dispose();
            return;
        }

        RenderInit();
        
        Resolution = Window.ClientSize;
        SizeMode = (int)ForGrid.ResizeMode;
        AutoAdvance = (ForGrid.AdvanceMode == GridAdvanceMode.Automatic);

        GetPlaybackSequence();
        PlaybackIndex = 0;
        ReloadAll();
        SetNextAdvanceTime();
    }

    public void Advance()
    {
        ShowSlide(+1);
        SetNextAdvanceTime();
    }

    private void GetPlaybackSequence()
    {
        // Each window stores its own separate playback sequence which the show references.
        // The entire playlist series is pre-determined. The PlaybackSequence list of
        // integers identifies the Content index (positive) or Highlights index (negative)
        // to be played. This way, it is possible to manually step forwards and backwards
        // through the entire sequence at any time, and also to toggle between content-only
        // and highlight-only display modes. Because there is no "negative zero" which would
        // make it impossible to reference Highlights[0] in this scheme, int.MinValue is used
        // to represent that special case.

        var seqlen = (Config.RandomizeMode != PlaybackRandomizeMode.Shuffle) ? Config.SequenceLength : 1;
        var contentIDs = Enumerable.Range(0, Config.Content.Count).ToList();
        var highlightIDs = Config.Highlights.Count > 0 ? Enumerable.Range(0, Config.Highlights.Count).ToList() : new List<int>(1);
        PlaybackSequence.Clear();
        
        while (contentIDs.Count > 0)
        {
            List<int> target;
            int multiplier = 1;
            
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

                // special case for Higlights[0] since "negative zero" isn't possible
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

    private void ReloadAll()
    {
        Slide = LoadImage(ResolvePathname(PlaybackIndex));

        var index = PlaybackIndex;
        AdvanceIndex(ref index, -1);
        SlidePrev = LoadImage(ResolvePathname(index));

        index = PlaybackIndex;
        AdvanceIndex(ref index, +1);
        SlideNext = LoadImage(ResolvePathname(index));

        Render();
    }

    private ImageResult LoadImage(string pathname)
    {
        try
        {
            using var stream = File.OpenRead(pathname);
            return ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
        }
        catch
        {
            Console.WriteLine($"grid {ForGrid.Id} failed to load {pathname}");
            return new ImageResult
            {
                Width = 16,
                Height = 16,
                Data = new byte[16 * 16 * 4]
            };
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
            NextAdvance = DateTime.MaxValue;
            return;
        }

        double stagger = (Config.StaggerMode == PlaybackStaggerMode.Staggered) ? random.Next(1000) - 500 : 0;
        NextAdvance = DateTime.Now.AddSeconds(Config.ShuffleTime + stagger);
    }

    private void ShowSlide(int advance = 0, bool changeHighlightsMode = false)
    {
        // advance should be 0 when changing highlights mode, but index can
        // still change if the current index is not already a highlight image

        AdvanceIndex(ref PlaybackIndex, advance);

        if (!changeHighlightsMode)
        {
            if (advance == +1) SlidePrev = Slide;
            if (advance == -1) SlideNext = Slide;

            if (advance == +1) Slide = SlideNext;
            if (advance == -1) Slide = SlidePrev;

            if (advance != 0)
            {
                var index = PlaybackIndex;
                AdvanceIndex(ref index, advance);
                if (advance == +1) SlideNext = LoadImage(ResolvePathname(index));
                if (advance == -1) SlidePrev = LoadImage(ResolvePathname(index));
            }

            Render();
        }
        else
        {
            // reload all three when changing highlights mode
            ReloadAll();
        }
    }

    private void RenderInit()
    {
        GL.UseProgram(ShaderHandle);
        
        // find the frag uniforms
        UniformSlide = GL.GetUniformLocation(ShaderHandle, "slide");
        UniformResolution = GL.GetUniformLocation(ShaderHandle, "resolution");
        UniformImageSize = GL.GetUniformLocation(ShaderHandle, "imagesize");
        UniformSizeMode = GL.GetUniformLocation(ShaderHandle, "sizemode");
        
        // prepare the vertex stage
        var locationVertices = GL.GetAttribLocation(ShaderHandle, "vertices");
        var locationTexCoords = GL.GetAttribLocation(ShaderHandle, "vertexTexCoords");
        OpenGLUtils.InitializeVertices(ShaderHandle, locationVertices, locationTexCoords);
        
        // prepare a slide texture
        TextureHandle = OpenGLUtils.AllocateTexture();
    }

    private void Render()
    {
        if (ShaderHandle == -1) return;

        Window.MakeCurrent();
        
        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        
        GL.UseProgram(ShaderHandle);

        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, Slide.Width, Slide.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, Slide.Data);

        ImageSize = new Vector2(Slide.Width, Slide.Height);
        GL.Uniform1(UniformSlide, TextureUnit.Texture0.ToOrdinal());
        GL.Uniform2(UniformResolution, Resolution);
        GL.Uniform2(UniformImageSize, ImageSize);
        GL.Uniform1(UniformSizeMode, SizeMode);
        
        GL.BindVertexArray(OpenGLUtils.VertexArrayObject);
        GL.DrawElements(PrimitiveType.Triangles, OpenGLUtils.Indices.Length, DrawElementsType.UnsignedInt, 0);
        
        Window.Context.SwapBuffers();
    }
    
    public void Dispose()
    {
        if (TextureHandle != -1) GL.DeleteTexture(TextureHandle);
        TextureHandle = -1;
        
        if (ShaderHandle != -1) GL.DeleteProgram(ShaderHandle);
        ShaderHandle = -1;
        
        Window?.Dispose();
        Window = null;
    }
}