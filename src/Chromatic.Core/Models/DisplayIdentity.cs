namespace Chromatic.Core.Models;

public sealed record DisplayIdentity(string MonitorDevicePath, string? EdidSerial,
    string GdiName, long AdapterLuid, uint TargetId);
