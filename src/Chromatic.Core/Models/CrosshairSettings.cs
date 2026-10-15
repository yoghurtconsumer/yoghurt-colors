namespace Chromatic.Core.Models;

public enum CrosshairShape { Cross, Dot, Circle, T, DotCross, Picture }
public sealed record CrosshairSettings(bool Enabled = false, CrosshairShape Shape = CrosshairShape.Cross,
    int Size = 16, int Gap = 4, int Thickness = 2, int Outline = 1,
    string Color = "#FFFFFFFF", double Opacity = 1, int OffsetX = 0, int OffsetY = 0,
    string? AssetHash = null);
