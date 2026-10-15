# Release gates

- [x] Install .NET 10 SDK x64 (10.0.402); exact pin; generate production dependency locks.
- [x] Build/test .NET 10 WPF/x64 solution: 13 projects, 0 warnings/errors; 95 tests pass, including actual batch-launch regression.
- [x] Online NuGet dependency audit: no known vulnerable packages reported on 15 October 2026.
- [ ] Complete licence inventory, select project licence and produce release SBOM.
- Stage 2 native ABI/math, ownership, authenticated IPC, UI/Agent liveness and independent recovery.
- Stages 3–6 feature implementations and security/failure tests.
- Explicit opt-in hardware/OS/browser qualification in MANUAL-TESTS.md.
- Stage 7 signed installer, update trust, packaging, legal/licence selection and release audit.

This checkpoint is foundation code, not a production-ready display utility.
