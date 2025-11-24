
using PipeMethodCalls.MessagePack;
using System.Diagnostics;
using System.Runtime.InteropServices;

namespace slidegridx;

public class Program
{
    // The help shows there is one command-line argument, the .sgx configuration file.
    // However, if a second argument is provided, the program starts as a window-grid
    // child process. The second argument is the zero-based grid number defined in config.
    // The parent process orchestrates the child processes until exit. 

    public static ParentProcessManager ParentProcess;
    
    public static async Task Main(string[] args)
    {
        if (args.Length < 1 
            || args.Length > 2
            || args[0].Equals("-h", StringComparison.CurrentCultureIgnoreCase)
            || args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase)
            || args[0].Equals("-help", StringComparison.CurrentCultureIgnoreCase)
            || args[0].Equals("--help", StringComparison.CurrentCultureIgnoreCase)
            || args[0].Equals("-?", StringComparison.CurrentCultureIgnoreCase)
            || args[0].Equals("--?", StringComparison.CurrentCultureIgnoreCase)
            || (args.Length == 2 && !int.TryParse(args[1], out ChildProcessManager.GridNumber)))
        {
            Console.WriteLine("Usage: slidegridx.exe file.sgx\nSee sample.sgx for details.");
            Environment.Exit(0);
        }
        
        var who = (ChildProcessManager.GridNumber > -1) ? $"slide {ChildProcessManager.GridNumber}" : "controller ";
        Console.WriteLine($"{who} loading {args[0]}");
        
        var result = Config.ReadConfig(args[0]);
        if (result is not null)
        {
            Console.WriteLine("slidegridx failed to load the requested file:");
            Console.WriteLine(result);
            Environment.Exit(1);
        }

        // If this is set, this process is a child and only needs config.
        if (ChildProcessManager.GridNumber > -1)
        {
            await ChildProcessManager.Run();
            Environment.Exit(0);
        }

        // Otherwise this process is the parent and needs to start the show.
        Console.WriteLine($"read {Config.Content.Count} content images and {Config.Highlights.Count} highlights");
        Console.WriteLine(@"

slidegridx playback starting:

ESC    Exit
LEFT   Previous (also left mouse and scrollwheel up)
RIGHT  Next (also right mouse and scrollwheel down)
~      Commands to all grids (click to select one grid)
SPACE  Manual advance
ENTER  Toggle highlight-only

");

        try
        {
            // Controls everything, the child apps are just display-handlers
            ParentProcess = new ParentProcessManager();

            // Create a pipe server for each child process
            List<Task> connections = new(Config.Grids.Count);
            for (int i = 0; i < Config.Grids.Count; i++)
            {
                var pipeName = $"sgx{i}";
                Config.Grids[i].ParentIPC = new(new MessagePackPipeSerializer(), pipeName, () => new ParentProcessProxy());
                connections.Add(Config.Grids[i].ParentIPC.WaitForConnectionAsync());
            }

            // Create the child processes
            for (int i = 0; i < Config.Grids.Count; i++)
            {
                var info = new ProcessStartInfo
                {
                    FileName = Environment.ProcessPath!,
                    ArgumentList = { args[0], i.ToString() },
                    CreateNoWindow = true,
                };
                Config.Grids[i].ChildProcess = Process.Start(info);
            }

            // Each child connects after the window is ready to use
            await Task.WhenAll(connections);
            connections = null;

            // Start the show
            await ParentProcess.Run();
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex);
        }
        finally
        {
            // The party's over
            foreach (var grid in Config.Grids)
            {
                grid.ParentIPC.Dispose();
                grid.ParentIPC = null;

                grid.ChildProcess.Kill();
                grid.ChildProcess.Dispose();
                grid.ChildProcess = null;
            }
        }    
    }

    public static void LinuxSleep(int milliseconds = 100)
    {
        // On Linux the window compositor is asynchronous, so
        // not all window operations happen immediately, but
        // Win32 is synchronous so this is not necessary.
        if (RuntimeInformation.IsOSPlatform(OSPlatform.Linux)) Thread.Sleep(milliseconds);
    }
}
