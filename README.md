# FileMorph

FileMorph — локальный конвертер файлов для Windows. Windows 11 — основная платформа; поддержка Windows 10 рассматривается там, где она технически разумна.

FileMorph is a local-first file converter for Windows. Conversion must not require cloud services or internet access.

## Status

Early Alpha foundation. This commit establishes the solution structure, core contracts, external-process boundary, initial WPF shell, unit tests, and Windows CI. Production format handlers and conversion workflows are not implemented yet.

## Structure

- src/FileMorph.App — WPF desktop UI
- src/FileMorph.Core — domain models and contracts
- src/FileMorph.Infrastructure — external-engine process execution and adapters
- tests/FileMorph.Core.Tests — core unit tests
- docs/ARCHITECTURE.md and docs/ARCHITECTURE.en.md — architecture docs

## Build

Install the .NET 10 SDK and a compatible Visual Studio installation with the .NET desktop development workload.

    dotnet restore FileMorph.sln
    dotnet build FileMorph.sln --configuration Release
    dotnet test FileMorph.sln --configuration Release
    dotnet run --project src/FileMorph.App/FileMorph.App.csproj

## Principles

- Never overwrite source files.
- Keep processing local; no telemetry.
- Launch engines without a shell and pass arguments safely.
- Keep the UI responsive and report failures explicitly.
- Declare handler capabilities rather than assuming every format pair is supported.
- Audit bundled engine licenses before distributing binaries.
