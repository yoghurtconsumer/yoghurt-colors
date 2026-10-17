# Stage 4, step 1 review — read-only inventory

The user authorized continued step-by-step work while testing a separate portable
copy. This checkpoint does not change that copy or run any physical display test.
Stage 4 as a whole remains incomplete. No mode/HDR/scaling/DDC setter is enabled.

## Review this build

Exit the older Chromatic before switching builds. On Resolution, click Refresh
monitors (read only), select a target and Load supported modes (read only).
Check the name, source/target IDs, negative positions if applicable, current exact
refresh and SDR/WCG/HDR description. Driver-enumerated integral refresh remains
separate from CCD current fractional refresh. Modes with incomplete required
fields, interlacing, private data or less than 32 bpp are omitted; a driver may
therefore show only the current observed mode.

Select an enumerated choice/scaling and Review mode plan. It must clearly say
Draft only / no display change and preserve the other active target metadata.
No arbitrary dimensions, custom timing, FOV control, Keep/Revert countdown or
working Apply is claimed. Cloned shared sources refuse draft planning. A topology
change/resume invalidates the Agent snapshot; Refresh again before loading pages.
Actual inventory on physical hardware remains a user-run check, not development
qualification. Automated tests use injected APIs and isolated authenticated pipes.

## Next implementation gates

- Capture/validate the complete native CCD path/mode arrays and per-target HDR
  baseline in the recovery journal, not just output metadata from this checkpoint.
- Independently compare managed ABI with a Windows-SDK native sizeof/offsetof
  harness. Implement canonical CCD validate/apply and safe single-display legacy
  CDS_TEST / temporary CDS_FULLSCREEN, without saved database changes.
- Recovery must already be ready; Agent must own the 15-second deadline. Wire
  Keep/Revert/Escape and timeout/UI-death/hotplug/failure compensation before any
  UI mode application. Re-query and fence every step. No auto mode changes yet.
- Qualify HDR prerequisites / type 16 vs legacy 10, policy refusal and mixed SDR
  effects; then DDC capability/ranges/handle lifetime and separate persistent Undo.
- Add bounded watchdog retries/cooldown and foreign-writer conflict handling.
  Public NvAPI custom timing stays Unsupported without qualification; no EDID hack.

Native read-only references used for this checkpoint:
[EnumDisplaySettingsExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw),
[QueryDisplayConfig](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig),
[DEVMODEW](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew).
