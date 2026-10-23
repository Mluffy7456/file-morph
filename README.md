# FileMorph

FileMorph — локальный конвертер файлов для Windows. Windows 11 — основная платформа; поддержка Windows 10 рассматривается там, где она технически разумна.

FileMorph is a local-first file converter for Windows. Conversion must not require cloud services or internet access.

## Status

Early Alpha. The first end-to-end conversion slice is implemented for common raster images using Windows Imaging Component (WIC) through WPF: PNG, JPEG, BMP, TIFF and GIF. The UI supports file picking, drag-and-drop, output format selection, progress feedback, and saving a separate output next to the source. Animated inputs are currently reduced to their first frame. AVIF, HEIC, WebP, batch queue, history, presets and Explorer integration are not implemented yet.

## Structure

- src/FileMorph.App — WPF desktop UI and built-in WIC image handler
- src/FileMorph.Core — domain models and contracts
- src/FileMorph.Infrastructure — external-engine process execution and conversion orchestration
- tests/FileMorph.Core.Tests — core unit tests
- docs/ARCHITECTURE.md and docs/ARCHITECTURE.en.md — architecture docs

## Build

Install the .NET 10 SDK and a compatible Visual Studio installation with the .NET desktop development workload.

    dotnet restore FileMorph.sln
    dotnet build FileMorph.sln --configuration Release
    dotnet test FileMorph.sln --configuration Release
    dotnet run --project src/FileMorph.App/FileMorph.App.csproj

## Principles

- Never overwrite source files or silently replace existing output files.
- Keep processing local; no telemetry.
- Launch external engines without a shell and pass arguments safely.
- Keep the UI responsive and report failures explicitly.
- Declare handler capabilities rather than assuming every format pair is supported.
- Use temporary output and only publish the final file after successful encoding.
- Audit bundled engine licenses before distributing binaries.