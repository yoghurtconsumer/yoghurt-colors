using Chromatic.Core.State;
using Chromatic.Infrastructure.Ipc;

// Manual only. All writes go through the ordinary Agent/journal/recovery path.
if (args.Length != 2 || !args.Contains("--hardware") || !args.Contains("--acknowledge-display-changes") || Console.IsInputRedirected)
{
    Console.Error.WriteLine("No hardware test was run. Manual-only: use --hardware --acknowledge-display-changes in an interactive console.");
    return 2;
}
Console.WriteLine("This manual test applies 110% global saturation for 10 seconds, then restores the captured baseline.");
Console.WriteLine("Close Chromatic first. HDR/unknown SDR or an unfinished journal blocks the test. Type APPLY to proceed:");
if (Console.ReadLine() != "APPLY") return 2;
using var cancellation = new CancellationTokenSource();
Console.CancelKeyPress += (_, e) => { e.Cancel = true; cancellation.Cancel(); };
using var session = new CompanionSession();
var exitCode = 0;
try
{
    for (var seconds = 5; seconds > 0; seconds--) { Console.WriteLine($"Applying in {seconds}… Ctrl+C cancels."); await Task.Delay(1000, cancellation.Token); }
    await session.StartAsync(new(), _ => false);
    if (!session.Connected) throw new InvalidOperationException(session.Failure ?? "Agent unavailable.");
    async Task Send(EffectsCommand command)
    {
        var state = session.State;
        var ack = await session.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, command), cancellation.Token);
        Console.WriteLine($"{ack.Completion.Backend}: {ack.Completion.ErrorCode ?? "Completed"} {ack.Completion.Explanation}");
        if (!ack.Completion.Success) throw new InvalidOperationException("Hardware operation was not confirmed.");
    }
    await Send(new SetColor(new(SaturationPercent: 110))); await Send(new Enable(true));
    for (var seconds = 10; seconds > 0; seconds--) { Console.WriteLine($"Automatic rollback in {seconds}…"); await Task.Delay(1000, cancellation.Token); }
}
catch (OperationCanceledException) { Console.WriteLine("Cancelled; requesting baseline restoration."); exitCode = 2; }
catch (Exception error) { Console.Error.WriteLine(error.Message); exitCode = 3; }
finally
{
    var state = session.State;
    var restore = await session.SendAsync(new(Guid.NewGuid(), state.Revision, state.Generation, new Shutdown()));
    Console.WriteLine($"Final restoration: {restore.Completion.Success} {restore.Completion.ErrorCode} {restore.Completion.Explanation}");
    if (!restore.Completion.Success) exitCode = 3;
    Console.WriteLine("API readback is not visual/capture qualification. Record OS/GPU/driver and baseline-return observations manually.");
}
return exitCode;
