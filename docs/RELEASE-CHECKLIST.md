# Release gates

- [x] Install and pin verified .NET 10 SDK x64 10.0.402; locked dependencies.
- [x] Build Stage 2 implementation: 14 Release/x64 projects, zero warnings/errors.
- [x] Run 140 .NET 10 VSTest tests, including actual launcher and fake-resource child-process recovery.
- [x] Online NuGet vulnerability audit including transitive packages: no known vulnerable packages reported on 15 October 2026.
- [x] Implement guarded native color adapters, authenticated local IPC, ownership, journal, UI/Agent liveness and independent recovery.
- [ ] Complete physical OS/GPU/driver/monitor qualification in MANUAL-TESTS.md.
- [ ] Qualify shared gamma outputs and hold write latency before exposing those routes.
- [ ] Complete Stages 3–6 feature implementations and security/failure tests.
- [ ] Complete licence inventory, select project licence, redistribute required notices and produce release SBOM.
- [ ] Stage 7 signed installer, update trust, publish companion layout, package and final security audit.

The hardware harness was NOT run. Passing fake/ABI tests never establishes
physical GPU, anti-cheat or capture support. This remains a development build.
