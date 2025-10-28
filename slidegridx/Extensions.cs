using OpenTK.Graphics.OpenGL;

namespace slidegridx;

public static class Extensions
{
    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static T ToEnum<T>(this string textValue, T defaultValue)
        where T : Enum
        => Enum.IsDefined(typeof(T), textValue)
            ? Enum.TryParse(typeof(T), textValue, true, out var parsed) ? (T)parsed : defaultValue
            : defaultValue;    
    
    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static int ToInt32(this string textValue, int defaultValue)
        => int.TryParse(textValue, out var parsed) ? parsed : defaultValue;

    /// <summary>
    /// String-conversion helper
    /// </summary>
    public static double ToDouble(this string textValue, double defaultValue)
        => double.TryParse(textValue, out var parsed) ? parsed : defaultValue; 
 
    /// <summary>
    /// TextureUnit-conversion helper
    /// </summary>
    public static int ToOrdinal(this TextureUnit textureUnit)
        => (int)textureUnit - (int)TextureUnit.Texture0;
}