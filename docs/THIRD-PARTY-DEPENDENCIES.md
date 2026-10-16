# Dependency provenance at Stage 2

This is a development inventory, not a completed release SBOM or redistribution
review. Versions/hashes are in the committed per-project packages.lock.json files.
The following licence fields were read from the installed packages' own NuGet
manifests on 15 October 2026.

| Dependency | Version | Scope | Manifest licence |
| --- | --- | --- | --- |
| Microsoft.Windows.CsWin32 | 0.3.363 | Private native declaration generator | MIT |
| Microsoft.Windows.SDK.Win32Metadata | 71.0.30-preview | Generator metadata | Package sdk_license.txt (Windows SDK terms) |
| Microsoft.Windows.SDK.Win32Docs | 0.1.42-alpha | Generator documentation metadata | Manifest links Windows SDK licence URL |
| Microsoft.Windows.WDK.Win32Metadata | 0.13.25-experimental | Transitive generator metadata; no driver code requested | Package sdk_license.txt (SDK/WDK terms) |
| Microsoft.NET.Test.Sdk | 17.11.1 | Tests only | MIT |
| xunit | 2.9.2 | Tests only | Apache-2.0 |
| xunit.runner.visualstudio | 2.8.2 | Tests only | Apache-2.0 |

CsWin32 generates requested SDK declarations into ignored obj/Generated. The
generator/metadata packages are not runtime hardware DLLs and are PrivateAssets.
Do not label Windows SDK/WDK metadata as MIT merely because CsWin32 is MIT.
Production uses Microsoft .NET 10 Windows Desktop/ASP.NET Core shared frameworks;
self-contained public-release packaging/licensing remains a Stage 7 gate. The
user's private friend-test ZIP now bundles .NETCore, WindowsDesktop and ASP.NET
Core runtime packs 10.0.13, with their actual upstream licence/notice files copied
from the installed packages. No test or build-time generator DLL is shipped.

The NVIDIA adapter loads only the user's installed System32 nvapi64.dll. No
driver DLL is shipped/downloaded and no third-party wrapper code was copied.
Known private ABI identifiers/signatures are documented in the normative
specification Section 3.3. This is not a public NVIDIA support guarantee.

CVD coefficient provenance and approximation limits are separately recorded in
COLOR-PROVENANCE.md. Numeric attribution does not grant permission to redistribute
upstream source, papers, imagery or assets. Final project licence selection,
complete transitive licence/notice collection and release SBOM remain open.

docs/notices/CsWin32-LICENSE.txt is the unmodified MIT licence fetched from the
generator's pinned commit 6f436ab751:
https://raw.githubusercontent.com/microsoft/CsWin32/6f436ab751/LICENSE
SHA-256: b5cb7ff7859e7d282d98fc43ab081b0dda5dc659dc3385cf65740c759a7e6a6c.
The private ZIP also retains the package NOTICE and Win32 metadata sdk_license.txt.
