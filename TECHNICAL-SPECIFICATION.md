# Chromatic Desktop — clean-room product and engineering specification

Version 1.0 · Research date: 15 October 2026 · Reference product: PlexusX 2.1.12

This is an implementation specification for an independently branded Windows utility. It describes externally observable features and an original implementation using Windows interfaces. No PlexusX executable, source code, private service, licence scheme, artwork, preset payload, or proprietary protocol was inspected or reused. Public statements are evidence of advertised behavior, not proof of the reference application's internals. A formal two-team clean-room process would additionally separate observation and implementation personnel; this document alone does not establish that process.

The four numbered sections below are the requested deliverable. Feature IDs, behavior, interfaces, and acceptance criteria are normative unless marked optional, experimental, or unverified. Mathematical algorithms and engineering decisions are original design proposals, not claims about PlexusX's implementation.

Document verification: extracted C# matrix code and handwritten native structure declarations compiled and passed 76,576 checks for sizes/offsets, identity/grayscale/300% saturation, neutral-gray invariance, input rejection and the bounded shadow curve. The isolated probe used the locally installed .NET 8 SDK; it did not build the proposed .NET 10 application or invoke any display-changing API. Four-section structure, F01–F38 inventory and seven prompt blocks were checked. GPU, HDR, overlay, anti-cheat and phone behavior remain implementation/release qualification work.

## Section 1 — Recommended tech stack and product requirements

### 1.1 Recommendation

Use **C# WPF, .NET 10 LTS, x64**, with a native Windows interop library and a separate effects agent. The requested .NET 8 comparison is included below, but .NET 8 support ends on 10 November 2026; .NET 10 is the appropriate new production baseline as of this document's date. Use the current serviced .NET 10 SDK and pin the exact installed SDK and package versions when bootstrapping. Do not hard-code an unverified patch version. [.NET lifecycle](https://dotnet.microsoft.com/en-us/platform/support/policy/dotnet-core).

| Option | Native integration and generation effort | Deployment/UI tradeoff | Decision |
|---|---|---|---|
| C#/.NET 8 WPF | Mature HWND integration, straightforward P/Invoke, WinRT access, MVVM; smallest conceptual surface for a coding agent | .NET 8 is near end of support; WPF needs styling | Architecture fits; use .NET 10 instead |
| C#/.NET + WinUI 3 | Same native reach, modern controls | Windows App SDK/bootstrap/package lifecycle adds complexity; transparent top-level overlay still needs Win32 | Viable if Fluent UI is a hard requirement |
| Rust + Tauri 2 + React | `windows` crate offers typed Win32/WinRT; React suits complex UI | Rust ownership, COM/apartment rules, WebView2, JS/Rust IPC and native overlay window require more integration work | Good Rust team choice; higher initial agent/debugging burden |

The effort comparison is architectural judgment, not a benchmark. Tauri does not make display effects more portable: all seven core features still require Windows-specific code.

Dependencies: CommunityToolkit.Mvvm; Microsoft.Extensions.Hosting/DependencyInjection; ASP.NET Core shared framework for Kestrel; Microsoft.Windows.CsWin32 as a build-time SDK binding generator; QRCoder; xUnit; FlaUI for selective UI automation. Use WPF imaging/WIC and native GDI for a static crosshair. Use Windows SDK WinRT projections for GSMTC. Add a vetted GIF decoder only for the optional animated app background. Lock all dependencies, audit licences, and avoid introducing a generic third-party GPU wrapper as an opaque dependency. No DirectX detours, game capture hooks, DLL injection, kernel driver, or administrator requirement in normal operation.

Target `net10.0-windows10.0.19041.0`, `SupportedOSPlatformVersion=10.0.19041.0`, `RuntimeIdentifier=win-x64`, `PlatformTarget=x64`, `Prefer32Bit=false`, `UseWPF=true`; use a current Windows SDK for bindings and runtime-gate newer calls. Validate Windows 10 22H2 and supported Windows 11 releases. Windows 10 deployment must have an applicable servicing arrangement; API compatibility does not imply OS servicing. ARM64 needs its own native build and qualification; x64 emulation is not a promised Magnification configuration. WOW64 Magnification is unsupported. [Magnification overview](https://learn.microsoft.com/en-us/windows/win32/winauto/magapi/magapi-intro).

### 1.2 Research coverage and limits

Read directly in the in-app browser: [home/features/pricing/changelog](https://plexusx.lol/), [product](https://plexusx.lol/product), [how it works](https://plexusx.lol/how-it-works), [community presets](https://plexusx.lol/presets), [AMD/Intel guide](https://plexusx.lol/guides/digital-vibrance-amd-intel), [HDR guide](https://plexusx.lol/guides/hdr-looks-washed-out), [recording guide](https://plexusx.lol/guides/colours-missing-from-recordings), [privacy](https://plexusx.lol/privacy), and [trust](https://plexusx.lol/trust). The research search engine could not fetch the site; the interactive browser could. Sources below link to the actual pages read.

This catalog covers the publicly described application features found on those pages. Account-only UI, complete historic release notes, exact slider ranges beyond published saturation, unspecified Explorer switches, internal algorithms, and compatibility claims were not independently tested. Those unknowns must not be invented as facts. No installer was run. Site previews explicitly describe themselves as browser simulations.

### 1.3 Feature inventory and traceability

Sources: H=home, P=product, W=how-it-works, C=presets, A=AMD/Intel guide, R=recording guide, V=privacy. Each identifier maps to a module in Section 3. Extra features discovered during research remain in the specification even when they are not one of the seven requested core features.

| ID | Publicly described capability | Evidence | Required clone behavior / module |
|---|---|---|---|
| F01 | Whole-screen saturation through 300%; 100% neutral | H/P/W | Global Magnification matrix, explicit all-monitor scope; §3.2 |
| F02 | Native NVIDIA digital vibrance; software AMD/Intel equivalent; software beyond NVIDIA limit | H/W/A | Optional private NvAPI adapter plus software backend; §3.3 |
| F03 | Brightness, contrast, temperature and gamma | P/W | Affine color controls plus optional SDR LUT; §3.2/3.4 |
| F04 | Highlights and shadows sliders in phone UI | V | Explicit nonlinear LUT controls, with limitations; §3.4 |
| F05 | Scene looks for treelines, interiors/dark rooms, night, snow glare, smoke/dust, range/bright sky | H/P | Original built-in presets with preview, reset and hotkeys; §3.7 |
| F06 | Saved custom looks | P | Local CRUD, duplicate/export/import/version migration; §3.7 |
| F07 | Automatic game-specific colors | H/P/W | Foreground executable matching, desktop restoration; §3.7 |
| F08 | Preset hotkeys | P | Conflict-aware global toggle hotkeys; §3.5 |
| F09 | Red-/green-/blue-weak color modes | P | Configurable approximate linear daltonization; §3.2 |
| F10 | Hold Dark Boost, selectable tint; release restores normal look; Mouse 5 demo | H | Keyboard and side-mouse hold, bounded shadow LUT; §3.4/3.5 |
| F11 | Resolution and refresh switching | H/W | Enumerated modes, test/apply/confirm/rollback; §3.6 |
| F12 | HDR on/off | H/W | Per-target capability-gated CCD toggle; §3.6 |
| F13 | Stretched 4:3 | H | Source 4:3 + supported output stretch; never modify game FOV; §3.6 |
| F14 | Custom modes including square sizes; kept NVIDIA mode saved in driver list | H/W | Enumerated custom-size selection; optional vendor custom-mode provider; §3.6 |
| F15 | Crosshair shape, size, gap, color and outline | H/P | External transparent HWND; §3.8 |
| F16 | Picture crosshairs and X/Y offsets (2.1.12) | H | Sanitized PNG image, scale/opacity/offsets; §3.8 |
| F17 | Library of shared looks/crosshairs; public page also lists resolutions/full setups | H/C/V | Local library and share documents; optional independent hosted catalog; §3.7/3.12 |
| F18 | Short preset codes; website-to-app link; confirmation before application | C/W | Own protocol `chromaticdesk://`, bounded schema and confirmation; §3.7 |
| F19 | Catalog filter by type/game, newest/most-copied, author/date; publish/report; image moderation | C/V | Independent optional community service; §3.12 |
| F20 | Wi-Fi phone color sliders/scene presets via QR; server lives only while Phone window is open | H/V | Authenticated LAN HTTP/WebSocket, color-only whitelist; §3.9 |
| F21 | Music controls, improved app support (2.1.10) | H | GSMTC session picker, metadata and supported transport buttons; §3.10 |
| F22 | Per-monitor gamma/warmth | P | Device-specific SDR LUT where supported; global matrix remains global; §3.4/3.11 |
| F23 | Monitor hardware brightness/color over display cable | W/V | DDC/CI capability-gated operations; §3.11 |
| F24 | Color watchdog following game/display changes | P | Event-driven bounded reapply, conflict detection; §3.1/3.11 |
| F25 | Default/Slate appearance; user picture/GIF background | P | Independently designed themes and local media; §3.12 |
| F26 | Windows appearance customization: dark mode, accent, taskbar alignment/transparency, some Explorer options | W/V | Supported Settings links; optional documented/versioned per-user adapters; §3.12 |
| F27 | Start with Windows | W/V | Opt-in HKCU Run registration; §3.12 |
| F28 | Recording-friendly color routing; detection notice and setup test | R/V | Software route + capability test; no capture guarantee; §3.13 |
| F29 | Screenshot hotkey, local file and clipboard with color changes | V | User-initiated capture/export with explicit processing route; §3.13 |
| F30 | Installed Steam game discovery | V | Local registry/library manifests, manual add always available; §3.7 |
| F31 | Local Discord activity including selected preset/game | W/V | Optional opt-in Discord RPC; §3.12 |
| F32 | Account sign-in using short-lived loopback browser callback; licence key entry/account page | V | Local build needs no account; independent optional identity adapter; §3.12 |
| F33 | Plans, feature locks, separate Dark Boost entitlement, PC activation/release/move | H/V | Cataloged commercial behavior; independent optional entitlement service; §3.12 |
| F34 | Automatic signed update verification, release notes/What's New; status/revocation/check-in | V | Own signed updater; no PlexusX endpoints; §3.12 |
| F35 | Local crash logs with build/OS; bug-report form with user-triggered technical report | H/V | Local rotating logs, reviewed export, explicit upload only; §3.12 |
| F36 | Legal/licences page, uninstall retains user data, explicit cleanup | V | Original notices, export/delete controls, per-user uninstall; §3.12 |
| F37 | Repair action can relaunch elevated | W | Diagnostic help first; separately scoped repair, never automatic elevation; §3.12 |
| F38 | Update rating controls documented but inactive | V | Record as inactive, do not enable by default |

Reference pricing on the research date: free saturation/resolution with an account; monthly €4.99; lifetime €15.99; separate Dark Boost €4.50/month; bundle €7.99/month. Trials and commercial copy are reference metadata, not a requirement to reproduce a shop. The local utility ships all implemented features unlocked and offline. Cloud commerce is an explicitly separate deployment profile. [Reference pricing](https://plexusx.lol/#pricing).

### 1.4 Non-negotiable feasibility boundaries

1. **No anti-cheat guarantee.** The implementation never opens a game for VM read/write, injects code, attaches a debugger, hooks DirectX, edits game files, or installs a driver. Reading an executable path with limited process-query rights is allowed. An external overlay or visibility aid can still violate game rules; do not claim “undetectable” or “ban-proof.” PlexusX itself flags Dark Boost risk. [Reference disclosure](https://plexusx.lol/#pricing).
2. **300% means the saturation parameter is 3.0.** It is not three times display gamut, brightness, or calibrated perceptual saturation. RGB clipping is expected at high values.
3. **Magnification is one desktop-wide matrix.** It cannot independently saturate only one monitor or only a game window. Windowed/borderless SDR is the intended support target; independent flip, protected content, remote sessions, exclusive fullscreen and driver paths require qualification.
4. **No public arbitrary DWM desktop color-matrix API exists.** Use the public Magnification interface. DWM attributes and DirectComposition effects do not transform unrelated applications' entire desktop.
5. **Three independent gamma ramps cannot implement RGB cross-channel saturation.** They can approximate gamma, warmth and tonal curves. Do not silently drop saturation or claim equivalent exclusive-fullscreen color. Microsoft's gamma API has known reliability and HDR limitations. [Gamma limitations](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp).
6. **A 5×5 affine matrix cannot implement a shadow-only nonlinear curve.** Its Dark Boost fallback is a lower-contrast affine lift, clearly labeled; highlight preservation requires the optional LUT path. It cannot recognize a game's maps or menus. Release-to-restore provides that separation.
7. **HDR switching is distinct from HDR grading.** SDR effects are disabled by default when advanced color/HDR is active; no promise of correct HDR saturation or shadow processing. The reference also disclaims HDR signal processing. [Reference HDR guide](https://plexusx.lol/guides/hdr-looks-washed-out).
8. **Borderless stretch changes the desktop output, not the game's rendering policy.** A game may letterbox or ignore the requested aspect. Win32 cannot universally force unsupported modes or GPU scaling behavior.
9. **Overlay “zero latency” is not physically supportable.** Static overlay rendering does not intercept or queue game frames; compositor/display timing and possible performance changes must be measured.
10. **Capture parity is conditional.** Driver DVC/LUT effects commonly occur outside capture. Software color may still be omitted by a capture method. No promise that game capture, desktop capture, screenshots or encoded clips all match.

### 1.5 Product and release requirements

Users can select a monitor, enable colors, adjust saturation, save a profile, bind it to an executable, test a display mode, use an optional crosshair, and pair a phone without administrator privileges. Main navigation: Display, Presets/Library, Games, Resolution, Crosshair, Monitor, Customize, Settings; Music is a compact panel. Show effective backend, scope, capability and pending/failed status in a diagnostics panel. User-facing sliders remain plain language.

Default state: effects off, saturation 100, brightness 0, contrast 1, gamma 1, neutral warmth, vibrance neutral, Dark Boost off, crosshair off, phone listener off, startup off, Discord off. Nothing changes on install. Main window close exits/restores by default; optional “close to tray” is visibly configured. Tray provides Show, Pause, Restore and Exit. A conflicting restore hotkey must be reported, not silently ignored.

Acceptance and release gates: mathematical/ABI tests; restore-on-exit and crash recovery tests; real AMD/Intel/NVIDIA SDR tests; multiple displays including negative coordinates and mixed DPI; HDR transitions; hotplug and resume; display preview timeout; LAN authorization/Origin/schema tests; hardware-specific feature matrix. Unsupported capabilities must produce a reason and no side effect. Hardware tests are manual opt-in and never run as part of ordinary CI.

Performance targets to measure, not guarantees: idle CPU below 0.5% on the documented reference PC; no per-frame work for a static crosshair; software slider command-to-API completion p95 below 50 ms on that PC; phone command-to-ack p95 below 150 ms on local Wi-Fi; hook callback p99 below 1 ms; no more than 30 software color writes/second during drag. Gamma may take ~200 ms per call and uses a serialized latest-value queue with release priority; disable hold boost on measured slow configurations. Record CPU/GPU/frame-time deltas when overlay or Magnification is active. Publish hardware, Windows build, driver version and methodology with results.

## Section 2 — Complete directory and file structure

The tree is the intended final implementation. Generated interop is produced at build time, not manually edited. Cloud projects are optional and excluded from the default local solution build.

```text
ChromaticDesktop/
  ChromaticDesktop.sln
  global.json
  Directory.Build.props
  Directory.Packages.props
  NuGet.config
  .editorconfig
  .gitignore
  README.md
  LICENSE
  THIRD-PARTY-NOTICES.md
  AGENTS.md
  docs/
    TECHNICAL-SPECIFICATION.md
    IMPLEMENTATION-STATE.md
    REQUIREMENTS-TRACEABILITY.md
    CAPABILITIES.md
    COMPATIBILITY-MATRIX.md
    SECURITY.md
    RELEASE-CHECKLIST.md
    MANUAL-TESTS.md
    schemas/
      settings.schema.json
      profile.schema.json
      preset.schema.json
      ipc.schema.json
      remote.schema.json
      recovery.schema.json
  src/
    Chromatic.Core/
      Chromatic.Core.csproj
      Models/
        AppSettings.cs
        ColorSettings.cs
        CrosshairSettings.cs
        DisplayIdentity.cs
        DisplayMode.cs
        DisplayProfile.cs
        GameRule.cs
        PresetDocument.cs
        CapabilityReport.cs
        ApplyResult.cs
        RecoverySnapshot.cs
      Abstractions/
        IColorBackend.cs
        IVibranceBackend.cs
        IGammaBackend.cs
        IDisplayBackend.cs
        IMonitorBackend.cs
        IForegroundSource.cs
        IProfileRepository.cs
        IEffectsClient.cs
        IMediaController.cs
      Color/
        Matrix5x5.cs
        ColorMatrixBuilder.cs
        DaltonizationBuilder.cs
        ToneCurveBuilder.cs
        GammaRampComposer.cs
        BackendPlanner.cs
      State/
        EffectsReducer.cs
        EffectsState.cs
        Commands.cs
        ProfileResolver.cs
      Validation/
        SettingsValidator.cs
        PresetValidator.cs
        RemoteCommandValidator.cs
    Chromatic.Native/
      Chromatic.Native.csproj
      NativeMethods.txt
      NativeMethods.json
      MagnificationInterop.cs
      GammaInterop.cs
      InputInterop.cs
      DisplayConfigInterop.cs
      HdrPackets.cs
      NvApiInterop.cs
      MonitorInterop.cs
      WindowInterop.cs
      NativeErrors.cs
      SafeHandles.cs
      AbiAssertions.cs
    Chromatic.Infrastructure/
      Chromatic.Infrastructure.csproj
      Persistence/
        AtomicJsonStore.cs
        SettingsRepository.cs
        ProfileRepository.cs
        PresetRepository.cs
        AssetStore.cs
        MigrationRunner.cs
      Ipc/
        EffectsPipeClient.cs
        EffectsPipeServer.cs
        PipeAuthorization.cs
        Protocol.cs
      Games/
        SteamDiscovery.cs
        SteamVdfReader.cs
        ForegroundMonitor.cs
        ExecutableIdentityResolver.cs
      Integrations/
        StartupRegistration.cs
        ProtocolRegistration.cs
        DiscordActivity.cs
        WindowsAppearanceService.cs
        CaptureService.cs
        CaptureDiagnostics.cs
        UpdateService.cs
        DiagnosticExport.cs
        LocalFeaturePolicy.cs
      Logging/
        LocalLogService.cs
        Redaction.cs
    Chromatic.Agent/
      Chromatic.Agent.csproj
      Program.cs
      AgentHost.cs
      HiddenBroadcastWindow.cs
      EffectsCoordinator.cs
      RecoveryJournal.cs
      OwnershipTracker.cs
      ColorWatchdog.cs
      Backends/
        MagnificationBackend.cs
        NvidiaDvcBackend.cs
        SdrGammaBackend.cs
        DisplayModeBackend.cs
        HdrBackend.cs
        DdcMonitorBackend.cs
      Input/
        HookMessageLoop.cs
        HoldBindingService.cs
        HotkeyService.cs
        SessionSuspension.cs
      Display/
        DisplayInventory.cs
        ModePlanner.cs
        ModePreviewTransaction.cs
        CustomModeProvider.cs
    Chromatic.Recovery/
      Chromatic.Recovery.csproj
      Program.cs
      AgentLivenessMonitor.cs
      RestoreExecutor.cs
    Chromatic.Overlay/
      Chromatic.Overlay.csproj
      OverlayHost.cs
      OverlayWindow.cs
      CrosshairRenderer.cs
      DibSurface.cs
      OverlayPlacement.cs
      ImageCrosshairLoader.cs
    Chromatic.Remote/
      Chromatic.Remote.csproj
      PhoneServer.cs
      NetworkInterfaceSelector.cs
      PairingService.cs
      RemoteSessionStore.cs
      HostOriginValidator.cs
      WebSocketEndpoint.cs
      wwwroot/
        index.html
        app.js
        app.css
    Chromatic.Media/
      Chromatic.Media.csproj
      GsmtcController.cs
      MediaSessionModel.cs
      ThumbnailCache.cs
    Chromatic.App/
      Chromatic.App.csproj
      app.manifest
      App.xaml
      App.xaml.cs
      CompositionRoot.cs
      MainWindow.xaml
      MainWindow.xaml.cs
      TrayService.cs
      Views/
        DisplayView.xaml
        PresetsView.xaml
        GamesView.xaml
        ResolutionView.xaml
        CrosshairView.xaml
        MonitorView.xaml
        CustomizeView.xaml
        SettingsView.xaml
        MusicPanel.xaml
        PhoneWindow.xaml
        ModeConfirmWindow.xaml
        PresetImportWindow.xaml
      ViewModels/
        MainViewModel.cs
        DisplayViewModel.cs
        PresetsViewModel.cs
        GamesViewModel.cs
        ResolutionViewModel.cs
        CrosshairViewModel.cs
        MonitorViewModel.cs
        CustomizeViewModel.cs
        SettingsViewModel.cs
        MusicViewModel.cs
        PhoneViewModel.cs
      Themes/
        Default.xaml
        Slate.xaml
        Common.xaml
      Assets/
        app.ico
        presets.json
        release-notes.json
  tests/
    Chromatic.Core.Tests/
      Chromatic.Core.Tests.csproj
      MatrixTests.cs
      ToneCurveTests.cs
      ReducerTests.cs
      ProfileResolverTests.cs
      ValidationTests.cs
    Chromatic.Integration.Tests/
      Chromatic.Integration.Tests.csproj
      AbiTests.cs
      IpcTests.cs
      RecoveryTests.cs
      RemoteAuthorizationTests.cs
      RemoteOrderingTests.cs
      PersistenceTests.cs
    Chromatic.Ui.Tests/
      Chromatic.Ui.Tests.csproj
      ShellSmokeTests.cs
      ModePreviewTests.cs
    Chromatic.HardwareHarness/
      Chromatic.HardwareHarness.csproj
      Program.cs
      SafePreviewRunner.cs
      ColorTestPattern.cs
  packaging/
    installer.iss
    publish.ps1
    sign.ps1
    verify-package.ps1
    release-manifest.schema.json
  optional-cloud/
    README.md
    contracts.openapi.yaml
    Catalog.Api/
    IdentityEntitlements.Api/
    Catalog.Web/
    database/migrations/
    tests/
  .github/workflows/
    build.yml
    release.yml
```

Each XAML view uses its paired view model; code-behind is only for HWND/lifecycle plumbing. `Chromatic.Core` contains no HWND, registry, network or P/Invoke. App hosts Remote, Media and Overlay. Agent owns color, modes, monitor writes and input bindings. Recovery operates only after Agent ownership is conclusively lost.

User data, under the independently branded directory:

```text
%APPDATA%\ChromaticDesktop\
  settings.json
  profiles.json
  presets.json
  assets/<sha256>.png
  backgrounds/<sha256>.<approved-extension>
%LOCALAPPDATA%\ChromaticDesktop\
  recovery/current.json
  logs/<date>.log
  cache/library.json
  cache/thumbnails/
  updates/staging/
  secrets.dat                 # only optional cloud credentials, DPAPI protected
%USERPROFILE%\Pictures\ChromaticDesktop\<timestamp>.png
```

Settings use atomic same-volume replacement, schema versions, a last-good backup, bounded file/image sizes and explicit migration. Temporary hold state and LAN pairing secrets are never persisted. Imported presets contain portable intentions, not another machine's adapter LUID or device path.

## Section 3 — Module-by-module implementation guide

### 3.1 Process architecture, state and restoration

```text
WPF App ── typed, authenticated named pipe ── Effects Agent
  │                                           │
  ├─ external crosshair HWND                  ├─ Magnification + NvAPI + gamma
  ├─ Phone HTTP/WS → validated commands       ├─ display inventory / mode transaction
  ├─ GSMTC                                    ├─ keyboard/mouse/foreground events
  └─ local profiles/settings                  └─ ownership + durable recovery journal
                                                     │
                                             Recovery companion
                                        (same interactive user/session)
```

App and Agent are ordinary user processes. No Session 0 service. Agent has a single reducer/command queue and an STA thread with a hidden **top-level** window and message pump for display/power/session broadcasts. A message-only window alone does not receive broadcast messages. Input callbacks run on a separate pumping thread and enqueue small state changes; DDC calls run on a serialized monitor worker. Never perform filesystem, driver, network or UI work inside a callback.

The named pipe is per-logon-session, random-suffixed, ACL restricted to the current user, and rejects remote clients. Validate peer PID/session and exchange a random launch nonce; never trust a pipe name as authentication. Limit frames to 64 KiB, use a versioned JSON envelope, and expose typed commands only. No method-name reflection, shell commands or arbitrary file writes. Public UI and phone routes cannot invoke raw native functions.

Core contracts:

```csharp
public enum Capability { Available, Unsupported, NeedsUserAction, Experimental }
public sealed record ApplyResult(bool Success, string Backend,
    string? ErrorCode, string? Explanation, long Revision);
public sealed record DisplayIdentity(string MonitorDevicePath,
    string? EdidSerial, string GdiName, long AdapterLuid, uint TargetId);
public sealed record ColorSettings(double SaturationPercent,
    double Brightness, double Contrast, double Gamma, double Warmth,
    double Vibrance, double Shadows, double Highlights, string CvdMode);
public interface IColorBackend {
    Task<ApplyResult> ApplyAsync(ColorSettings settings, long revision,
        CancellationToken cancellationToken);
    Task<ApplyResult> RestoreAsync(CancellationToken cancellationToken);
}
```

Use immutable desired state plus observed state, active profile ID, capability inventory, ownership generation, foreground identity and a monotonic revision number. Commands are `Enable`, `SetColor`, `ActivatePreset`, `ForegroundChanged`, `SetHold`, `PreviewMode`, `ConfirmMode`, `CancelPreview`, `Suspend`, `Resume`, `Restore`, `Shutdown`. Command completion means the requested operation completed or explicitly failed; a slider value alone is not proof the screen changed. UI reconciles from Agent acknowledgments.

Precedence: shutdown/restore/session suspension > active display-preview transaction > current base profile with temporary held-boost overlay > manual session override > foreground game profile > desktop profile. Holding a key is a temporary modifier, never an overwrite of the saved profile. If a preset changes during a hold, release resolves the new base profile. Display preview suspends auto mode switching and hold boost. Any stale queued color operation must check its generation again immediately before writing.

Before the first mutation of each owned resource, read its real baseline: full-screen matrix, per-display gamma table, native DVC, complete active CCD paths/modes and HDR state, and any temporary hardware setting. Journal version, timestamp, user/session, display identities, baseline, last applied value and transaction deadline atomically **before** the write. Re-query after display changes. LUID/target ID and `DISPLAY1` are runtime mappings, not permanent identities; hotplug reconnect uses monitor device path/EDID and refuses ambiguous matches.

Agent monitors the UI process handle: on unexpected UI exit restore and terminate. Recovery monitors Agent termination and a heartbeat; a timeout triggers IPC restore first, then terminates only a verified hung child Agent and waits for process exit before taking ownership. Never have two live writers. On next launch, offer recovery of an unfinished journal before applying profiles. Restore the captured baseline, not an assumed identity matrix or linear gamma. Where readback shows a foreign change instead of our last write, stop owning that resource and report a conflict; do not continuously fight Windows Magnifier, Night Light, calibration tools or a driver panel.

Normal exit, pause, lock, fast user switch, suspend and disconnected session clear hold state, hide overlay, stop phone server, cancel preview and restore temporary effects. Unregister hooks and events, then call `MagUninitialize`. A killed recovery process, kernel failure, power loss or unrelated software can defeat immediate restoration; journal recovery is best effort. Persistent user-selected Customize and DDC changes have their own explicit undo and are not silently undone as if they were temporary colors.

### 3.2 Whole-screen matrix and color controls — F01/F03/F09

#### Native ABI and exact orientation

`Magnification.dll` exports these Win32 functions. `MAGCOLOREFFECT` is exactly 25 contiguous 32-bit floats, size 100 bytes. The following handwritten declarations are an alternative to generated bindings; do not define duplicate types. [Matrix layout](https://learn.microsoft.com/en-us/windows/win32/api/magnification/ns-magnification-magcoloreffect), [initialization](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-maginitialize), [full-screen setter](https://learn.microsoft.com/en-us/windows/win32/api/magnification/nf-magnification-magsetfullscreencoloreffect).

```csharp
using System.Runtime.InteropServices;

[StructLayout(LayoutKind.Sequential)]
public unsafe struct MAGCOLOREFFECT { public fixed float transform[25]; }

internal static class MagnificationNative {
    [DllImport("Magnification.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagInitialize();
    [DllImport("Magnification.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagGetFullscreenColorEffect(out MAGCOLOREFFECT effect);
    [DllImport("Magnification.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagSetFullscreenColorEffect(in MAGCOLOREFFECT effect);
    [DllImport("Magnification.dll", ExactSpelling=true)]
    [return: MarshalAs(UnmanagedType.Bool)]
    internal static extern bool MagUninitialize();
}
```

These pages document BOOL success/failure, not a reliable extended error contract; do not invent a meaningful last-error value for every Magnification failure. Keep calls on the initialized Agent thread. No magnifier control window is needed. Do not alter the full-screen zoom, offset, input transform or cursor to implement color. `MagSetInputTransform` has separate UIAccess requirements and is not used.

Use **row vectors**, normalized channel values and row-major storage:

```text
[R' G' B' A' 1] = [R G B A 1] × M
R' = R*M00 + G*M10 + B*M20 + A*M30 + M40
G' = R*M01 + G*M11 + B*M21 + A*M31 + M41
B' = R*M02 + G*M12 + B*M22 + A*M32 + M42
```

Translation belongs in the fifth **row**, not the fifth column. Preserve alpha with M33=1, all RGB/alpha cross terms zero, M44=1. This convention follows Microsoft's full-screen grayscale example and homogeneous color-matrix convention. [ColorMatrix](https://learn.microsoft.com/en-us/windows/win32/api/gdipluscolormatrix/ns-gdipluscolormatrix-colormatrix).

Let p be the saturation slider, `s=p/100`, `p∈[0,300]`; weights `wr=.2126`, `wg=.7152`, `wb=.0722`; `L=wr*R+wg*G+wb*B`. Define `C'=s*C+(1-s)*L` for each RGB channel. Then:

```text
q = 1-s
S(s) =
[ s+q*wr   q*wr     q*wr    0  0 ]
[   q*wg s+q*wg     q*wg    0  0 ]
[   q*wb   q*wb   s+q*wb    0  0 ]
[      0      0        0    1  0 ]
[      0      0        0    0  1 ]

S(3) =
[  2.5748 -0.4252 -0.4252  0 0 ]
[ -1.4304  1.5696 -1.4304  0 0 ]
[ -0.1444 -0.1444  2.8556  0 0 ]
[  0       0       0       1 0 ]
[  0       0       0       0 1 ]
```

This is an exact implementation of the chosen saturation definition; the weights do not turn the desktop pipeline into a calibrated linear-light workflow. Treat it as an SDR appearance transform. Negative terms and out-of-range outputs are expected; clipping alters hue and detail. Do not clamp individual matrix coefficients. Reject NaN/infinity, compute in double, convert to float at the native boundary, and clamp only the reference preview's final output to [0,1].

```csharp
public static unsafe MAGCOLOREFFECT Saturation(double percent) {
    if (!double.IsFinite(percent) || percent < 0 || percent > 300)
        throw new ArgumentOutOfRangeException(nameof(percent));
    double s = percent / 100.0, q = 1.0 - s;
    double[] w = [0.2126, 0.7152, 0.0722];
    MAGCOLOREFFECT m = default;
    for (int input=0; input<3; input++)
        for (int output=0; output<3; output++)
            m.transform[input*5+output] =
                (float)(q*w[input] + (input==output ? s : 0));
    m.transform[18] = 1; // alpha
    m.transform[24] = 1; // homogeneous coordinate
    return m;
}
```

Additional affine controls: brightness b∈[-.25,.25] adds b to each RGB output; contrast k∈[.5,2] gives `k*(C-.5)+.5`; warmth t∈[-1,1] uses chosen UI gains `(1+.1*t, 1, 1-.1*t)`. These are deliberate product ranges and an original warmth approximation, not Kelvin calibration or reference-app ranges. Affine matrix A has RGB diagonal k and translation `.5*(1-k)+b`; T has RGB diagonal warmth gains. Compose `M=S(s) × D × A × T` for row vectors, then preserve alpha. Build one final matrix; multiple setters replace one another rather than stack.

If the baseline is nonidentity, default to declining ownership and explain an existing full-screen filter. An explicit coexistence option may use `Mbaseline × Muser`; that is a chosen ordering, not a guarantee of composability with another app continually writing the same global state. Always restore the baseline when still owned.

Approximate color-accessibility mode: simulation is not correction. Implement a named, adjustable experimental correction `D=I+e*(I-C)*K`, where C is the row-vector transpose of a checked-in CVD simulation matrix, e∈[0,1], and K redistributes the residual. For red/green weakness choose K rows `[[0,.7,.7],[0,1,0],[0,0,1]]`; for blue weakness use `[[1,0,0],[0,1,0],[.7,.7,0]]`. This correction design is original and must be described as an approximation in encoded SDR; validate with affected users. Source the simulation coefficients and attribution from the [Machado/Oliveira/Fernandes paper](https://profs.ic.uff.br/~laffernandes/content/publications/journal/2009_tvcg_15%286%29/machado_oliveira_fernandes-tvcg-15%286%29-2009-corrected.pdf), verify redistribution rights, and distinguish the research simulation from the original correction. Offer off/strength/reset and no medical efficacy claim. A single matrix cannot perform correct sRGB decode → linear transform → encode.

Acceptance: S(1)=I; S(0) maps red to (.2126,.2126,.2126); every neutral gray stays gray under saturation alone; alpha unchanged; test red/green/blue at 300%; verify fifth-row translations; transposed-matrix regression; identity reset and prior-baseline restoration. View real test patterns on physical monitors because capture may omit the effect.

### 3.3 Hardware digital vibrance — F02

The name `NvAPI_GPU_SetDVCLevelEx` is commonly used for a **private**, reverse-engineered display-control entry point; it is not a promise of a current public SDK contract or a physical-GPU-handle signature. Do not `DllImport` that name as an ordinary exported symbol. Resolve `nvapi_QueryInterface` from the installed driver DLL, then resolve a known private interface ID. The authors' published wrapper demonstrates the ABI, structure and IDs below; NVIDIA's public headers are authoritative for public NvAPI, not this private call. [Wrapper implementation](https://github.com/jNizM/NVIDIA_NvAPI/blob/master/src/Class_NvAPI.ahk), [public NvAPI interfaces](https://github.com/NVIDIA/nvapi/blob/main/nvapi_interface.h).

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct NV_DISPLAY_DVC_INFO_EX {
    public uint Version;
    public int CurrentLevel, MinLevel, MaxLevel, DefaultLevel;
} // 20 bytes; version = 20u | (1u << 16) = 0x00010014

[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate nint NvQueryInterface(uint interfaceId);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NvInitialize();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NvUnload();
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NvEnumDisplay(uint index, out nint displayHandle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl, CharSet=CharSet.Ansi)]
internal delegate int NvAssociatedDisplay(
    [MarshalAs(UnmanagedType.LPStr)] string gdiDeviceName, out nint displayHandle);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NvGetDvcEx(nint displayHandle, uint outputId,
    ref NV_DISPLAY_DVC_INFO_EX info);
[UnmanagedFunctionPointer(CallingConvention.Cdecl)]
internal delegate int NvSetDvcEx(nint displayHandle, uint outputId,
    ref NV_DISPLAY_DVC_INFO_EX info);

// QueryInterface IDs; pin their provenance with the adapter implementation.
// Initialize=0x0150E828; Unload=0xD22BDD7E;
// EnumNvidiaDisplayHandle=0x9ABDD40D;
// GetAssociatedNvidiaDisplayHandle=0x35C29134;
// GetDVCInfoEx=0x0E45002D; SetDVCLevelEx=0x4A82C2B1.
```

Load only `nvapi64.dll` from Windows' system directory using `LoadLibraryExW` with `LOAD_LIBRARY_SEARCH_SYSTEM32` (0x800); never download or ship an arbitrary DLL, or search the working directory. Retain module and delegate lifetimes until all calls finish; unload last. Initialize, check every non-null function pointer and NvAPI status, then map the **display-driving** adapter/output using its GDI display name. A laptop rendering on NVIDIA with a panel driven by Intel is not native-DVC capable merely because an NVIDIA GPU exists.

For the attached-display-handle form, use the validated handle with outputId=0. Do not substitute a physical GPU handle, ordinal monitor number or Windows target ID. Read the 20-byte info structure, validate min≤default≤max and current range, snapshot CurrentLevel, change only the requested level with initialized version, invoke setter and read back. On any incompatible-structure/function/not-supported/invalidated-handle error disable the adapter or re-enumerate once after topology change. No blind function-ID scanning. Private ABI failure can crash a process; qualification is mandatory before enabling this adapter in a release.

UI native vibrance is its own 0–100 scale with default at 50. Map u∈[0,1] piecewise: below .5, `level=min+2*u*(default-min)`; above .5, `level=default+(2*u-1)*(max-default)`. Round/clamp to discovered integers. Do not equate this with the saturation percentage. Software vibrance v∈[-1,1] is explicitly a saturation-like approximation `S(1+v)`, combined with saturation s as `S(s*(1+v))` with effective factor capped at 3. It is not NVIDIA's perceptual algorithm.

Backend policy: Software mode leaves native DVC at its captured baseline and uses the matrix for both controls; Native mode uses NvAPI for vibrance and Magnification only for independently requested saturation/affine controls. Switching mode restores the departing resource before applying the new plan, so a single vibrance slider is never applied twice. A combined native-plus-extra-saturation profile is allowed only as an explicit advanced setting. AMD/Intel and unsupported NVIDIA paths use Software mode. Gamma is a fallback for tonal controls only; if Magnification fails, saturation/vibrance are unavailable instead of being faked with gamma.

Acceptance: multi-monitor mapping, hybrid laptop fallback, absent DLL, null entry point, version mismatch, neutral slider at reported default, round trip to baseline, handle invalidation after mode switch. Record tested driver versions and mark private DVC support experimental until that matrix is complete.

### 3.4 Gamma, shadows, highlights and Dark Boost — F03/F04/F10/F22

#### Native gamma ABI

The optional SDR backend uses `Gdi32.dll`. RGB arrays are three consecutive 256-entry WORD tables, not an RGB-interleaved array. Use a display-specific DC created with `CreateDCW(L"DISPLAY", gdiName, NULL, NULL)`; destroy it with `DeleteDC`. A desktop DC does not mean independent per-monitor control. Some outputs share ramps; detect and disclose that. [GetDeviceGammaRamp](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-getdevicegammaramp).

```csharp
[StructLayout(LayoutKind.Sequential)]
internal unsafe struct GAMMARAMP {
    public fixed ushort Red[256];
    public fixed ushort Green[256];
    public fixed ushort Blue[256];
} // 1536 bytes

[DllImport("gdi32.dll", ExactSpelling=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool GetDeviceGammaRamp(nint hdc, out GAMMARAMP ramp);
[DllImport("gdi32.dll", ExactSpelling=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool SetDeviceGammaRamp(nint hdc, in GAMMARAMP ramp);
[DllImport("gdi32.dll", CharSet=CharSet.Unicode, ExactSpelling=true)]
static extern nint CreateDCW(string driver, string device, nint output, nint initData);
[DllImport("gdi32.dll", ExactSpelling=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool DeleteDC(nint hdc);
```

This API is explicitly discouraged by Microsoft: it can return success without applying a rejected ramp, can be overwritten/reset, has undefined HDR/calibration interaction, and may take hundreds of milliseconds. Readback is necessary but not sufficient evidence of visible output. Keep it an opt-in compatibility feature, disabled for HDR/ACM and where it conflicts with calibration. [SetDeviceGammaRamp limitations](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/nf-wingdi-setdevicegammaramp).

#### Original bounded shadow curve

For x∈[0,1], choose shadow boundary t=.45, strength a∈[0,.8], exponent q∈[.4,1], and blend h∈[0,1]. Set:

```text
u = x/t
f(x) = x + a*x*(1-u)^2,              0 ≤ x < t
f(x) = x,                           t ≤ x ≤ 1
g(x) = (1-h)*f(x) + h*pow(f(x), q)
```

Default Dark Boost h=0, a=.6. Above .45 the curve is exactly identity, black stays black and white stays white; there is no hard knee in slope. Its shadow derivative is `1+a*(1-4u+3u²)`, whose minimum is `1-a/3 > 0` in the allowed range, proving monotonicity. The optional gamma term h>0 also changes highlights; show that in preview and default it off for hold boost. True crushed-to-zero scene detail cannot be recovered from black pixels.

For normal Gamma use `pow(x,1/G)` with G∈[.5,2.5]. Define optional Shadows with the same bounded lift and Highlights as the mirrored curve `1-f(1-x)`, strength in [0,.8]. Use a deterministic order: normal gamma → shadows → highlights → temporary dark curve → warmth/tint. Per-channel curves can shift hue and are not luminance-aware tone mapping. Dark tint uses gains `1-dc`, each dc∈[0,.1]; attenuation avoids additive white clipping. Optional affine dark fallback is `C'=(1-l)*C+l`, l∈[0,.15], preserving white but lifting all other levels and reducing contrast; call it “basic lift,” never “shadow-only.”

For entry i, compute x=i/255, desired tonal mapping z=T(x), then sample the captured baseline calibration table `B[channel]` at z*255 using linear interpolation. Write rounded ushort values, preserving the baseline's quantization and monotonicity where possible; reject nonmonotone or invalid baselines for composition and offer disable instead of corrupting calibration. Do not repeatedly compose from the last written ramp. Restoration writes the exact original 1536 bytes. A native-neutral identity table uses i*257 but is **not** a substitute for the saved baseline.

The reducer generates both held and normal desired ramps ahead of time. Release queues a high-priority latest normal state; it does not wait behind a backlog of slider requests. An in-flight driver call cannot be preempted. Measure latency before enabling hold support; if too slow or ignored, offer the basic Magnification lift or mark hold boost unavailable. On driver reset re-evaluate capability instead of rapidly retrying.

This mechanism is global per display and never knows whether a map/menu is open. The reference's release behavior is achievable; automatic menu-specific preservation without reading or capturing game state is not. A bounded curve protects high input values only where this backend actually applies.

Acceptance: exact endpoints, continuity at t, monotonicity of all 256 entries, identity above t for h=0, zero residual boost after key-up/focus loss, tint bounds, baseline composition, failed/ignored ramp detection, HDR refusal and slow-call degradation. Require an on-screen grayscale ramp check on actual hardware.

### 3.5 Global hold bindings and hotkeys — F08/F10

Use `WH_KEYBOARD_LL=13` for press/release and optional `WH_MOUSE_LL=14` for side buttons. These low-level callbacks execute in the installing process; do not use injecting `WH_KEYBOARD` or in-context WinEvent hooks. Install on a dedicated pumping thread, retain delegates strongly, and always call `CallNextHookEx` without suppressing input. The low-level keyboard hook can be silently removed if too slow. [LowLevelKeyboardProc](https://learn.microsoft.com/en-us/windows/win32/winmsg/lowlevelkeyboardproc), [SetWindowsHookExW](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwindowshookexw).

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct KBDLLHOOKSTRUCT {
    public uint vkCode, scanCode, flags, time;
    public nuint dwExtraInfo;
} // x64: 24 bytes
[StructLayout(LayoutKind.Sequential)]
internal struct POINT { public int x,y; }
[StructLayout(LayoutKind.Sequential)]
internal struct MSLLHOOKSTRUCT {
    public POINT pt;
    public uint mouseData, flags, time;
    public nuint dwExtraInfo;
} // x64: 32 bytes (padding before pointer)

[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate nint HookProc(int code, nuint wParam, nint lParam);
[DllImport("user32.dll", EntryPoint="SetWindowsHookExW", SetLastError=true)]
static extern nint SetWindowsHookEx(int idHook, HookProc callback,
    nint module, uint threadId);
[DllImport("user32.dll", ExactSpelling=true)]
static extern nint CallNextHookEx(nint hook, int code, nuint wp, nint lp);
[DllImport("user32.dll", ExactSpelling=true, SetLastError=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool UnhookWindowsHookEx(nint hook);
[DllImport("user32.dll", ExactSpelling=true, SetLastError=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool RegisterHotKey(nint hwnd, int id, uint modifiers, uint vk);
[DllImport("user32.dll", ExactSpelling=true, SetLastError=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool UnregisterHotKey(nint hwnd, int id);
[DllImport("user32.dll", ExactSpelling=true)]
static extern short GetAsyncKeyState(int vk);
```

The two structures must use pointer-sized extra-info fields and default native packing. [Keyboard structure](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-kbdllhookstruct), [mouse structure](https://learn.microsoft.com/en-us/windows/win32/api/winuser/ns-winuser-msllhookstruct).

Set threadId=0 and obtain this executable's module handle via `GetModuleHandleW(NULL)` for the standard managed low-level hook pattern; check installation result and capture last error immediately on failure. Keep hooks in this process and retain the delegates until after unhook. Managed callback must catch exceptions at the boundary and enqueue only recognized configured bindings; never record keystroke history or text. Handle nCode<0 by immediate chaining. Key-down messages are 0x0100/0x0104 and key-up 0x0101/0x0105. Repeats do not retrigger. Track the configured key/chord state, including left/right modifiers, and release when **any** required chord component goes up. Modifier-only bindings are disallowed by default.

For Mouse 5 use WM_XBUTTONDOWN=0x020B / UP=0x020C and high WORD of mouseData: XBUTTON2=2 (XBUTTON1=1 is Mouse 4). No polling of cursor coordinates is needed. Ignore synthetic input by default (`LLKHF_INJECTED=0x10`, mouse injected bit=1), with an accessibility override and a visible synthetic-source state.

Use `RegisterHotKey` and `MOD_NOREPEAT=0x4000` for discrete preset selection, pause, restore and screenshot. `WM_HOTKEY` does not supply reliable key-up, so it is not a complete hold implementation. Detect conflicts and reserved Windows combinations; choose no hardcoded assumption that any proposed default is free. [RegisterHotKey](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-registerhotkey).

Hold safety: reset on foreground leaving an eligible game, lock/suspend, hook replacement, device/session loss, disabled effects, preview start or crash. Outside the hook callback, a 100 ms timer checks only configured held keys with GetAsyncKeyState to release a missed-up; it never activates a boost. Do not query asynchronous state inside LowLevelKeyboardProc because it has not yet been updated. After resume, require a fresh key-down. A Raw Input backend (`RegisterRawInputDevices`, `RIDEV_INPUTSINK`) can be offered for users avoiding low-level hooks, with the same reducer semantics and qualification; do not enable both sources concurrently.

Acceptance: keyboard and side mouse down/up, repeats, chord modifier release, focus change while held, synthetic input policy, hook thread blocked simulation, missing-up reconciliation, registered-key conflict, no swallowed game input and zero persisted keystrokes.

### 3.6 Display inventory, resolution, refresh, stretch and HDR — F11–F14

#### Interop strategy and signatures

Generate SDK bindings rather than hand-rolling CCD unions. The following are the exact native prototypes to target, provided by `User32.dll`; BOOL is 32-bit, LONG is a signed 32-bit return code, UINT32 stays 32-bit on x64. CCD/ChangeDisplaySettings functions return status values directly; do not replace them with GetLastError.

```cpp
BOOL WINAPI EnumDisplaySettingsExW(LPCWSTR device, DWORD modeIndex,
    DEVMODEW* mode, DWORD flags);
LONG WINAPI ChangeDisplaySettingsExW(LPCWSTR device, DEVMODEW* mode,
    HWND hwnd, DWORD flags, LPVOID param);
LONG WINAPI GetDisplayConfigBufferSizes(UINT32 flags,
    UINT32* pathCount, UINT32* modeCount);
LONG WINAPI QueryDisplayConfig(UINT32 flags, UINT32* pathCount,
    DISPLAYCONFIG_PATH_INFO* paths, UINT32* modeCount,
    DISPLAYCONFIG_MODE_INFO* modes, DISPLAYCONFIG_TOPOLOGY_ID* topology);
LONG WINAPI SetDisplayConfig(UINT32 pathCount, DISPLAYCONFIG_PATH_INFO* paths,
    UINT32 modeCount, DISPLAYCONFIG_MODE_INFO* modes, UINT32 flags);
LONG WINAPI DisplayConfigGetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_HEADER* packet);
LONG WINAPI DisplayConfigSetDeviceInfo(DISPLAYCONFIG_DEVICE_INFO_HEADER* packet);
```

[Mode enumeration](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-enumdisplaysettingsexw), [legacy mode application](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-changedisplaysettingsexw), [CCD query](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-querydisplayconfig), [CCD apply](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setdisplayconfig).

For a direct C# legacy binding, this is the display branch of DEVMODEW, with the printer union intentionally represented by its same-size display arm:

```csharp
[StructLayout(LayoutKind.Sequential, CharSet=CharSet.Unicode)]
internal unsafe struct DEVMODEW {
    public fixed char dmDeviceName[32];
    public ushort dmSpecVersion, dmDriverVersion, dmSize, dmDriverExtra;
    public uint dmFields;
    public POINT dmPosition;
    public uint dmDisplayOrientation, dmDisplayFixedOutput;
    public short dmColor, dmDuplex, dmYResolution, dmTTOption, dmCollate;
    public fixed char dmFormName[32];
    public ushort dmLogPixels;
    public uint dmBitsPerPel, dmPelsWidth, dmPelsHeight;
    public uint dmDisplayFlags, dmDisplayFrequency;
    public uint dmICMMethod, dmICMIntent, dmMediaType, dmDitherType;
    public uint dmReserved1, dmReserved2, dmPanningWidth, dmPanningHeight;
} // 220 bytes, dmFields offset 72, width offset 172, frequency offset 184

[DllImport("user32.dll", ExactSpelling=true, CharSet=CharSet.Unicode)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool EnumDisplaySettingsExW(string device, uint index,
    ref DEVMODEW mode, uint flags);
[DllImport("user32.dll", ExactSpelling=true, CharSet=CharSet.Unicode)]
static extern int ChangeDisplaySettingsExW(string? device, nint mode,
    nint hwnd, uint flags, nint param);
// Pass a pointer to the above unmanaged struct; nint.Zero allows restore overloads.
```

Keep dmDriverExtra=0 when allocating only this struct. If a driver supplies private extra bytes, allocate the complete buffer or discard the unsupported private-data path. Prefer enumerated records over manufacturing arbitrary fields. [DEVMODEW fields](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-devmodew).

Required CCD SDK structures, with their exact logical fields (use generated types for C unions/bitfields):

```text
LUID = DWORD LowPart; LONG HighPart                         [8 bytes]
DISPLAYCONFIG_RATIONAL = UINT32 Numerator, Denominator      [8]
DISPLAYCONFIG_2DREGION = UINT32 cx, cy                       [8]
DISPLAYCONFIG_DEVICE_INFO_HEADER = type:uint32, size:uint32,
    adapterId:LUID, id:uint32                              [20]
DISPLAYCONFIG_PATH_SOURCE_INFO = adapterId:LUID, id:uint32,
    modeInfoIdx:uint32 (union with cloneGroupId/sourceModeInfoIdx),
    statusFlags:uint32                                    [20]
DISPLAYCONFIG_PATH_TARGET_INFO = adapterId:LUID, id:uint32,
    modeInfoIdx:uint32 (union with desktopModeInfoIdx/targetModeInfoIdx),
    outputTechnology:enum32, rotation:enum32, scaling:enum32,
    refreshRate:RATIONAL, scanLineOrdering:enum32,
    targetAvailable:BOOL32, statusFlags:uint32              [48]
DISPLAYCONFIG_PATH_INFO = sourceInfo, targetInfo, flags:uint32 [72]
DISPLAYCONFIG_SOURCE_MODE = width:uint32, height:uint32,
    pixelFormat:enum32, position:POINTL                     [20]
DISPLAYCONFIG_VIDEO_SIGNAL_INFO = pixelRate:uint64,
    hSyncFreq:RATIONAL, vSyncFreq:RATIONAL, activeSize:2DREGION,
    totalSize:2DREGION, videoStandard/additionalSignalInfo:uint32-union,
    scanLineOrdering:enum32                                [48]
DISPLAYCONFIG_TARGET_MODE = targetVideoSignalInfo          [48]
DISPLAYCONFIG_DESKTOP_IMAGE_INFO = PathSourceSize:POINTL,
    DesktopImageRegion:RECTL, DesktopImageClip:RECTL         [40]
DISPLAYCONFIG_MODE_INFO = infoType:enum32, id:uint32, adapterId:LUID,
    union(targetMode,sourceMode,desktopImageInfo) at byte 16 [64]
```

Sizes above are for the native Windows x64 ABI. Also generate SOURCE_DEVICE_NAME, TARGET_DEVICE_NAME and TARGET_PREFERRED_MODE packets. Preserve enum widths, union overlays, BOOL width and unsigned rational denominators. Assert against a tiny Windows SDK native sizeof/offsetof harness, not only managed expectations. Source/target mode indexes are indexes into the returned mode array; they are not monitor IDs. [Target-path structure](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_path_target_info), [device-info header](https://learn.microsoft.com/en-us/windows/win32/api/wingdi/ns-wingdi-displayconfig_device_info_header).

#### Mode selection

1. Query buffer sizes then `QueryDisplayConfig(QDC_ONLY_ACTIVE_PATHS=2,...)`, topology pointer NULL. Retry the whole query on ERROR_INSUFFICIENT_BUFFER=122 with a bounded retry count. Keep the complete active configuration, including every unaffected monitor. Map adapter/source to GDI name (GET_SOURCE_NAME=1) and target to monitor path/friendly name (GET_TARGET_NAME=2). Detect clones and disclose shared-source constraints.
2. Enumerate each GDI display with modeIndex 0 upward, flags=0, dmSize=220. Capture ENUM_CURRENT_SETTINGS=0xFFFFFFFF separately. Exclude unsafe, interlaced-by-default and <32bpp modes; present supported width/height/refresh combinations. Retain timing metadata, not just display strings.
3. Simple single-display compatibility path: copy an enumerated DEVMODE, set `DM_PELSWIDTH=0x80000 | DM_PELSHEIGHT=0x100000 | DM_DISPLAYFREQUENCY=0x400000` and any legitimately changed field; call `CDS_TEST=2`; then `CDS_FULLSCREEN=4` for temporary application. CDS_FULLSCREEN is a temporary-mode flag, not a request to make a game exclusive fullscreen. No CDS_UPDATEREGISTRY during preview. Check DISP_CHANGE_SUCCESSFUL=0; other results fail the transaction.
4. CCD is the canonical path for multi-display changes, exact rational refresh and output scaling. Clone the full arrays, modify only selected source/target information, validate with `SDC_USE_SUPPLIED_DISPLAY_CONFIG=0x20 | SDC_VALIDATE=0x40`; apply with 0x20|`SDC_APPLY=0x80`. Validation and application are separate calls. Re-query and compare observed result. Do not routinely use SDC_ALLOW_CHANGES=0x400 because it allows Windows to change the requested result. An explicit “closest supported” action may use it and must show the result.
5. Refresh is rational: distinguish 60000/1001 from 60/1 and preserve valid target timing. Do not fabricate pixel clock, blanking or totals. If no compatible target timing exists, allow Windows best-mode selection only through an explicitly defined plan, then verify its actual result. Do not take an unrelated timing and only overwrite its refresh numerator.
6. Initial implementation uses non-virtual-aware CCD queries consistently. A later virtual-mode-aware implementation must correctly split the packed mode-index unions and pair matching QDC/SDC_VIRTUAL_MODE_AWARE flags; Windows 11 virtual-refresh awareness is similarly paired. Never add flags without implementing the corresponding structure interpretation. Preserve dynamic refresh policy rather than accidentally disabling it.

#### Stretched 4:3 and custom sizes

Use supported 4:3 source modes such as 1280×960 or 1440×1080 when enumerated. For stretch, set target path scaling to DISPLAYCONFIG_SCALING_STRETCHED=3 while keeping a compatible/native target output timing; aspect-preserving uses ASPECTRATIOCENTEREDMAX=4, identity=1, centered=2. Changing source dimensions while preserving output timing is different from transmitting a new physical 4:3 timing. Validate and re-query. `dmDisplayFixedOutput`/DM_DISPLAYFIXEDOUTPUT is a limited driver request, not a universal vendor GPU-scaling switch. Drivers and monitor OSDs can reject or override scaling. “Verified source mode + requested stretch” and “visually verified stretch” are separate statuses.

Generic Win32 does not install arbitrary custom timings. The local baseline supports custom-size **selection** when that size is enumerated. For full advertised NVIDIA custom-mode parity, add a separately enabled provider using the public SDK's `NvAPI_DISP_EnumCustomDisplay`, `NvAPI_DISP_TryCustomDisplay`, `NvAPI_DISP_RevertCustomDisplayTrial` and `NvAPI_DISP_SaveCustomDisplay` with SDK-versioned NV_CUSTOM_DISPLAY/NV_TIMING structures. Snapshot existing modes, trial under the rollback guard, and save only after local Keep. Never overwrite a different existing mode or invent timing fields; use qualified SDK timing generation/driver validation. Generate declarations from the pinned NVIDIA headers, with exact structure versions, rather than transplanting the private DVC ABI into these public functions. If unsupported, return Unsupported with instructions for the vendor's control panel; no EDID override, registry timing hack or driver installation. [NVIDIA display APIs](https://docs.nvidia.com/nvapi/group__dispcontrol.html).

#### HDR packet structure and version handling

HDR belongs to a target `(adapterId,targetId)`, not an HMONITOR or source ID. After each mode change, re-resolve that target. In old advanced-color packets, `advancedColorEnabled` is not a universal synonym for HDR on newer ACM/WCG desktops. Prefer the newer HDR-specific information when supported. The SDK declares packet types 9/10 for legacy get/set advanced color, 15 for GET_ADVANCED_COLOR_INFO_2 and 16 for SET_HDR_STATE. New packet ABI is defined by the [Windows SDK header](https://github.com/microsoft/win32metadata/blob/main/generation/WinSDK/RecompiledIdlHeaders/um/wingdi.h).

```csharp
[StructLayout(LayoutKind.Sequential)]
internal struct LUID { public uint LowPart; public int HighPart; }
[StructLayout(LayoutKind.Sequential)]
internal struct DISPLAYCONFIG_DEVICE_INFO_HEADER {
    public uint type, size;
    public LUID adapterId;
    public uint id;
}
[StructLayout(LayoutKind.Sequential)]
internal struct ADVANCED_COLOR_INFO {
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint flags;       // bits 0=supported,1=enabled,2=wide-enforced,3=force-disabled
    public uint colorEncoding, bitsPerColorChannel;
} // 32 bytes
[StructLayout(LayoutKind.Sequential)]
internal struct ADVANCED_COLOR_INFO_2 {
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint flags;       // bits 0=supported,1=active,3=policy-limited,
                             // 4=HDR supported,5=HDR user-enabled,
                             // 6=WCG supported,7=WCG user-enabled
    public uint colorEncoding, bitsPerColorChannel, activeColorMode;
} // 36 bytes; activeColorMode: SDR=0, WCG=1, HDR=2
[StructLayout(LayoutKind.Sequential)]
internal struct COLOR_STATE_PACKET {
    public DISPLAYCONFIG_DEVICE_INFO_HEADER header;
    public uint value;       // bit 0 only; reserved bits zero
} // 24 bytes; type=10 enables advanced color, type=16 enables HDR
```

Initialize header.size to the **whole packet**, not 20. Query type 15 on recent Windows 11 (qualify 24H2+) and use flags plus activeColorMode; ERROR_INVALID_PARAMETER/NOT_SUPPORTED means try the legacy capability path on qualified older builds. To set new HDR use type=16 and value=`enabled ? 1u : 0u`. For Windows 10/older Windows 11 use type 9 capability query and type 10 enableAdvancedColor only in the known legacy HDR case. If HDR vs WCG cannot be distinguished, return NeedsUserAction and open `ms-settings:display-advancedcolor`; do not toggle ACM by accident. Probe support at runtime rather than assuming header availability equals runtime support. No simulated Win+Alt+B or undocumented registry writes.

Read back both user-enabled and active state where available; policy, battery state, topology and monitor can prevent activation. Before enabling HDR restore owned SDR matrices/ramps/DVC, apply HDR and re-query the display inventory. In mixed SDR/HDR setups, default to suspending global Magnification entirely because it cannot exclude one HDR monitor; retain qualified per-output SDR-only controls. Re-enable profiles only after a verified SDR transition.

#### One-click transaction and rollback

One user action can request a compound profile, but no Windows API makes mode+HDR+scaling+color atomic. Model a compensating transaction: capture baseline → journal → suspend hold/auto switching → remove SDR effects if needed → set desired HDR prerequisite → rebuild/validate mode plan for new capabilities → apply mode/scaling → re-query → apply compatible colors → show local Keep/Revert for 15 seconds. Agent owns the deadline, independent of WPF. Failure or timeout compensates in reverse order, re-querying after each step. If rollback of an old topology is invalid after hotplug, recover connected displays to the saved Windows configuration and report degraded restoration; never loop through invalid topology indefinitely.

Recovery must already be alive before display mutation. Closing/crashing UI, screen going black, pressing Escape or deadline expiry all revert. A successful Keep records the intended profile; persistence into Windows' own database is a separate explicit option. Subsequent per-game mode switches use only a previously confirmed monitor/mode/HDR combination; a new or changed combination requires preview again. Automatic game focus can otherwise produce a black-screen loop.

Acceptance: unsupported mode fails before mutation; timeout works after UI death; complete topology preserved on a second monitor; rational refresh remains distinct; stretch verified against a geometric pattern; saved custom mode never occurs before Keep; HDR unsupported/policy-limited paths; hot-unplug during preview; lock/resume; repeated focus events do not repeat mode changes.

### 3.7 Foreground profiles, preset Library and game discovery — F05–F08/F17–F19/F30

Subscribe to `EVENT_SYSTEM_FOREGROUND=0x0003` only, with `WINEVENT_OUTOFCONTEXT=0`. Use no DLL and no WINEVENT_INCONTEXT. Keep your own foreground events so moving into the utility can be resolved deliberately; otherwise blindly skipping your process can leave a game profile active on the utility. Root the callback and pump messages. [SetWinEventHook](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-setwineventhook).

```csharp
[UnmanagedFunctionPointer(CallingConvention.Winapi)]
internal delegate void WinEventProc(nint hook, uint eventType, nint hwnd,
    int objectId, int childId, uint eventThread, uint eventTime);
[DllImport("user32.dll", ExactSpelling=true)]
static extern nint SetWinEventHook(uint min, uint max, nint module,
    WinEventProc callback, uint process, uint thread, uint flags);
[DllImport("user32.dll", ExactSpelling=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool UnhookWinEvent(nint hook);
[DllImport("user32.dll", ExactSpelling=true)]
static extern uint GetWindowThreadProcessId(nint hwnd, out uint processId);
[DllImport("kernel32.dll", ExactSpelling=true, SetLastError=true)]
static extern nint OpenProcess(uint access,
    [MarshalAs(UnmanagedType.Bool)] bool inherit, uint processId);
[DllImport("kernel32.dll", CharSet=CharSet.Unicode, ExactSpelling=true,
    SetLastError=true)]
[return: MarshalAs(UnmanagedType.Bool)]
static extern bool QueryFullProcessImageNameW(nint process, uint flags,
    System.Text.StringBuilder imagePath, ref uint chars);
```

Use `PROCESS_QUERY_LIMITED_INFORMATION=0x1000` only, a SafeHandle and `CloseHandle`. Flags=0 yields the Win32 executable path. This is a metadata query, not VM access. Protected/elevated processes may deny access; retain the desktop profile or an explicit user-approved fallback instead of escalating. [Window PID lookup](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-getwindowthreadprocessid), [executable-path lookup](https://learn.microsoft.com/en-us/windows/win32/api/winbase/nf-winbase-queryfullprocessimagenamew).

Callback posts HWND and event time. Worker waits a configurable 150 ms debounce, verifies the HWND still equals GetForegroundWindow, then resolves PID/path. Cache by PID plus process start identity, never PID alone. Default match is exact normalized absolute executable path, OrdinalIgnoreCase. Optional filename rules must be explicitly marked broad; no window-title substring matching by default. Resolve the window's monitor with MonitorFromWindow/GetMonitorInfo and match DisplayIdentity. Unknown processes, desktop and denied access resolve to desktop. If a game exits while foreground, an event plus a low-frequency liveness fallback clears it.

Profiles store separate ColorSettings, optional confirmed DisplayIntent, CrosshairSettings and DarkBoostBinding. One global Magnification profile follows foreground; it does not somehow stay on the game monitor. Per-monitor gamma/DDC scopes are explicit. Game-to-game switching replaces the active profile but preserves the original session baseline for eventual restoration. Grace transitions over utility dialogs can preserve colors for preview only when configured; hold boost always clears on focus loss.

Installed-game discovery is opt-in and local: read Steam install path from HKCU registry, parse `steamapps/libraryfolders.vdf` and appmanifest files in registered libraries with a bounded VDF parser. Do not recursively scan all disks or launch games. Offer manual executable picker, add/remove/disable rules and fix paths after updates.

Preset document schema v1: `schemaVersion,id,name,kind,color,crosshair,displayIntent,gameTag,authorMetadata,assets`. Kinds: Color, Crosshair, Resolution, FullSetup. Allow only known fields and enums, finite bounded numbers and enumerated display intents. Unknown major schema versions are rejected. Imported profiles never carry raw native function IDs, registry paths, executable commands, local absolute asset paths or another machine's hardware identifiers. Resolve portable display intents against this machine, show the exact proposed changes and require local confirmation before applying display modes or imported presets.

Use original built-ins with descriptive names and stored values; provide neutral plus treeline/interior/night/snow/smoke/bright-sky examples. They are editable starting points, not performance claims. Local export `.chromaticpreset` JSON is always available. Offline share code: `CD1-` + base64url of bounded compressed canonical JSON plus checksum; limit decompressed size to 64 KiB. Checksum detects corruption, not trust. Hosted short codes resolve to the independently deployed catalog, never PlexusX services. Register `chromaticdesk://preset/<code>` per-user; validate scheme/action, parse argv as data, and forward through the authenticated local pipe. Single-instance startup is scoped to the current session. Never use the reference's protocol scheme or code namespace.

Acceptance: rapid Alt-Tab, process exit/PID reuse, own app focused, access denied, duplicate filename rules, simultaneous devices, hold plus profile switch, unknown preset schema, oversized/decompression-bomb import, malicious image, spoofed deep link, mode preview after import. Local Library supports search/type/game filtering and offline cache. Publishing and moderation belong to §3.12's optional server.

### 3.8 External crosshair overlay — F15/F16

Create a separate, unowned, top-level `WS_POPUP=0x80000000` HWND. It must not be parented to a game. Extended styles are `WS_EX_LAYERED=0x00080000 | WS_EX_TRANSPARENT=0x00000020 | WS_EX_TOPMOST=0x00000008 | WS_EX_TOOLWINDOW=0x00000080 | WS_EX_NOACTIVATE=0x08000000`; never add WS_EX_APPWINDOW. TOOLWINDOW keeps it out of the taskbar/Alt-Tab; NOACTIVATE and `SW_SHOWNOACTIVATE`/`SWP_NOACTIVATE` prevent focus theft. For a layered window, TRANSPARENT passes mouse events through to underlying windows. Generic WS_EX_TRANSPARENT by itself is not a universal cross-process hit-test solution. [Styles](https://learn.microsoft.com/en-us/windows/win32/winmsg/extended-window-styles), [layered-window behavior](https://learn.microsoft.com/en-us/windows/win32/winmsg/window-features).

Use a small window covering only the crosshair bounds plus outline, rather than a transparent full-screen surface. Start hidden. Draw premultiplied BGRA32 into a top-down DIB and publish with `UpdateLayeredWindow(ULW_ALPHA=2)` and `BLENDFUNCTION{BlendOp=AC_SRC_OVER=0,BlendFlags=0,SourceConstantAlpha=255,AlphaFormat=AC_SRC_ALPHA=1}`. Update only on content/placement change. Reuse the bitmap, memory DC, brushes and pens; pair SelectObject with restoring the previous object before DeleteObject, and DeleteDC/ReleaseDC according to acquisition. This avoids a continuous render loop and minimizes GDI resource pressure. [UpdateLayeredWindow](https://learn.microsoft.com/en-us/windows/win32/api/winuser/nf-winuser-updatelayeredwindow).

Exact native surface (generate additional Win32 support types from SDK):

```cpp
HWND WINAPI CreateWindowExW(DWORD exStyle, LPCWSTR className, LPCWSTR title,
    DWORD style, int x, int y, int width, int height, HWND parent,
    HMENU menu, HINSTANCE instance, LPVOID parameter);
BOOL WINAPI SetWindowPos(HWND window, HWND insertAfter, int x, int y,
    int width, int height, UINT flags);
BOOL WINAPI UpdateLayeredWindow(HWND window, HDC dst, const POINT* dstPoint,
    const SIZE* size, HDC src, const POINT* srcPoint, COLORREF colorKey,
    const BLENDFUNCTION* blend, DWORD flags);
BOOL WINAPI DestroyWindow(HWND window);
// BLENDFUNCTION = four BYTE fields in the order above; size 4.
// POINT/SIZE are two signed 32-bit values; RECT is four signed 32-bit values.
```

Use `HWND_TOPMOST=(HWND)-1`, SWP_NOACTIVATE=0x10. Handle WM_MOUSEACTIVATE with MA_NOACTIVATE=3; WM_NCHITTEST may return HTTRANSPARENT=-1 as supplementary behavior, but the layered transparent style is the cross-process input guarantee being used. Register the class with a retained WndProc and avoid CS_OWNDC/CS_CLASSDC with a layered window. Do not combine SetLayeredWindowAttributes with the UpdateLayeredWindow route; doing so can invalidate subsequent updates until the style is reset.

Manifest enables PerMonitorV2 DPI awareness before any HWND creation. Overlay coordinates are physical pixels; WPF controls are DIPs. Resolve monitor physical rectangle through GetMonitorInfo; center=`left+width/2,top+height/2`, then apply signed pixel offsets. Negative monitor coordinates and odd dimensions are valid. Handle WM_DPICHANGED, WM_DISPLAYCHANGE and monitor reassignment. Default center is selected monitor; optional “follow foreground client” uses GetClientRect plus ClientToScreen and window-location events without manipulating the game. Reapply topmost placement on meaningful state changes only, not a constant z-order fight.

Drawn styles: cross, dot, circle, T and combined dot/cross; thickness 1–12 physical pixels, size 2–128, gap 0–64, outline 0–6, opacity 0–1, ARGB color and offsets bounded to the selected display. These are original ranges. Picture crosshair: local PNG only initially, maximum 4 MiB and 1024×1024 after decode; strip metadata, normalize to premultiplied BGRA, cache by hash, and preserve alpha. Both drawn and picture paths use the same final placement/offset function. Library images must pass the same parser, with no remote file references or active SVG/HTML.

Hide on lock, minimized/ineligible game, disabled overlay and preview interruption. Exclusive fullscreen, secure desktop and protected content may hide the overlay; offer borderless guidance, never bypass. Color effects may also alter the crosshair; no claim it can be excluded from full-screen Magnification. No automatic aim movement, enemy detection, recoil automation, injected rendering or game-state reading.

Acceptance: clicking and typing reach the underlying independent test process over every opaque crosshair pixel; no taskbar/Alt-Tab entry; no focus change; no update work when static; picture offsets work; mixed DPI/negative coordinates; 1,000 redraws without rising GDI handles; explicitly measured compositor/frame-time effect. Say “external static overlay,” not “zero-latency guaranteed.”

### 3.9 Phone control over local Wi-Fi — F20

Embed Kestrel inside App through a FrameworkReference to Microsoft.AspNetCore.App. Start only when the Phone window opens, after the user chooses a private, connected physical LAN interface. Use `NetworkInterface.GetAllNetworkInterfaces`, unicast address and prefix length; list multiple candidates, avoid guessing VPN/virtual/public interfaces, and show the chosen IP. Bind **only that exact local address**, not 0.0.0.0, +, a public interface or every adapter. An ephemeral port is preferred and read from the actual bound server addresses. Disable IPv6 in v1 unless its address/scope/prefix validation is implemented. No UPnP, port forwarding or internet relay.

```csharp
var builder = WebApplication.CreateBuilder();
builder.WebHost.ConfigureKestrel(k => k.Listen(selectedIPv4, 0));
var app = builder.Build();
// Install strict Host/Origin and IP-prefix checks before routing.
app.UseWebSockets(new WebSocketOptions {
    KeepAliveInterval = TimeSpan.FromSeconds(20)
});
// Serve embedded static assets only; implement /pair, /state and /ws below.
await app.StartAsync(cancellationToken);
```

This sketch omits application-specific handlers on purpose; never publish it as an unauthenticated server. WebSocket Origin must be validated explicitly; CORS does not authenticate or protect a WebSocket handshake. [ASP.NET Core WebSockets](https://learn.microsoft.com/en-us/aspnet/core/fundamentals/websockets?view=aspnetcore-10.0).

Pairing protocol:

1. Generate 32 random bytes with RandomNumberGenerator. QR contains `http://<ip>:<port>/#pair=<base64url>` so the secret stays out of URL request logs and Referer. Serve only local JS/CSS, no analytics/CDN. Set no-store, no-referrer, restrictive CSP and frame-ancestors 'none'. JS reads the fragment, removes it from history immediately and sends it in a bounded POST `/pair` body with same-origin and Host checks.
2. One-time secret expires in two minutes, is compared in fixed time, consumed once, and can be regenerated. Rate-limit pairing, for example five failed attempts per source per minute and a bounded global counter. After a valid request show the candidate device/IP locally and require “Allow phone” before issuing its session. QR possession plus local confirmation defines authorization.
3. Issue a random short-lived session token, held in phone page memory. `/state` requires Authorization; the WebSocket `/ws` upgrade checks Host, Origin, source subnet and a fresh one-use WS ticket obtained through an authenticated POST. Do not place a persistent bearer token in a query string. Reject unpaired sockets before accepting commands; cap total clients to two.
4. Only the advertised interface's prefix plus loopback is allowed. This is network scoping, not authentication. Validate Host equals advertised IP/port and Origin exactly equals the served phone-page origin; reject wildcard/null/foreign origins. Do not trust forwarded headers. Stop if interface/address/network profile changes. No CORS wildcard.
5. Closing Phone window or exiting/locking/suspending stops all sockets and the listener and invalidates every key/session. Reopening creates a new secret. The firewall helper, if needed, is a separately invoked operation scoped to the executable, selected port, Private profile and LocalSubnet; never silently add a global/Public allow rule or bypass a denied Windows prompt.

Threat model: plain HTTP/WebSocket on a private Wi-Fi is **unencrypted** and does not defend against a malicious on-path LAN peer. Pairing/auth checks prevent casual unauthorized requests, not passive interception. UI says “Use on a trusted private network.” Optional hardened deployment uses HTTPS/WSS with an independently trusted certificate; do not instruct users to bypass mobile certificate warnings. No account password, licence token or sensitive desktop data is exposed on this listener.

Versioned message example:

```json
{"v":1,"type":"setColor","clientId":"paired-session-id","seq":42,
 "baseRevision":17,"values":{"saturationPercent":230,"contrast":1.1}}
```

Allowed actions: set the ten documented color values, choose a locally permitted scene preset, get current effective color/capability state. No resolution/HDR, DDC/CI, hotkey capture, files, processes, screenshots, arbitrary preset download, software update or shell actions. Thus F20 follows the reference's color-only scope. Media remains local UI unless the user explicitly adds an independently secured media route later.

Validate all JSON before dispatch: maximum 4 KiB/message, bounded nesting, known keys only, finite numeric ranges and known preset IDs. Per client sequence strictly increases; acknowledgments include seq, authoritative revision, effective values and error. Agent serializes all writers. For concurrent phone/UI edits, reject stale baseRevision and return state for rebase; the phone keeps one write in flight and coalesces further slider movements. Throttle at 20–30 Hz, apply final pointer-up value, backpressure rather than unbounded queue. Broadcast authoritative state to both UIs. Socket reconnect gets state first, never replays old held or mode commands.

Acceptance: unauthenticated/expired/replayed pairing fails; wrong Origin/Host fails even with a valid socket URL; public NIC refused; browser page from another site cannot connect; server absent when closed; key rotation; two-client edit conflict; malformed/oversized/nonfinite payload; Wi-Fi IP change; rapid slider drags settle at the final acknowledged value.

### 3.10 Media controller using GSMTC — F21

Use Windows.Media.Control GlobalSystemMediaTransportControlsSessionManager, available from Windows 10 1809. v1 is a **controller for existing media sessions**, not a streaming service or a bundled music decoder. Source apps must publish media sessions and permit the requested operations. Enumerate sessions, provide a picker plus “current session,” and subscribe/unsubscribe to session, metadata, playback and timeline changes. Do not assume GetCurrentSession is always the user's intended app. [GSMTC manager](https://learn.microsoft.com/en-us/uwp/api/windows.media.control.globalsystemmediatransportcontrolssessionmanager?view=winrt-26100).

```csharp
using Windows.Media.Control;

var manager = await GlobalSystemMediaTransportControlsSessionManager.RequestAsync();
var sessions = manager.GetSessions();
var session = manager.GetCurrentSession();
if (session is not null) {
    var metadata = await session.TryGetMediaPropertiesAsync();
    var playback = session.GetPlaybackInfo();
    var timeline = session.GetTimelineProperties();
    bool accepted = await session.TryTogglePlayPauseAsync();
    // Other supported methods:
    // TryPlayAsync(), TryPauseAsync(), TrySkipNextAsync(), TrySkipPreviousAsync()
    // TryChangePlaybackPositionAsync(long positionIn100nsTicks)
}
```

Check playback.Controls capability flags before enabling play/pause/next/previous/seek. Position uses TimeSpan ticks (100 ns), clamped to current session timeline bounds; live streams can be unseekable. Check returned bool and refresh state, never optimistically claim the action succeeded. Marshal UI notifications to WPF Dispatcher. Thumbnails use bounded WinRT stream decoding and dispose streams. Timeline UI can update once per second while visible; no high-frequency polling when closed.

Probe RequestAsync at runtime and handle access denied/no sessions/source disappearance. Packaged builds declare `globalMediaControl` as required by the WinRT contract; unpackaged behavior must be verified for the chosen target and supported Windows builds. If unavailable, disable the panel with a reason; do not impersonate another package or scrape another app's private UI. GSMTC supplies no universal volume control; an optional future volume slider would need an explicitly scoped Core Audio endpoint/session controller. No claim that every browser tab or player is supported.

Acceptance: no player open, two simultaneous players, session picker, paused metadata, session vanishes during async operation, rejected transport command, unseekable live stream, packaged/unpackaged qualification and event-handler cleanup.

### 3.11 Per-monitor hardware controls and bounded watchdog — F22–F24

Map HMONITOR to physical monitors using the Dxva2.dll Monitor Configuration APIs; a logical monitor can expose more than one physical handle. Discover capability flags and DDC/CI capability strings before presenting controls. Native ABI:

```cpp
// Dxva2.dll; BOOL32 return values.
BOOL WINAPI GetNumberOfPhysicalMonitorsFromHMONITOR(HMONITOR monitor, LPDWORD count);
BOOL WINAPI GetPhysicalMonitorsFromHMONITOR(HMONITOR monitor, DWORD count,
    LPPHYSICAL_MONITOR monitors);
BOOL WINAPI DestroyPhysicalMonitors(DWORD count, LPPHYSICAL_MONITOR monitors);
BOOL WINAPI GetMonitorCapabilities(HANDLE physical, LPDWORD caps, LPDWORD temperatures);
BOOL WINAPI GetMonitorBrightness(HANDLE physical, LPDWORD min, LPDWORD current, LPDWORD max);
BOOL WINAPI SetMonitorBrightness(HANDLE physical, DWORD value);
BOOL WINAPI GetMonitorContrast(HANDLE physical, LPDWORD min, LPDWORD current, LPDWORD max);
BOOL WINAPI SetMonitorContrast(HANDLE physical, DWORD value);
BOOL WINAPI GetMonitorRedGreenOrBlueGain(HANDLE physical, MC_GAIN_TYPE channel,
    LPDWORD min, LPDWORD current, LPDWORD max);
BOOL WINAPI SetMonitorRedGreenOrBlueGain(HANDLE physical, MC_GAIN_TYPE channel, DWORD value);
BOOL WINAPI GetVCPFeatureAndVCPFeatureReply(HANDLE physical, BYTE code,
    LPMC_VCP_CODE_TYPE type, LPDWORD current, LPDWORD maximum);
BOOL WINAPI SetVCPFeature(HANDLE physical, BYTE code, DWORD value);
// PHYSICAL_MONITOR: HANDLE hPhysicalMonitor; WCHAR description[128]; x64 size 264.
```

Generate capabilities-string functions and typed enums too; do not guess VCP support. The high-level brightness/contrast/RGB-gain calls are preferred; advanced VCP writes are limited to a fixed audited allowlist after capability parsing. Never expose arbitrary MCCS codes, factory reset, power/input switching or firmware functions. Clamp to discovered ranges, read back, debounce to ~4 Hz and show errors without looping. DDC can be slow, ignored or hang in a driver; do not block the input/UI threads, and a managed timeout does not cancel an in-flight native call. Qualify problematic monitors or move this provider into a supervised helper process if process isolation is required. Destroy all physical handles on re-enumeration/shutdown. [Monitor configuration workflow](https://learn.microsoft.com/en-us/windows/win32/monitor/using-the-low-level-monitor-configuration-functions).

DDC hardware brightness/color and software gamma/warmth are separate labeled controls. Store original hardware values for an Undo button; persistence across app exit is the default for an explicit hardware change. Temporary per-game hardware profiles are disabled initially because cable operations are slow and persistence can surprise users. Internal laptop panels often lack DDC/CI; show Unsupported, with an optional later documented brightness interface rather than pretending GDI changes the backlight.

Watchdog: on WM_DISPLAYCHANGE, device notification, resume, foreground transition or detected native-handle invalidation, debounce, refresh inventory, check HDR state and compare readable owned state. Reapply only desired state for resources still owned, maximum three retries with increasing delays (250 ms, 1 s, 3 s). Cool down for 30 seconds after repeated failure. Optional idle verification every five seconds is low-cost and capability gated; never write every frame or constantly overwrite foreign color tools. If observed values differ in a way inconsistent with a known reset, pause the affected backend and expose a conflict. Slow-call metrics guide fallback. Watchdog cannot make an unsupported path supported.

Acceptance: unsupported DDC monitor, valid range scaling, monitor sleeping, multiple physical handles, hotplug handle release, repeated resets without retry storm, calibration conflict, restore of temporary software states and independent persistent hardware Undo.

### 3.12 Remaining application features and optional services — F19/F25–F27/F31–F38

**App appearance:** implement two original WPF resource dictionaries with dark/light accessibility, system text scaling, keyboard navigation and accessible names. Local background picture/GIF has file/decoded-frame/memory limits; cache and stop animation when window hidden or reduced motion is enabled. Do not reuse PlexusX artwork. Read saved window bounds and clamp them onto connected monitors. Tray behavior is explicit.

**Windows Customize:** distinguish stable API access from unsupported shell implementation details. Open `ms-settings:colors`, `ms-settings:taskbar` and `ms-settings:personalization` for system accent/taskbar options where no supported setter exists. An optional versioned legacy adapter can change the current user's `Themes\Personalize` DWORD values AppsUseLightTheme/SystemUsesLightTheme/EnableTransparency, or Explorer\Advanced\TaskbarAl on qualified Windows 11 builds. These registry values are implementation details, not stable Win32 contracts: read baseline, change only selected values, allow undo, broadcast WM_SETTINGCHANGE through SendMessageTimeoutW with a bounded timeout where applicable, and report when sign-out/restart is needed. Do not promise immediate Explorer refresh, terminate Explorer, edit policy, patch taskbar binaries or invent the publicly unspecified Explorer options. Default release uses Settings links for unsupported/unknown builds; direct toggles are opt-in compatibility features.

**Startup:** own HKCU\Software\Microsoft\Windows\CurrentVersion\Run value with a fully quoted executable path plus `--tray`; remove only that value on disable/uninstall. No scheduled task, SYSTEM account or elevation. **Protocol:** own HKCU\Software\Classes\chromaticdesk, URL Protocol and quoted `"app.exe" --uri "%1"`; no mutation of another application's registration. Treat `%1` as untrusted argv data, never a shell command. Use ShellExecuteEx only for fixed approved HTTPS/ms-settings targets; no arbitrary URI scheme launch from presets.

**Discord:** optional opt-in integration with local Discord RPC, fixed application ID owned by this project, bounded preset text and optional game disclosure. No account token scraping, process injection or auto-launch of Discord. If no supported client connection exists, hide activity without error loops. Core display features work with this dependency absent.

**Diagnostics:** local rotating logs, for example 7 files × 2 MiB, include own app build, OS build, backend result and timings. Redact full paths/usernames/tokens, no key contents beyond configured binding identifiers, no screen capture except user action. Crash handler does best-effort journal signal and writes a local report; it is not the only restore mechanism. “Report bug” builds a previewable diagnostic export and transmits only after an explicit Send to a configured independent endpoint. No upload endpoint exists in the offline build. Account/feature flags must not turn local logs into silent telemetry.

**Updates:** own HTTPS endpoint, pinned application-level release-signing public key, signed version/hash/size/channel manifest, Authenticode verification of expected publisher for installer/helper, bounded download to staging and atomic update with rollback. SHA-256 without authenticated metadata is not sufficient. Reject downgrade, tampered manifest, unexpected publisher and wrong architecture. Restore effects and finish/cancel mode transactions before updater execution; defer restart while a game is active. Credentials/signing private keys stay outside the repo. No endpoint configured means “automatic updates unavailable,” with working local app. Package release notes locally; never fetch the reference product's notes. Code-sign release binaries and publish checksums/SBOM; a document cannot supply a real signing certificate.

**Repair:** diagnostics first. No “run everything as admin” workaround for unsupported color paths. If a narrowly scoped operation such as a Private-profile firewall rule genuinely needs privilege, show exact change and invoke a fixed privileged helper through ShellExecuteEx `runas`. User can cancel. No privilege persistence.

**Independent optional cloud profile:** cloud identity/commerce is not needed for local utility parity. Catalog all reference behavior without secretly depending on its service. Implement own services only when configured:

| Service | Required contract and behavior |
|---|---|
| Catalog API | GET /v1/presets with type/game/sort/cursor; GET /v1/presets/{id}; authenticated POST/DELETE own preset; POST /reports; bounded JSON/images, rate limits, cache validators, moderation queue before image publication |
| Catalog website | Browse/filter/search, author/date, copy own short code, confirmed app deep link; do not auto-apply from website |
| Identity | OIDC authorization code + PKCE S256 through system browser; state/nonce; loopback 127.0.0.1 ephemeral callback limited to five minutes; close after success; no password embedded in desktop app |
| Entitlements | Own signed feature document with installation-random ID, feature list, expiry/version, public-key verification and defined offline grace; do not use CPU/disk/name fingerprinting by default |
| Activation | Explicit activate/release/move with authenticated ownership checks, idempotent request keys and audit trail; failure leaves local safety/restore functions available |
| Commerce | Provider-hosted checkout and billing portal, verified signed webhooks and idempotent order ledger; no card handling in app; original terms/privacy/retention policy required before production |
| Product status | Optional signed notices/minimum-version advisories; never remotely disable restoration or corrupt/remove user settings |

Do not implement reference licensing bypass or call reference activation, heartbeat, preset, payment, private API or update endpoints. No account required in local mode; `LocalFeaturePolicy` grants local features, while server mode is an explicit composition-root alternative. Optional cloud is a separate deployment requiring owned domains, identity-provider/payment configuration and moderation; report those setup requirements instead of fabricating working credentials. The public reference documents disabled update-rating controls; retain that as inactive, not an unsolicited telemetry feature. [Reference lifecycle/integration inventory](https://plexusx.lol/privacy).

**Packaging:** per-user installer/uninstaller, x64 self-contained initially, tested upgrade preserving JSON migrations. Uninstall asks whether to retain user data, restores temporary effects through the Agent, removes only own Run/protocol/firewall entries and never deletes game files. Signed executable paths and local socket/pipe limits are part of release review. CI can verify package hashes/contents without installing on a developer's active machine. Production signing and real-device compatibility remain release gates, not claims satisfied by a successful build.

### 3.13 Recording diagnostics and screenshot export — F28/F29

Do not attempt to hook OBS or game swap chains. A “capture-friendly” preference chooses software color and restores native DVC/LUT resources used only for appearance; nonlinear gamma/shadows may then be unavailable. A Magnification matrix is not a guarantee that a specific capture API sees processed colors. Display the route and remaining limitations. No undocumented DWM filter, capture-exclusion bypass or mirror overlay is used to fake parity.

Diagnostics draw an owned SDR test pattern, offer a user-triggered desktop capture and compare known sample regions; include a user visual confirmation. Capture can come from a documented desktop API such as Windows.Graphics.Capture with the normal consent flow, or a qualified GDI desktop BitBlt path. Test only owned patterns and reported capture methods. Results are “effect observed,” “effect absent,” or “inconclusive”; a capture result is not proof of the physical output or another recorder's pipeline. Continuous game screenshot analysis is outside scope. [Reference recording feature](https://plexusx.lol/guides/colours-missing-from-recordings).

For screenshot hotkey, capture on explicit user action only, save to a user-visible Pictures directory and copy a PNG/bitmap to clipboard. If captured content is already matrix-processed, never apply it twice. Maintain a tested route classification. For raw captures, apply the same software reference matrix/tonal curve explicitly and label export “processed preview,” not an exact physical monitor capture. Native NVIDIA vibrance, hardware monitor controls and arbitrary calibration cannot be reconstructed exactly from known slider values; disclose that. When route is unknown, export raw plus optional processed preview rather than guess. Preserve alpha/metadata responsibly; HDR capture export is outside SDR v1 or requires a separate color-managed implementation.

Acceptance: explicit-only capture, clipboard busy failure, denied permission, already-processed detection, raw/preview distinction, no double filtering, bounds/file naming, no silent upload and clear unsupported HDR/native-DVC parity.

### 3.14 Verification matrix and definition of done

| Area | Automated tests | Hardware/manual release evidence |
|---|---|---|
| Color math | Identity/grayscale/300%, orientation, affine composition, alpha, finite checks, CVD transpose | SDR pattern on Intel/AMD/NVIDIA; Magnifier conflict |
| LUT | Monotonicity, endpoints, baseline interpolation, high segment unchanged | Per-output ramp read/write, ignored-call case, latency and calibration conflict |
| Input | Reducer press/up/repeat/chord/focus tests; callback allocation budget | Side mouse, elevated game behavior, lock/resume, missed-up recovery |
| Display | Mock validate/apply/failure compensation, 15-second deadline independent of UI | 60 vs 59.94, high-refresh, square/4:3 source, multi-display, HDR and hotplug |
| Foreground | Debounce, PID reuse, denied query, matching, restore | Rapid game/desktop Alt-Tab and game close |
| Overlay | Premultiplied alpha/placement/image validator | Independent-process click-through, mixed DPI, GDI resources, exclusive/borderless limitations |
| Phone | Pair/expiry/replay/Origin/Host/subnet/schema/sequence/size/rate/concurrent edit | iOS/Android browser, Private firewall, Wi-Fi loss, listener teardown |
| Media | No-session/rejected command/session-loss adapter tests | Two real players, browser media, packaged/unpackaged support |
| Persistence/recovery | Atomic write failure, corrupt settings, migrations, ACL, agent/UI crash | Terminate UI/Agent under effect, black-screen preview rollback, sleep/unplug |
| Release | Reproducible locked build, dependencies/licences, manifest tamper, update rollback | Signed installation/upgrade/uninstall on fresh supported Windows images |

Normal CI runs pure and mocked integration tests only. A test cannot change the developer's physical desktop unless launched with an explicit `--allow-display-changes` hardware-harness switch after a visible countdown and active rollback companion. Automated smoke tests should use an isolated VM or test account. Do not treat passing mock tests as GPU compatibility evidence.

Definition of done: every F-ID is implemented, deliberately optional, inactive-by-reference, or explicitly unsupported with an actionable reason; traceability records the exact status. No hidden stubs return success. All seven core modules have real implementations for their supported paths, fault handling, baseline restoration and test evidence. Unknown hardware behavior remains labeled unqualified. No developer machine is reconfigured while merely generating the code.

## Section 4 — Seven sequenced, self-contained Codex prompts

Save this specification at `docs/TECHNICAL-SPECIFICATION.md` in the new repository **before Prompt 1**. Each prompt below can run in a new chat against that repository. The file, schemas, tests and `IMPLEMENTATION-STATE.md` carry context across chats; the agent must not depend on previous chat history. These are seven implementation stages, not seven messages asking the agent to output an entire repository as text. Let it write and test real files. Each prompt intentionally scopes work so that the result is reviewable and compiles before the next stage.

Use the prompts in order. When a stage has unfinished work, resume that stage from its saved checkpoint before moving to the next. Context loss must not silently convert missing implementation into success. All stages forbid changing the current physical desktop during ordinary development; native mutation is exercised only through the explicit hardware harness.

### Prompt 1 of 7 — Foundation, contracts, state and persistence

```text
Build stage 1 of Chromatic Desktop, an independently branded Windows 10/11
display utility. Work in this repository and write code; do not just propose it.
Read docs/TECHNICAL-SPECIFICATION.md Sections 1, 2 and 3.1, then inspect any
AGENTS.md and existing files. If IMPLEMENTATION-STATE.md exists, resume from it.
No prior conversation is required. The specification is the normative contract.

Use C# WPF on .NET 10 LTS, native x64, target Windows 10 build 19041 or newer,
with runtime checks for newer APIs. Pin an installed serviced SDK and packages.
If the required SDK is missing, prepare the files and report the exact build
prerequisite; do not silently downgrade the production target to .NET 8.
Never access game VM memory, inject DLLs, hook DirectX, edit game files, install
a driver, require normal elevation, or promise anti-cheat compatibility.
Do not change physical display/registry/firewall settings while developing.

Create the solution/project boundaries from Section 2. Implement Core models,
typed commands/results/capabilities, immutable reducer and backend planner;
settings/profile/preset/recovery/IPC schemas; atomic versioned JSON stores,
last-good recovery and bounded validation. Initial colors and optional services
are off. Distinguish global matrix scope from per-monitor resources.
Create the WPF shell/navigation and original themes with dependency injection,
accessible names, explicit close-vs-tray behavior and a capability/status model.
Backend implementations belong to later stages; report NotImplemented in this
development checkpoint, never return fake native success.

Define current-user/session authenticated named-pipe contracts, revision ordering,
owned-resource baselines, preview transaction states and recovery-journal types.
Implement the pure reducer including hold as a temporary modifier, stale command
rejection, suspension priority and restore. Provide injectable fake backends for
tests. Add meaningful tests for reducer transitions, corrupt/failed atomic writes,
schema/range validation and migrations. Do not write brittle pixel/layout tests.

Create AGENTS.md describing the safety constraints, module boundaries, expected
build/test commands and requirement to keep durable progress notes. Populate
REQUIREMENTS-TRACEABILITY.md for F01-F38 with current stage/status, not claims.
Build and run non-hardware tests if the SDK is available. Repair errors in scope.
Finish with implemented files/behavior, actual test results, prerequisites and
next stage. Update IMPLEMENTATION-STATE.md with exact unfinished items, contract
decisions and commands. Do not proceed to stage 2 in this turn.
```

### Prompt 2 of 7 — Color engines, native vibrance and recovery

```text
Implement stage 2 of Chromatic Desktop in the existing repository. Read
AGENTS.md, docs/IMPLEMENTATION-STATE.md and specification Sections 3.1-3.4 plus
their F-ID requirements. Treat repository files as durable context; do not rely
on previous chat history. Finish unfinished stage-1 prerequisites first.
Maintain .NET 10 WPF/x64 and the existing interfaces. No game-memory access,
injection, DirectX hooks, drivers or anti-cheat guarantees. Do not mutate the
developer's physical desktop during build/tests.

Implement Chromatic.Native bindings and ABI assertions. MAGCOLOREFFECT is 100
bytes of row-major floats; [R G B A 1] multiplies on the left, translation is
row 5, alpha is preserved. Implement s=percent/100, 0..300%, luma weights
.2126/.7152/.0722, Sij=(1-s)*w[input]+s*delta(input,output). Verify the numeric
300% matrix from the specification. Implement brightness/contrast/warmth,
single composed matrix and the specified approximate color-accessibility mode,
with provenance for simulation coefficients and honest approximation labeling.

Implement the STA Magnification backend: initialize, capture existing matrix,
apply/readback, restore captured baseline, uninitialize; do not change zoom or
input transform. One global matrix is not independent per-monitor saturation.
Implement optional SDR per-output gamma ramps (3x256 WORDs), baseline sampling,
normal gamma, shadows/highlights and bounded Dark Boost f(x)=x+a*x*(1-x/.45)^2
below .45, identity above it, 0<=a<=.8; include basic affine fallback explicitly.
Gamma cannot implement saturation; HDR/ACM and slow/ignored ramps disable it.

Implement optional private NVIDIA DVC using the installed System32 nvapi64.dll,
nvapi_QueryInterface, checked Cdecl delegates and the specification's interface
IDs/20-byte versioned structure. Map the display-driving output, query ranges,
save current level and restore it. Never import SetDVCLevelEx as a named DLL
export or pass a physical GPU handle. Missing/unsupported/invalidated/private
ABI cases degrade to software; no blind interface probing. Separate native DVC
scale from saturation; no accidental double application.

Implement Agent, serialized coordinator, authenticated local pipe, ownership,
atomic journal, UI-liveness shutdown and Recovery companion before any live
display mutation can be enabled. A restore uses the actual baseline and never
has two simultaneous writers. Handle conflicts with other color software.
Wire Display controls and diagnostics to real backend results.

Test math/ABI, monotonic curve/endpoints, composition, failure and crash recovery
with fake adapters/child processes that do not mutate hardware. Add an explicit
opt-in hardware harness with rollback/countdown, but do not run it. Build/test,
fix errors, update F-ID traceability, compatibility gaps and durable progress.
Stop after stage 2 and report real validation rather than claiming GPU support.
```

### Prompt 3 of 7 — Hold input, presets and automatic game profiles

```text
Implement stage 3 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.5 and 3.7, including referenced Core contracts.
Resume prerequisite work before extending it. .NET 10/x64 remains the target.
Use only OS metadata/events, never game VM access, DLL injection, game files,
DirectX hooks, drivers or automated game input. Do not alter live display state
while running ordinary tests.

Implement dedicated pumping-thread WH_KEYBOARD_LL press/up handling and optional
WH_MOUSE_LL Mouse 4/5 holds, strongly retained delegates and unconditional
CallNextHookEx. Callbacks do only bounded state extraction/enqueue and catch
exceptions. Repeats do not retrigger. Chord component release ends the hold.
Use RegisterHotKey with conflict reporting for preset toggles/pause/restore and
screenshot action dispatch; WM_HOTKEY is not a key-up mechanism. Ignore injected
input by default with an explicit accessibility option. Clear hold on focus
loss, lock, suspend, preview, disable or disconnect; reconcile missed release
outside callbacks by polling only configured held keys. Never log typed text.

Implement EVENT_SYSTEM_FOREGROUND SetWinEventHook OUTOFCONTEXT, debounce and
GetWindowThreadProcessId -> OpenProcess(PROCESS_QUERY_LIMITED_INFORMATION only)
-> QueryFullProcessImageNameW; close handles and handle denied/vanished process.
Match normalized exact executable paths, cache with process identity, resolve
own/unknown windows to the configured desktop behavior and avoid PID reuse bugs.
No title-only matching or automatic privilege escalation. Global matrix profile
scope remains all displays; per-output settings retain explicit monitor scope.

Implement local profile/preset CRUD, original built-ins, editable game bindings,
local Steam library/manifest discovery plus manual executable picker. Add typed
Color/Crosshair/Resolution/FullSetup imports/exports, bounded offline share codes
and the project's own chromaticdesk:// link parser/single-instance handoff.
Never contact PlexusX services or use its code namespace. Deep links/imports
show a confirmation diff; unconfirmed resolution changes remain pending until
the display-preview implementation from stage 4. No fake apply success.

Wire Presets/Library and Games pages with preview, reset, duplicate, search/type/
game filtering and preset hotkeys. Save base profile separately from held boost;
release restores the newest base settings. Add tests for rapid focus transitions,
process identity, denied queries, chord/repeat/missed-up, profile-while-held,
malicious and oversized preset payloads and decompression limits.
Build/test, repair in-scope failures, update traceability/progress and list manual
input/GPU checks still required. Stop after stage 3.
```

### Prompt 4 of 7 — Display transactions, HDR and monitor hardware

```text
Implement stage 4 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md,
specification Sections 3.6 and 3.11 and restore/ownership invariants in 3.1.
Keep .NET 10/x64 and SDK-generated CCD unions/bitfields. No game injection,
memory access, file modification, drivers, undocumented DWM color transforms
or anti-cheat guarantees. Development tests must not change physical displays.

Implement display enumeration/mapping through QueryDisplayConfig/device-info
packets and EnumDisplaySettingsExW. Keep complete topology, monitor identities,
source vs target IDs and rational refresh. Verify DEVMODEW/CCD/HDR packet sizes
and offsets against the Windows SDK. Retry topology query buffer races safely.
Implement legacy CDS_TEST then temporary CDS_FULLSCREEN path and canonical CCD
SDC_VALIDATE then APPLY path. No persisted mode before Keep, no fabricated
timings, no treating return codes as GetLastError, no dropping other monitors.

Implement supported 4:3 source modes with requested output stretch/scaling,
readback and honest driver limitations; a desktop change cannot force game FOV.
Custom-size selection works only for enumerated sizes by default. Add the
separately enabled public-NvAPI custom-mode provider if qualified, with SDK
structures, trial/revert and save only after Keep; otherwise return Unsupported
with a vendor-settings route. Never use EDID hacks or install custom drivers.

Implement per-target HDR probing, new information type 15 and HDR set type 16
where supported, legacy type 9/10 only for qualified older HDR paths, and no
confusion of ACM/WCG with HDR. Restore SDR effects before HDR; re-query after
every topology change. Mixed HDR/SDR suspends the global matrix by default.
No simulated HDR keyboard shortcut or registry toggle.

Implement compound mode/HDR/scaling preview as a journaled compensating
transaction with independent Agent 15-second deadline, local Keep/Revert,
Recovery already active, stale-queue fencing and rollback after UI death or
hotplug. Only previously confirmed combinations may auto-apply per game.
Wire Resolution view and confirmation UI. Never invoke the hardware harness now.

Implement capability-gated DDC/CI brightness/contrast/RGB gains, handle lifetime,
discoverable ranges, readback, bounded worker queue and explicit persistent Undo.
No arbitrary VCP access. Build event-driven color watchdog with bounded retry/
cooldown and foreign-writer conflict handling. Wire Monitor page.
Test transaction failure at each step, timeout independent of UI, topology
preservation, HDR packet selection, device identity and retry bounds with mocks.
Build/test, update F-ID/status/checklist evidence and exact unqualified hardware
paths in durable state. Stop after stage 4.
```

### Prompt 5 of 7 — Crosshair, capture and the remaining local UI

```text
Implement stage 5 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.8, 3.12 (local UI/integrations), and 3.13.
Keep independent branding, .NET 10/x64 and all existing safety contracts.
No game-state inspection, VM access, injection, DirectX detours, drivers or
guarantee of zero latency/anti-cheat approval. Do not reconfigure the user's
desktop, registry, startup or firewall while developing or smoke-testing.

Implement external unowned WS_POPUP crosshair HWND with LAYERED|TRANSPARENT|
TOPMOST|TOOLWINDOW|NOACTIVATE, no APPWINDOW, premultiplied BGRA top-down DIB
and UpdateLayeredWindow. Keep it small/event-driven, hidden initially, click-
through over opaque pixels, absent from taskbar/Alt-Tab and unable to steal
focus. Implement shapes/dot/gap/thickness/outline/color/opacity, sanitized PNG
images and one shared offset/placement path for drawn and picture crosshairs.
Use physical monitor/client coordinates, PerMonitorV2 DPI, negative coordinates
and hotplug handling. Dispose GDI objects correctly. Hide on suspension or
ineligible game; exclusive-fullscreen support is not promised. Wire the editor.

Implement user-triggered screenshot/clipboard export and recording diagnostics.
Separate raw capture, already-processed capture and explicitly processed preview;
never apply color twice or claim exact reconstruction of native DVC/DDC output.
Use documented capture APIs and their permissions. No continuous game capture,
silent upload or false assurance that OBS/game capture sees every screen effect.
Capture-friendly mode changes backend plan with explicit missing nonlinear features.

Finish original themes, accessible keyboard navigation, bounded local image/GIF
background, window placement, tray/Pause/Restore/Exit and opt-in startup/protocol
registration adapters. Customize opens supported Windows Settings pages by
default; optional version-gated registry compatibility adapters must capture/
restore only requested per-user values and report shell refresh limitations.
No Explorer termination, binary patching or broad privilege workaround.
Implement optional disabled-by-default Discord RPC, local logs/crash diagnostics,
previewable diagnostic export and Legal/licences view. No outgoing report until
an explicit Send to the project's configured endpoint. Cloud remains optional.

Test placement, image limits, alpha math, lifecycle and capture-route decisions.
Use a separate owned test process for click-through/focus checks in an isolated
UI environment; record unrun checks honestly. Build/test, fix errors and update
traceability, manual verification list and IMPLEMENTATION-STATE.md. Stop at 5.
```

### Prompt 6 of 7 — Authenticated phone control and media sessions

```text
Implement stage 6 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md
and specification Sections 3.9 and 3.10 plus the shared coordinator contracts.
Continue .NET 10/x64. No game memory/injection/hooks/drivers, no arbitrary remote
execution and no physical display/firewall/network exposure in ordinary tests.

Implement embedded Kestrel Phone server bound only to a user-selected Private
LAN IPv4 address and actual ephemeral port. It starts only while Phone window
is open and stops/invalidates all sessions on close, lock, suspend, app exit or
network change. No 0.0.0.0/public adapter/UPnP/relay. Use local embedded assets.
Implement QR with 256-bit one-time fragment secret, two-minute expiry, local
Allow-phone confirmation, token/ticket authentication, bounded rate limits,
Host and exact Origin checks including WebSocket upgrade, source-prefix scope,
no wildcard CORS, secret-free logs, CSP/no-referrer/no-store and fresh reopen keys.
Plain HTTP/WS is explicitly trusted-private-network-only; it does not protect
against a malicious on-path peer. Do not bypass certificate or firewall prompts.

Implement color-only command whitelist: bounded color sliders and allowed local
scene presets plus state. No resolution/HDR, files, shell, DDC, screenshot,
update or raw native calls. Enforce 4-KiB frames, schema/finite/range validation,
paired sessions, increasing seq, authoritative revision/ack, stale-revision
conflict/rebase, one write in flight with latest-value coalescing, final pointer-
up value, 20-30 Hz cap and two-client limit. Both UIs consume coordinator state.
Write responsive mobile controls and clear connection/capability status. Any
firewall helper is user-invoked, Private/LocalSubnet-scoped and never run now.

Implement GSMTC manager/session picker, current-session option, metadata/art,
capability-gated play/pause/next/previous and seek in 100-ns ticks. Subscribe to
events, handle disappearing/rejecting sessions, no sessions/access denied and
UI-thread dispatch. Verify packaged capability and unpackaged runtime behavior;
report unsupported cases. GSMTC is a controller, not an audio streaming engine,
and exposes no universal volume control. Wire the local Music panel.

Test pairing replay/expiry, malicious Origin/Host, schema/rate/size limits,
session teardown, two-client ordering, reconnect/no stale replay and mock media
failure/lifecycle. Loopback ephemeral integration tests may run without opening
LAN access or changing the desktop. Build/test and update state/traceability.
List real-phone/media/hardware qualification still required. Stop at stage 6.
```

### Prompt 7 of 7 — Integration, release packaging and completion audit

```text
Complete stage 7 of Chromatic Desktop. Read AGENTS.md, IMPLEMENTATION-STATE.md,
the entire specification's requirement inventory and Section 3.14. Audit code
against F01-F38 rather than assuming earlier stages succeeded. Keep .NET 10
WPF/x64, independent branding, no game memory/injection/DirectX hooks/drivers,
and no guarantee of universal anti-cheat, HDR grading or capture parity.

Finish missing supported local utility behavior and real fault paths. Every
feature must be Implemented, Optional, Inactive-by-reference, Unsupported with
reason, or Not yet complete. No hidden TODO or adapter returning fake success.
Verify colors/native DVC/LUT do not double-apply, hold release wins over stale
queued commands, foreground restoration works, display previews survive UI death,
overlay does not steal clicks/focus, phone server tears down, and shutdown/lock/
resume/hotplug restore correctly. No ordinary test mutates physical settings.

Implement per-user installer/upgrade/uninstaller, original protocol and opt-in
startup cleanup, dependency lock/licence notices/SBOM and reproducible publish.
Implement the project's updater with authenticated signed manifest, file hash/
size/architecture checks, expected publisher validation, staging/rollback,
restore-before-update and defer-during-game. No private signing keys in repo.
When real signing/hosting is absent, provide working packaging scripts and an
explicit release prerequisite; do not manufacture a valid production signature.

Keep local mode offline and unlocked. Build optional-cloud contracts and an
independent reference deployment scaffold only if requested/configured: bounded
moderated catalog, own preset codes, OIDC+PKCE loopback sign-in, signed feature
documents and provider-hosted commerce with verified webhooks. Never call
PlexusX APIs or bypass its licensing. The absence of cloud credentials must not
break local utility functions. Do not turn disabled reference update ratings into
telemetry. Do not deploy, purchase certificates, publish or send reports now.

Run the appropriate full non-hardware test suite and isolated smoke/package
checks. Run security-focused input/IPC/LAN/import/update/recovery fault tests.
Fix relevant failures without weakening safety contracts. Do not claim that
passing tests qualifies untested GPU/monitor/anti-cheat combinations. Prepare
MANUAL-TESTS.md and COMPATIBILITY-MATRIX.md with actual Tested/Untested results,
including AMD/Intel/NVIDIA, mixed DPI/multi-monitor, SDR/HDR, 59.94/60/high refresh,
borderless/exclusive limits, phone browsers, DDC and GSMTC. Hardware harness is
explicitly opt-in with active rollback; do not run it on this desktop now.

Deliver the buildable repository, exact build/run/publish instructions, complete
requirement traceability, known limitations, actual test results and remaining
production gates. Update IMPLEMENTATION-STATE.md so another chat can resume
without hidden context. If any required implementation is incomplete, state it
plainly and keep completion status open; do not label a scaffold production-ready.
```

These prompts produce a production-oriented implementation path. Actual production readiness is established by the release gates and hardware qualification, not by wording a prompt or compiling a prototype.

