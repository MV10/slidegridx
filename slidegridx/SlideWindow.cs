
using System.Diagnostics;
using System.Runtime.InteropServices;
using OpenTK.Windowing.Common;
using OpenTK.Windowing.Desktop;
using OpenTK.Graphics.OpenGL;
using OpenTK.Mathematics;
using OpenTK.Windowing.GraphicsLibraryFramework;
using StbImageSharp;

namespace slidegridx;

// This is only created and used by a child process. Dispose
// is actually never called because the parent process simply
// kills the child process outright.

public class SlideWindow : IDisposable, IChildProcess
{
    private NativeWindow Window { get; set; }
    private Grid ForGrid { get; set; }

    private int ShaderHandle = -1;
    private int ElementBufferObject = -1;
    private int VertexBufferObject = -1;
    private int VertexArrayObject = -1;
    private int TextureHandle = -1;
    private int UniformSlide = -1;
    private int UniformResolution = -1;
    private int UniformImageSize = -1;
    private int UniformSizeMode = -1;

    private ImageResult SlideVisible;
    private ImageResult SlidePrev;
    private ImageResult SlideNext;

    private Vector2 Resolution;
    private Vector2 ImageSize;
    private int SizeMode;

    private static DebugProcKhr DebugMessageDelegate = OpenGLUtils.ErrorCallback;

    public SlideWindow()
    {
        Debug.WriteLine($"child window {ChildProcessManager.GridNumber} constructor running on thread {Environment.CurrentManagedThreadId}");
        ForGrid = Config.Grids[ChildProcessManager.GridNumber];
        
        var xy = new Vector2i(ForGrid.X, ForGrid.Y);
        var wh = new Vector2i(ForGrid.W, ForGrid.H);

        if(RuntimeInformation.IsOSPlatform(OSPlatform.Linux))
        {
            // NVIDIA driver has a known Wayland bug (textures are often blank), try to use X11 or XWayland
            GLFW.InitHint(InitHintPlatform.Platform, Platform.X11);

            var env = Environment.GetEnvironmentVariable("XDG_SESSION_TYPE") ?? string.Empty;
            if (env.ToLowerInvariant().Equals("wayland")) Console.WriteLine("wayland may be unreliable; if images are blank, try X11");
        }

        // the application switches context frequently, which is normally slow; setting
        // KHR_context_flush_control to None should significantly improve response time
        GLFW.WindowHint(WindowHintReleaseBehavior.ContextReleaseBehavior, ReleaseBehavior.None);

        // Since console programs don't have a SynchronizationContext, the use of await prior to
        // this point means that we're most likely not on the main thread (managed thread ID #1).
        //GLFWProvider.CheckForMainThread = false; // only applies to OpenTK's GameWindow?

        if (!GLFW.Init())
        {
            Console.WriteLine("failed to initialize GLFW");
            Environment.Exit(1);
        }
        
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
        Program.LinuxSleep();
        if (Window.Location != xy || Window.ClientSize != wh)
        {
            Window.AlwaysOnTop = true;
            Window.Location = xy;
            Window.ClientSize = wh;
            Window.AlwaysOnTop = false;

            Program.LinuxSleep();
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
    }

    public void SetVisible(string pathname)
    {
        if (IsDisposed) return;
        
        SlideVisible = LoadImage(pathname);

        Interlocked.Exchange(ref ChildProcessManager.RenderRequired, 1);
    }

    public void SetNext(string pathname)
    {
        if (IsDisposed) return;
        
        SlideNext = LoadImage(pathname);
    }

    public void SetPrev(string pathname)
    {
        if (IsDisposed) return;
        
        SlidePrev = LoadImage(pathname);
    }

    public void Advance(int direction, string pathname)
    {
        if (IsDisposed) return;
        
        if (direction == +1)
        {
            SlidePrev = SlideVisible;
            SlideVisible = SlideNext;
            SlideNext = LoadImage(pathname);
        }

        if (direction == -1)
        {
            SlideNext = SlideVisible;
            SlideVisible = SlidePrev;
            SlidePrev = LoadImage(pathname);
        }

        Interlocked.Exchange(ref ChildProcessManager.RenderRequired, 1);
    }

    public void Render()
    {
        if (IsDisposed || ShaderHandle == -1 || Window is null || !Window.Exists) return;

        GL.ClearColor(0.0f, 0.0f, 0.0f, 1.0f);
        GL.Clear(ClearBufferMask.ColorBufferBit);
        
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, TextureHandle);
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, SlideVisible.Width, SlideVisible.Height, 0, PixelFormat.Rgba, PixelType.UnsignedByte, SlideVisible.Data);

        GL.UseProgram(ShaderHandle);

        ImageSize = new Vector2(SlideVisible.Width, SlideVisible.Height);
        GL.Uniform1(UniformSlide, TextureUnit.Texture0.ToOrdinal());
        GL.Uniform2(UniformResolution, Resolution);
        GL.Uniform2(UniformImageSize, ImageSize);
        GL.Uniform1(UniformSizeMode, SizeMode);
        
        GL.BindVertexArray(VertexArrayObject);
        GL.DrawElements(PrimitiveType.Triangles, OpenGLUtils.Indices.Length, DrawElementsType.UnsignedInt, 0);
        
        Window.Context.SwapBuffers();
    }
    
    public (KeyboardState keyboardState, MouseState mouseState) CheckInputs()
    {
        Window.NewInputFrame();
        NativeWindow.ProcessWindowEvents(waitForEvents: false);
        return (Window.KeyboardState, Window.MouseState);
    }
    
    private void RenderInit()
    {
        Window.MakeCurrent();

        GL.UseProgram(ShaderHandle);
        
        // find the frag uniforms
        UniformSlide = GL.GetUniformLocation(ShaderHandle, "slide");
        UniformResolution = GL.GetUniformLocation(ShaderHandle, "resolution");
        UniformImageSize = GL.GetUniformLocation(ShaderHandle, "imagesize");
        UniformSizeMode = GL.GetUniformLocation(ShaderHandle, "sizemode");
        
        // prepare the vertex stage
        var locationVertices = GL.GetAttribLocation(ShaderHandle, "vertices");
        var locationTexCoords = GL.GetAttribLocation(ShaderHandle, "vertexTexCoords");
        (VertexArrayObject, VertexBufferObject, ElementBufferObject) = OpenGLUtils.InitializeVertices(ShaderHandle, locationVertices, locationTexCoords);
        
        // prepare a slide texture
        TextureHandle = OpenGLUtils.AllocateTexture();

        // make a blank current slide
        SlideVisible = new ImageResult
        {
            Width = 16,
            Height = 16,
            Data = new byte[16 * 16 * 4]
        };
        Render();
    }

    private ImageResult LoadImage(string pathname)
    {
        try
        {
            using var stream = File.OpenRead(pathname);
            var i= ImageResult.FromStream(stream, ColorComponents.RedGreenBlueAlpha);
            return i;
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
    
    public void Dispose()
    {
        if(IsDisposed) return;
        IsDisposed = true;
        
        if (VertexBufferObject != -1) GL.DeleteBuffer(VertexBufferObject);
        VertexArrayObject = -1;
        
        if (ElementBufferObject != -1) GL.DeleteBuffer(ElementBufferObject);
        ElementBufferObject = -1;
        
        if (VertexArrayObject != -1) GL.DeleteVertexArray(VertexArrayObject);
        VertexArrayObject = -1;
        
        if (TextureHandle != -1) GL.DeleteTexture(TextureHandle);
        TextureHandle = -1;
        
        if (ShaderHandle != -1) GL.DeleteProgram(ShaderHandle);
        ShaderHandle = -1;

        Window?.Close();
        Window?.Dispose();
        Window = null;
        
        GLFW.Terminate();        
    }
    private bool IsDisposed = false;
}
