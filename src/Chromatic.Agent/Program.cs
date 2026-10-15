namespace Chromatic.Agent;
internal static class Program
{
    private static async Task<int> Main()
    {
        try { return await AgentHost.RunAsync(await Chromatic.Infrastructure.Ipc.CompanionLaunch.ReadAsync()); }
        catch (Exception error)
        { Chromatic.Infrastructure.Ipc.CompanionLaunch.Log("Agent", error.ToString()); return 2; }
    }
}
