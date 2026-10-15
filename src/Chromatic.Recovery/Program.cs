namespace Chromatic.Recovery;
internal static class Program
{
    private static async Task<int> Main()
    {
        try { return await RecoveryHost.RunAsync(await Chromatic.Infrastructure.Ipc.CompanionLaunch.ReadAsync()); }
        catch (Exception error)
        { Chromatic.Infrastructure.Ipc.CompanionLaunch.Log("Recovery", error.ToString()); return 2; }
    }
}
