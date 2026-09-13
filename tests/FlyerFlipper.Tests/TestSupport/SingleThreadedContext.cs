using System.Collections.Concurrent;

namespace FlyerFlipper.Tests.TestSupport;

/// <summary>
/// Runs an async test body on one thread with a message-pump <see cref="SynchronizationContext"/>,
/// like a UI thread, so UI-thread-affine services behave deterministically without Avalonia.
/// </summary>
internal sealed class SingleThreadedContext : SynchronizationContext
{
    private readonly BlockingCollection<(SendOrPostCallback Callback, object? State)> _queue = new();

    public override void Post(SendOrPostCallback d, object? state)
    {
        // Background work can still finish after the test body completed (or failed). Drop its
        // continuations instead of throwing on a thread-pool thread, which would crash the test host.
        try
        {
            _queue.TryAdd((d, state));
        }
        catch (InvalidOperationException) when (_queue.IsAddingCompleted)
        {
        }
    }

    public override void Send(SendOrPostCallback d, object? state) => throw new NotSupportedException();

    public static void Run(Func<Task> body)
    {
        var previous = Current;
        var context = new SingleThreadedContext();
        SetSynchronizationContext(context);
        try
        {
            var task = body();
            task.ContinueWith(_ => context._queue.CompleteAdding(), TaskScheduler.Default);

            foreach (var (callback, state) in context._queue.GetConsumingEnumerable())
            {
                callback(state);
            }

            task.GetAwaiter().GetResult();
        }
        finally
        {
            SetSynchronizationContext(previous);
        }
    }

    /// <summary>Yields to the pump until <paramref name="condition"/> holds.</summary>
    public static async Task WaitUntilAsync(Func<bool> condition, int timeoutMs = 5000)
    {
        var deadline = Environment.TickCount64 + timeoutMs;
        while (!condition())
        {
            if (Environment.TickCount64 > deadline)
            {
                throw new TimeoutException("Condition not met in time.");
            }

            await Task.Delay(5);
        }
    }
}
