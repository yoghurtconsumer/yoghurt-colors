# Stage 2 capabilities

A capability describes whether an operation can be attempted through the guarded
route. ApplyResult.Success requires completed readback; it never means GPU,
visual, capture or anti-cheat qualification. All effects and optional services
start off. Native constructors/default unconfigured backends fail closed.

| Component | Implemented behavior | Gate / limit |
| --- | --- | --- |
| Global Magnification | STA initialization, captured baseline, composed matrix, readback, exact restore | Authenticated Agent + live Recovery + exclusive writer lease + journal + verified SDR; existing global filters are declined |
| NVIDIA DVC | Known private ABI, System32 load, GDI output mapping, discovered ranges, restore and software fallback | Explicit experimental opt-in and single selected output; private ABI/driver support is unqualified |
| SDR gamma/warmth | Display DC, baseline-composed tonal ramps, readback and measured setter latency | Explicit opt-in and output; HDR/ACM/unknown mapping refused; slow/ignored route disabled; multiple outputs blocked pending shared-ramp qualification |
| Dark Boost | Pure curve/tint/baseline composition and injected qualified gamma backend | Live hold unavailable until latency qualification and Stage 3 input work; basic affine lift is labeled as such |
| CVD modes | Adjustable red/green/blue-weak approximation in the global matrix | Experimental encoded-SDR approximation; see COLOR-PROVENANCE.md |
| Local IPC | OS-authenticated current-user/session local pipes, bounded typed frames, nonce and concurrent correlation | Fixed peer processes, no public raw native operations; Recovery channel is read/restore/shutdown only |
| Recovery | Journal-before-write, pending/confirmed values, conflict checks, pinned Agent death then exclusive lease | No blind restore from older backup or foreign/conflicting values |
| Display inventory | Read-only CCD mapping and legacy advanced-color safety probe | Full enumeration/mode/HDR changes remain Stage 4 |
| Modes / HDR / DDC | NotImplemented | Stage 4 guarded transactions and hardware qualification |
| Overlay / capture | NotImplemented | Stage 5 |
| Phone / GSMTC | NotImplemented, no network listener | Stage 6 |
| WPF / themes / local settings | Implemented; launcher render checks and explicit Apply | No automatic desktop mutation from sliders or startup |

Magnification is global across the desktop. A monitor selection scopes only its
independent LUT/DVC intentions. Gamma never substitutes for saturation. Native
and software vibrance are never silently applied twice. See COMPATIBILITY-MATRIX.md
for what has actually been tested.
