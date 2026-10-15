using System.Windows.Threading;

namespace Chromatic.Agent;

public sealed class AgentStaWorker : IDisposable
{
    private readonly Thread _thread;
    private readonly Dispatcher _dispatcher;
    public AgentStaWorker()
    {
        var started = new TaskCompletionSource<Dispatcher>(TaskCreationOptions.RunContinuationsAsynchronously);
        _thread = new Thread(() =>
        {
            var dispatcher = Dispatcher.CurrentDispatcher;
            SynchronizationContext.SetSynchronizationContext(new DispatcherSynchronizationContext(dispatcher));
            started.SetResult(dispatcher);
            Dispatcher.Run();
        }) { IsBackground = true, Name = "Chromatic effects STA" };
        _thread.SetApartmentState(ApartmentState.STA); _thread.Start();
        _dispatcher = started.Task.GetAwaiter().GetResult();
    }
    public Task<T> InvokeAsync<T>(Func<Task<T>> operation, bool safetyPriority = false) =>
        _dispatcher.InvokeAsync(operation, safetyPriority ? DispatcherPriority.Send : DispatcherPriority.Normal).Task.Unwrap();
    public Task<T> InvokeAsync<T>(Func<T> operation, bool safetyPriority = false) =>
        _dispatcher.InvokeAsync(operation, safetyPriority ? DispatcherPriority.Send : DispatcherPriority.Normal).Task;
    public void Dispose()
    {
        _dispatcher.BeginInvokeShutdown(DispatcherPriority.Send);
        if (!_thread.Join(TimeSpan.FromSeconds(5))) throw new TimeoutException("Agent STA did not terminate; recovery supervisor must verify and handle a hung Agent.");
    }
}
