# Project Rules for Codex
- Target OS: Windows 10/11 (x64)
- Stack: C# / .NET 8 WPF (Windows Desktop)
- Never use placeholder comments (e.g., // TODO). Write complete Win32 P/Invoke signatures and structs.
- Always ensure `MagUninitialize()` and `ChangeDisplaySettingsEx(null, 0)` can be triggered via a panic hotkey (F12) and on process exit so the screen never gets stuck.
