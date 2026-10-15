# Stage 1 security boundary

No game VM access, code injection, DirectX interception, game-file modifications,
drivers, automatic elevation or anti-cheat guarantees. No live display, registry,
startup, firewall or LAN changes during ordinary tests.

IPC v1 uses bounded 4-byte little-endian length + UTF-8 JSON, max 64 KiB,
max depth 32, exact keys/enums and typed command discriminators. The eventual
server must use a random per-session pipe suffix, `CurrentUserOnly`, local-only
peer verification, OS-derived PID/token SID/session/start-time and a 256-bit
launch nonce. Nonce comparison uses constant-time comparison; authentication is
one-shot per connection. A hello never establishes OS identity by itself.
`UnavailablePipePeerVerifier` fails closed. No pipe listener is started in Stage 1.
Nonce distribution through an inherited protected launch channel, actual OS
verification, request dispatch authorization, response framing, connection
timeouts and UI/Agent liveness belong to Stage 2. Do not enable transport first.

The reducer requires current revision and ownership generation for ordinary
commands. Restore/shutdown/suspension/disable/release invalidate queued writes
even when their originating revision is old. Real writers must recheck the fence
immediately before native calls. A desired-state acknowledgment is distinct from
backend completion and observed state. Recovery must remain pending until actual
baseline restoration is acknowledged; requesting Restore cannot clear it.

Recovery journals contain user/session/process identity, owned-resource IDs,
exact captured baseline and last write, generation, preview deadline and status.
The typed topology payload is a version-tagged, bounded `ccd-v1-x64` snapshot;
its binary ABI interpretation/capture is Stage 4 and must not be guessed. Restore
must verify Agent termination, display identity and foreign-writer conflicts.
Never treat identity matrix/linear gamma as a substitute for real baselines.

Stores limit documents to 1 MiB, reject duplicate keys, NaN/infinity, unknown keys,
unknown schema versions and invalid ranges. Same-directory temporary files are
flushed to disk, then replaced atomically with a last-good backup. Writers use
in-process and exclusive file leases. A corrupt primary is preserved; the backup
may be read with a warning, and saving over corruption is refused. Migration is
in-memory and non-destructive. Atomic replacement cannot guarantee persistence
through every storage/power failure. An error after replacement requires reread
to distinguish an already committed write.

Preset payloads contain portable display intentions and SHA-256 asset IDs, not
native adapter IDs, absolute asset paths or URLs. File/image decode, import
confirmation, deep-link parsing and LAN pairing are later-stage gates, not
enabled by the schema alone. JSON Schema files and typed semantic validators
both apply; the embedded validator implements the keywords used by these files,
not arbitrary third-party JSON Schemas.
