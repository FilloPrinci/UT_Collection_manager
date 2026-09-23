# CLAUDE.md

Instructions for Claude Code on this repository.

## Project
Cross-platform (Windows + Linux) launcher/installer for UT99, UT2004, and UT4 (2017 pre-alpha).
**Always read `docs/SPEC.md` before working**: it is the full specification. `manifest/manifest.json` is the source of truth for versions, URLs, and hashes.

## Stack
- C# / .NET 10 (LTS), Avalonia UI (MVVM with CommunityToolkit.Mvvm), Serilog logging, xUnit tests.
- Linux distribution: self-contained tarball + `install.sh` script, same build for Debian/Ubuntu/Fedora/Arch (see SPEC.md §2). No separate .deb/.rpm/pacman packages.
- Projects: `UTLauncher.Core` (all logic, no UI dependency), `UTLauncher.App` (Avalonia), `UTLauncher.Cli`, `UTLauncher.Core.Tests`.

## Non-negotiable rules
1. **Verify the SHA-256** of every downloaded or imported file against the manifest. If it doesn't match: reject, log, explain. Never "install anyway".
2. **Feedback**: every non-instant operation has an activity indicator; if it can exceed 10 seconds, also a completion bar (when the total is known). Everything goes through Core's task system.
3. **Logging**: every step logs what it does. External processes: command, stdout, stderr, and exit code always in the log. Never swallow exceptions.
4. **No copied code** from `utshakka/ut4installer` (no explicit license): reimplement the logic described in the spec.
5. For UT99/UT2004, installation steps must be ported **faithfully** from the OldUnreal scripts at the commit referenced in the manifest (`OldUnreal/FullGameInstallers`, `Linux/src/` and `Windows/` folders). When a detail is not in the spec, the OldUnreal script is authoritative.
6. No CD keys, credentials, or personal data in the repo or the manifest.
7. Paths: no hardcoded `/` or `\` paths; use `Path.Combine` and platform APIs. Linux is case-sensitive.
8. Platform-dependent code sits behind interfaces (`IPlatform`, etc.) with Windows/Linux implementations.

## Conventions
- Code, identifiers, comments, documentation, commit messages, and UI text are all in English only. Do not add other-language resources unless explicitly requested.
- `async`/`await` with `CancellationToken` wherever there is I/O or external processes.
- `IProgress<T>` for progress reporting; never update the UI from Core.
- Unit tests for: manifest parsing, hash verification, Engine.ini merging, InstallInfo.bin read/write, path computation.

## Testing in a Linux VM
First-phase target: Linux VM (Fedora or Ubuntu), no GPU required.
```bash
dotnet build
dotnet test
dotnet run --project src/UTLauncher.Cli -- doctor
dotnet run --project src/UTLauncher.Cli -- install ut99 --dest ~/Games/UT99 --verbose
dotnet run --project src/UTLauncher.Cli -- install ut2004 --dest ~/Games/UT2004 --verbose
dotnet run --project src/UTLauncher.Cli -- verify ut99
dotnet run --project src/UTLauncher.App
```
Phase acceptance criteria: see `docs/SPEC.md` §8.

UT4 is not tested in the VM (10 GB + GPU): it is tested manually on real hardware.

## Working order
Follow the plan in `docs/SPEC.md` §8. One step at a time, with small, descriptive commits. Before moving to the next step: clean build and green tests.
