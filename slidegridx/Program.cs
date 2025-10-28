

namespace slidegridx;

public class Program
{
    public static void Main(string[] args)
    {
        if (args.Length != 1)
        {
            Console.WriteLine("Usage: slidegridx.exe file.sgx\nSee sample.sgx for details.");
            Environment.Exit(0);
        }
        
        Console.WriteLine($"loading {args[0]}");
        
        var result = Config.ReadConfig(args[0]);
        if (result is not null)
        {
            Console.WriteLine("slidegridx failed to load the requested file:");
            Console.WriteLine(result);
            Environment.Exit(1);
        }
        
        Console.WriteLine($"read {Config.Content.Count} content images and {Config.Highlights.Count} highlights");
        Console.WriteLine(@"

slidegridx playback starting... controls:

ESC    Exit
LEFT   Previous (also left mouse and scrollwheel up)
RIGHT  Next (also right mouse and scrollwheel down)
~      Commands to all grids (click to select one grid)
SPACE  Manual advance
ENTER  Toggle highlight-only

");

        Slideshow.Play();
    }
}
