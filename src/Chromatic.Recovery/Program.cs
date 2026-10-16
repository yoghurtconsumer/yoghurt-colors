namespace Chromatic.Recovery;
internal static class Program
{
    private static async Task<int> Main(string[] args)
    {
        if (args is ["--startup-smoke-test"]) return Chromatic.Infrastructure.Ipc.CompanionLaunch.StartupSmoke("Recovery");
        try { return await RecoveryHost.RunAsync(await Chromatic.Infrastructure.Ipc.CompanionLaunch.ReadAsync()); }
        catch (Exception error)
        { Chromatic.Infrastructure.Ipc.CompanionLaunch.Log("Recovery", error.ToString()); return 2; }
    }
}
