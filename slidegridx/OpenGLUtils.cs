
using OpenTK.Graphics.OpenGL;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace slidegridx;

public static class OpenGLUtils
{
    // simple quad from two triangles that covers the whole display area
    public static readonly float[] Vertices =
    {
         // position          texture coords
        +1.0f, +1.0f, 0.0f,   1.0f, 1.0f,     // top right
        +1.0f, -1.0f, 0.0f,   1.0f, 0.0f,     // bottom right
        -1.0f, -1.0f, 0.0f,   0.0f, 0.0f,     // bottom left
        -1.0f, +1.0f, 0.0f,   0.0f, 1.0f      // top left
    };

    public static readonly uint[] Indices =
    {
        0, 1, 3,
        1, 2, 3
    };
    
    // These are widely recognized as unimportant "noise" messages when the OpenGL
    // Debug Message error callback is wired up. For example:
    // https://deccer.github.io/OpenGL-Getting-Started/02-debugging/02-debug-callback/
    private static readonly List<int> IgnoredErrorCallbackIDs =
    [
        0,              // gl{Push,Pop}DebugGroup calls
        131169, 131185, // NVIDIA buffer allocated to use video memory
        131218, 131204, // texture cannot be used for texture mapping
        131222, 131154, // NVIDIA pixel transfer is syncrhonized with 3D rendering
    ];

    public static int CompileShader()
    {
        int handle = -1;
        int vertId;
        int fragId;
        
        // load
        try
        {
            var vertCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"slide.vert"));
            var fragCode = File.ReadAllText(Path.Combine(AppContext.BaseDirectory,"slide.frag"));
            vertId = GL.CreateShader(ShaderType.VertexShader);
            GL.ShaderSource(vertId, vertCode);
            fragId = GL.CreateShader(ShaderType.FragmentShader);
            GL.ShaderSource(fragId, fragCode);
        }
        catch (Exception e)
        {
            Console.WriteLine($"load stage: {e.Message}");
            return -1;
        }

        try
        {
            // program
            handle = GL.CreateProgram();
            GL.AttachShader(handle, vertId);
            GL.AttachShader(handle, fragId);
        
            // compile
            try
            {
                GL.CompileShader(vertId);
                GL.GetShader(vertId, ShaderParameter.CompileStatus, out var vertOk);
                if (vertOk == 0) throw new Exception("vertex shader compile failed");
                GL.CompileShader(fragId);
                GL.GetShader(fragId, ShaderParameter.CompileStatus, out var fragOk);
                if (fragOk == 0) throw new Exception("fragment shader compile failed");
            }
            catch (Exception e)
            {
                Console.WriteLine($"compile stage: {e.Message}");
                GL.DeleteProgram(handle);
                return -1;
            }
        
            // link
            try
            {
                GL.LinkProgram(handle);
                GL.GetProgram(handle, GetProgramParameterName.LinkStatus, out var linkOk);
                if (linkOk == 0) throw new Exception("shader linker failed");
            }
            catch (Exception e)
            {
                Console.WriteLine($"link stage: {e.Message}");
                GL.DeleteProgram(handle);
                return -1;
            }
        }
        catch (Exception e)
        {
            Console.WriteLine($"create program stage: {e.Message}");
            return -1;
        }
        finally
        {
            // cleanup
            GL.DetachShader(handle, vertId);
            GL.DetachShader(handle, fragId);
            GL.DeleteShader(vertId);
            GL.DeleteShader(fragId);
        }

        return handle;
    }
    
    public static (int VAO, int VBO, int EBO) InitializeVertices(int shaderHandle, int locationVertices, int locationTexCoords)
    {
        var VertexArrayObject = GL.GenVertexArray();
        GL.BindVertexArray(VertexArrayObject);

        var VertexBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ArrayBuffer, VertexBufferObject);
        GL.BufferData(BufferTarget.ArrayBuffer, OpenGLUtils.Vertices.Length * sizeof(float), OpenGLUtils.Vertices, BufferUsageHint.StaticDraw);

        var ElementBufferObject = GL.GenBuffer();
        GL.BindBuffer(BufferTarget.ElementArrayBuffer, ElementBufferObject);
        GL.BufferData(BufferTarget.ElementArrayBuffer, OpenGLUtils.Indices.Length * sizeof(uint), OpenGLUtils.Indices, BufferUsageHint.StaticDraw);

        GL.UseProgram(shaderHandle);
        
        GL.EnableVertexAttribArray(locationVertices);
        GL.VertexAttribPointer(locationVertices, 3, VertexAttribPointerType.Float, false, 5 * sizeof(float), 0);

        GL.EnableVertexAttribArray(locationTexCoords);
        GL.VertexAttribPointer(locationTexCoords, 2, VertexAttribPointerType.Float, false, 5 * sizeof(float), 3 * sizeof(float));
        
        return (VertexArrayObject, VertexBufferObject, ElementBufferObject);
    }

    public static int AllocateTexture()
    {
        var handle = GL.GenTexture();
        GL.ActiveTexture(TextureUnit.Texture0);
        GL.BindTexture(TextureTarget.Texture2D, handle);
        
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapS, (int)TextureWrapMode.Clamp);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureWrapT, (int)TextureWrapMode.Clamp);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMinFilter, (int)TextureMinFilter.Linear);
        GL.TexParameter(TextureTarget.Texture2D, TextureParameterName.TextureMagFilter, (int)TextureMagFilter.Linear);

        // blank 16x16 placeholder
        GL.TexImage2D(TextureTarget.Texture2D, 0, PixelInternalFormat.Rgba, 16, 16, 0, PixelFormat.Rgba, PixelType.UnsignedByte, IntPtr.Zero);
        
        return handle;
    }

    internal static void ErrorCallback(
        DebugSource source,     // API, WINDOW_SYSTEM, SHADER_COMPILER, THIRD_PARTY, APPLICATION, OTHER
        DebugType type,         // ERROR, DEPRECATED_BEHAVIOR, UNDEFINED_BEHAVIOR, PORTABILITY, PERFORMANCE, MARKER, OTHER
        int id,                 // ID associated with the message (driver specific; see IgnoredErrorCallbackIDs list above)
        DebugSeverity severity, // NOTIFICATION, LOW, MEDIUM, HIGH ... (others defined too?)
        int length,             // length of the string in pMessage
        IntPtr pMessage,        // pointer to message string
        IntPtr pUserParam)      // not used here
    {
        if (IgnoredErrorCallbackIDs.Contains(id)) return;

        var message = Marshal.PtrToStringAnsi(pMessage, length);
        var errSource = source.ToString().Substring("DebugSource".Length);
        var errType = type.ToString().Substring("DebugType".Length);
        var errSev = severity.ToString().Substring("DebugSeverity".Length);
        var stack = new StackTrace(true).ToString();

        Console.WriteLine($"OpenGL Error:\n[{errSev}] source={errSource} type={errType} id={id}\n{message}\n{stack}");
        
        //Debugger.Break();
    }
}