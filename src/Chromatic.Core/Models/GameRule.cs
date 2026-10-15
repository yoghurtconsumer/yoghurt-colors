namespace Chromatic.Core.Models;

public sealed record ExecutableIdentity(string NormalizedPath, int ProcessId, long ProcessStartTicks);
public sealed record GameRule(string Id, string ExecutablePath, string ProfileId, bool Enabled = false);
