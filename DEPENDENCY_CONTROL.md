# Dependency Control Plan

This file records external runtime/build dependencies that Nong intends to control through forks, vendored source, first-party runtime bundles, lock files, and package mirrors.

## Forked Upstreams

| Package(s) | Current version | Upstream | Fork | Current locked upstream commit | License | Control plan |
| --- | --- | --- | --- | --- | --- | --- |
| Docnet.Core | 2.6.0 | https://github.com/GowenGit/docnet | https://github.com/angri450/docnet | 03191d3a3eb27efa8b542bd637489b388d1ba316 | MIT | Managed wrapper source and PDFium runtime assets are vendored into `Angri450.Nong.Pdf`; no `Docnet.Core` NuGet package is used by `PdfCore`. |
| PdfPig | fork HEAD 0.1.15 source | https://github.com/UglyToad/PdfPig | https://github.com/angri450/PdfPig | 9ada00796d133587bea258b978012ab06fb05096 | Apache-2.0 plus bundled PDFBox/FontBox/Adobe notices | Main parser/writer source is vendored under `PdfPig/` and compiled by `ThirdParty`; no `PdfPig` NuGet package is used by `PdfCore` or PDF tests. |
| Sdcb.PaddleOCR, Sdcb.PaddleOCR.Models.Local, Sdcb.PaddleOCR.Models.LocalV5, Sdcb.PaddleOCR.Models.Shared, Sdcb.PaddleInference | 3.3.1 / 3.0.0 / 2.7.0.1 | https://github.com/sdcb/PaddleSharp | https://github.com/angri450/PaddleSharp | 139bc184a6d86c0c60bb8b8a90fb641b21c0b0e6 / 6a6adefaf3a4230a93e02a08e3da315eb4cfb782 / 51d451aeafb9d786da1873931692c18ea2400f38 | Apache-2.0 | Keep forked. Do not rush a source merge; local OCR depends on native Paddle/OpenCV runtime, so prioritize first-party runtime bundles, package mirrors, and hash validation. |
| OpenCvSharp4 | 4.11.0.20250507 | https://github.com/shimat/opencvsharp | https://github.com/angri450/opencvsharp | not recorded in package metadata | Apache-2.0 | Keep forked. Treat as native-heavy dependency; prefer mirrored packages and Nong runtime bundles over source merging. |
| YamlDotNet | 18.0.0 / 16.3.0 | https://github.com/aaubry/YamlDotNet | https://github.com/angri450/YamlDotNet | 6fc4590001f201adc1cfed7a821adc104ee309ec / ae480660f4fb26f3eb0b41c1d1fcf21c0e9d9e73 | MIT | Forked for package availability. Consider unifying to one version before any source merge. |
| System.CommandLine | 2.0.0-beta4.22272.1 | https://github.com/dotnet/command-line-api | https://github.com/angri450/command-line-api | 209b724a3c843253d3071e8348c353b297b0b8b5 | MIT | Forked because CLI depends on a beta package. Prefer moving to a stable upstream or first-party package before broad distribution. |

## Already Vendored

| Upstream | Status |
| --- | --- |
| ZXing.Net | QR decode subset is vendored under `ZXing.Net/` and compiled by `ThirdParty/ThirdParty.csproj`; no `ZXing.Net` NuGet package is used. |
| Docnet.Core | Source is vendored under `Pdf/Docnet/`; PDFium native assets are vendored under `Pdf/runtimes/` and packed by `Pdf/PdfCore.csproj`. |
| PdfPig | Main library source and resources are vendored under `PdfPig/` and compiled by `ThirdParty/ThirdParty.csproj`; upstream project files, tests, examples, and tools are not included in the build path. `LICENSE` and `NOTICES.txt` are packed under `third-party/pdfpig/`. |

## Not Forked For Now

| Packages | Reason |
| --- | --- |
| System.* runtime packages | Microsoft platform packages; use lock files and package mirrors instead of forks. |
| xunit, Microsoft.NET.Test.Sdk, xunit.runner.visualstudio | Test-only dependencies; they do not ship in the user runtime path. |
| SkiaSharp/HarfBuzzSharp native assets | Native assets are locked and mirrored first. If full first-party control is needed, create Nong graphics runtime bundles instead of source-merging native binaries into `ThirdParty`. |

## Current Guardrails

- `Directory.Build.props` enables `RestorePackagesWithLockFile`.
- CI restore should run with locked mode through `ContinuousIntegrationBuild=true`.
- `packages.lock.json` files are committed for normal projects.
- Upstream package fallback for OCR native runtime is explicit only; default install uses Nong first-party runtime bundles.
