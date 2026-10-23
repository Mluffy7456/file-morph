# FileMorph Architecture

## Goals

FileMorph is a local-first Windows app for file conversion and utility operations. The MVP aims to provide extensible format handlers, batch jobs, a queue, presets, local history, useful previews, and File Explorer integration.

## Solution boundaries

- App (C# / WPF): presentation, navigation, user commands, and Soft Tech visual design.
- Core: domain models, job states, handler capabilities, and contracts. No dependency on WPF or specific engines.
- Infrastructure: external CLI process execution, filesystem adapters, handler registration, and infrastructure services.
- Core.Tests: fast unit tests for domain rules.
- Windows shell integration should be a separate component after a technical prototype; keep COM code out of the UI layer.

Use a modular monolith, not microservices. Conversion engines run as separate processes to isolate crashes and support cancellation.

## Handler contract

Each handler declares its identifier, supported input/output extensions, and operations. The app checks capabilities before execution and explains unavailable actions. A file extension is a hint, not proof of file content.

## Job lifecycle

1. Validate the input file and selected operation.
2. Choose a destination path, next to the source by default.
3. Never overwrite the source or silently replace an existing output.
4. Create a unique temporary output in a controlled directory.
5. Launch the engine using ProcessStartInfo.ArgumentList, without shell interpretation.
6. Support cancellation, bounded output capture, and exit-code handling.
7. Verify that the output exists and passes basic validation.
8. Move the result to its destination, atomically where possible.
9. Clean up temporary files safely; never delete the source.
10. Record the result in local history once its schema and retention policy are defined.

## Security and privacy

No telemetry or network calls during normal processing. Any future update check must be separate and disableable. Never construct shell command strings; pass arguments separately. Bound process output and duration. Do not automatically execute unknown plugins. Never log user file contents. Handle malformed files, output collisions, cancellation, low disk space, and engine failures. Audit all bundled engine licenses before release.

## UI and visual system

WPF; dark theme by default with a light alternative. Soft Tech means balanced density, soft cards with thin borders, neutral modern typography, and a silver/white accent on graphite. The full app shows the mark and FileMorph wordmark; the app icon uses the mark only. Geometry break-apart/reassembly animation is decorative and must respect reduced-motion preferences.

## Compatibility and Alpha gates

Windows 11 is the primary target. Support Windows 10 where practical without compromising UX or safety. Validate Explorer integration separately on supported Windows versions.

Alpha gates: clean Windows CI build and tests; unit and integration tests; source-preservation, output-collision, cancellation, malformed-input, and temp-file tests; install/uninstall and Explorer integration checks; Portable test in a clean Windows VM; dependency license audit and bilingual documentation.
