# Dependency inventory — Stage 1

Production uses Microsoft .NET runtime/Windows Desktop/WPF/Windows SDK and
Microsoft.Extensions.DependencyInjection from the ASP.NET Core shared framework.
No third-party native driver, GPU wrapper or reference-product asset is bundled.

Pinned test packages, selected from versions present in the local package cache:
Microsoft.NET.Test.Sdk 17.11.1, xunit 2.9.2, xunit.runner.visualstudio 2.8.2.
NuGet packages retain their respective upstream licence notices. No packages
are vendored or redistributed by this source checkpoint. A full transitive
licence inventory and release SBOM remain mandatory release gates. Actual .NET 10
dependency lock files were generated using SDK 10.0.402. The online NuGet audit
on 15 October 2026 reported no known vulnerable direct or transitive packages.

CommunityToolkit.Mvvm/CsWin32/QRCoder/FlaUI are not yet dependencies: Stage 1
uses a small observable base, no generated native bindings, no QR and no UI
automation package. Add reviewed pinned dependencies when their stage needs them.
