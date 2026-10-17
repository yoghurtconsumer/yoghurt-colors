# Stage 4, step 2 review — experimental SDR display preview

This is a partial development checkpoint on .NET 10 / Windows x64. Stage 4 is
not complete. Existing colour presets and your running portable copy are retained.
No hardware harness or physical display test was run. The new backend is off by
default and remains unqualified on actual GPUs/drivers.

## Review without changing displays

Resolution → Refresh monitors, select a target, Load supported modes and Review
mode plan are read-only. Check monitor name/mapping, current rational refresh,
SDR/WCG/HDR description, positions and other outputs. Driver integer refresh is
separate from captured CCD physical timing. Incomplete/interlaced/private-extra
records are omitted. Shared-source clones are refused for planning/preview.

## Explicit experimental preview

1. Exit the old build before opening the new portable build; preserve its whole
   extracted folder. Prefer this optional display test with games closed.
2. Settings → Experimental SDR display mode preview → Save, then exit/restart.
   Independent Recovery must be ready and every active output verified SDR.
3. Resolution → Refresh monitors → select target → Load supported modes. Choose
   a **source-size option at current target timing**. A regular driver entry at a
   different refresh will be refused, even when its dimensions look identical.
   Choose Unchanged, Centered, Stretched or AspectRatio scaling; review the draft.
4. Click Preview selected source mode for 15 seconds. Agent restores owned SDR
   colours first, journals complete topology, validates/applies and reads it back.
5. Keep within the remaining Agent-owned deadline, or Revert / press Escape while
   Chromatic is focused. The banner remains above every page. Closing/tray hiding,
   UI death, timeout and OS safety suspension request baseline restoration.
6. Colours remain off after Keep or Revert. Apply them explicitly again if desired.
   Keep records confirmed intent and releases temporary topology ownership. It
   does not save Windows' database, create a custom timing or bind a game rule.

This changes the desktop source size/scaling, preserving all captured physical
target timing. A fullscreen game can choose its own mode; no game setting/FOV is
changed. Scaling can be unsupported or constrained by the driver/monitor. Actual
4:3 stretch needs human inspection with a geometric test pattern.

## Recovery and qualification limits

The separate display-preview.json journal is protected by the same exclusive
writer lease as colours. Native packets are internal bounded CCD data, never
preset imports or raw public IPC. After a verified Agent exit, Recovery takes
the lease and restores topology before colour resources. Crash immediately after
write, hung setter, UI death and timeout use fake file adapters in process tests.

Foreign/unreadable/HDR/hotplug mappings preserve the journal and require local
review rather than overwriting an unknown configuration. Degraded rollback to
Windows' saved connected-display configuration is still pending. Exact rollback
cannot be guaranteed after a disconnect. No invalid-topology retry loop exists.

The compile-only eng/Test-DisplayAbi.ps1 checks eng/native/DisplayAbi.cpp against
real Windows SDK headers in an **x64 Developer PowerShell with MSVC C++ Build
Tools and Windows SDK installed**. Neither compiler nor headers are available in
this build environment, so it was not qualified here. Managed ABI assertions and
synthetic packet/readback/fence tests pass. No native setter is run by those tests.

## Remaining Stage 4 work

- Independent SDK ABI compilation and explicit GPU/driver/Windows qualification.
- Physical refresh timing selection and legacy CDS_TEST / temporary CDS_FULLSCREEN.
- Degraded hotplug fallback; confirmed combinations for explicit profile application.
- Qualified HDR prerequisite/set/readback/rollback and mixed SDR/HDR policy.
- DDC ranges/handle lifetime/bounded workers and separate persistent Undo.
- Bounded watchdog retries/cooldown and foreign-writer conflict handling.
- Public NvAPI custom timing remains Unsupported without qualification; no EDID hacks.

Stage 5 overlay/capture has not started.
