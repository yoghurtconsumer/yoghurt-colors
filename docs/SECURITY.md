# Stage 2 process and ownership boundary

No game VM access, injection, DirectX interception, game-file changes, driver
installation, automatic elevation or anti-cheat approval claims. Ordinary tests
use injected memory/file resource adapters. No physical display, registry,
startup, firewall or LAN changes were made.

App starts fixed sibling Agent and Recovery executables. Secrets travel through
a bounded inherited stdin bootstrap, never command-line arguments or files.
Both pipe peers verify OS process ID, creation time, token SID and interactive
session before exchanging a random 256-bit nonce. Authentication is one-shot
and nonce comparison constant-time. Server DACL grants only the current SID;
PIPE_REJECT_REMOTE_CLIENTS prohibits remote connections independently of DACL.
Clients connect only to ".". First-instance protection prevents pipe squatting.

Frames use a bounded four-byte little-endian length and strict UTF-8 JSON,
64 KiB maximum, depth 32, exact known fields/enums and typed commands. There
are at most 32 in-flight correlated requests, one response reader and serialized
frame writes. Timeouts tear down the connection. Recovery's separate channel
permits only GetState, Restore and Shutdown. No IPC command exposes raw pointers,
native handles, arbitrary library loading, shell execution or arbitrary registers.

Agent alone owns normal writes on its STA. The reducer accepts safety commands
before slow disk/native completion; immutable revision AND ownership-generation
fences are checked after journal flush immediately before setters. A desired-state
acknowledgment is distinct from backend completion and observed state.

Before each first mutation, the actual baseline is captured. The atomic journal
records user/session/process identity, resource identity, baseline, last confirmed
and prospective pending value. Readback is mandatory. Restoration accepts only
our confirmed/pending value or an already-restored baseline. A foreign value
stops ownership and persists Conflict; no background retry fights another utility.
Corrupt journals and older backup-only journals require local review.

Process supervision pins OS handles after validating the exact launched image,
SID/session and creation time; it does not trust a PID alone. Agent observes UI
and Recovery termination. Recovery heartbeat queries pass through the real STA.
A timeout requests IPC shutdown first; only the pinned, fixed-image Agent can
then be terminated after a grace period. Recovery waits for actual termination
and acquires an exclusive OS-held writer lease before any restoration. No
PROCESS_VM_* or ALL_ACCESS rights are requested. Metadata queries use limited
query rights; synchronize/terminate rights are confined to the known Agent.

An unfinished journal is offered for local recovery before a new writer starts.
Recovery-only mode refuses a still-live or unverifiable prior Agent. Matching
an old pending value is not permission to ignore process/lease ownership.

Settings/profile/preset stores remain bounded to 1 MiB, schema/semantic validated,
with duplicate-key rejection, explicit version migration and same-directory
flush/atomic replace. Last-good settings can load with a warning while damage is
preserved; recovery deliberately refuses that fallback. Files inherit the current
user profile's permissions. Atomic replacement cannot guarantee every storage/
power-loss scenario. Reviewed data export, installer ACLs and the final release
security audit remain later gates.

Native DLL loading is limited to Windows APIs and installed System32 nvapi64.dll.
Only pinned known private DVC IDs are resolved; no downloaded/probed driver code.
HDR/ACM/unknown mappings fail closed. Resource constructors do not mutate hardware.
No phone, firewall or external service listener is enabled.
