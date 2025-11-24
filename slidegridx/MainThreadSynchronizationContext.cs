
using System.Collections.Concurrent;

namespace slidegridx;

internal sealed class MainThreadSynchronizationContext : SynchronizationContext
{
    private readonly Thread MainThread = Thread.CurrentThread;
    public readonly ConcurrentQueue<(SendOrPostCallback, object)> Queue = new();
    private readonly AutoResetEvent Event = new(false);

    public override void Post(SendOrPostCallback callbackDelegate, object state)
    {
        Queue.Enqueue((callbackDelegate, state));
        Event.Set();
    }

    public override void Send(SendOrPostCallback callbackDelegate, object state)
    {
        if (Thread.CurrentThread == MainThread)
        {
            callbackDelegate(state);
        }
        else
        {
            var reset = new ManualResetEventSlim(false);
            Post(s =>
            {
                try
                {
                    callbackDelegate(s);
                }
                finally
                {
                    reset.Set();
                }
            }, state);
            reset.Wait();
        }
    }

    public void Complete() 
        => Event.Set();
    
    public void Stop() 
        => Event.Close();
}