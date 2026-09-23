# UT Launcher — Specification

> Working name. Cross-platform (Windows and Linux) launcher/installer for **Unreal Tournament 99**, **Unreal Tournament 2004**, and **Unreal Tournament 4 (2017 pre-alpha)**, built for playing together with friends with **guaranteed identical versions** on both systems.

---

## 1. Goals

1. The user picks a game, clicks **Install**, and at the end clicks **Play**. No knowledge of Proton, Wine, patches, or dependencies required.
2. Same game version for everyone, on Windows and Linux: cross-play must work by construction.
3. Every downloaded or imported file is verified with **SHA-256** against the manifest.
4. Every issue is diagnosable: logs are always available, even for less experienced friends.

### Out of scope (for now)
- UT3 and other games.
- Advanced networking features (Host/Join, Tailscale, dedicated servers): **phase 2**.
- macOS.

---

## 2. Stack

| Area | Choice |
|---|---|
| Language | C# / .NET 10 (LTS) |
| UI | Avalonia (MVVM, CommunityToolkit.Mvvm) |
| Logging | Microsoft.Extensions.Logging + Serilog (rotated file + in-memory sink for the console) |
| Tests | xUnit |
| Windows distribution | self-contained `win-x64` executable (single file) |
| Linux distribution | self-contained `linux-x64` tarball + `install.sh` script (extracts to `~/.local/share/UTLauncher`, creates a `.desktop` entry). Same build for Debian, Ubuntu, Fedora, Arch: no dependency on system GTK/Qt (Avalonia uses Skia) or on the package manager, just a recent glibc. AppImage remains available as an optional secondary artifact, not as the primary method (avoids the "missing libfuse2" risk on recent distros). **No Flatpak** for now (the sandbox complicates umu). |

### Solution layout

```
src/
  UTLauncher.Core/      # logic: manifest, download, hashing, extraction, game modules, Proton. NO UI.
  UTLauncher.App/       # Avalonia UI
  UTLauncher.Cli/       # CLI for testing and headless VM use
tests/
  UTLauncher.Core.Tests/
manifest/
  manifest.json
docs/
  SPEC.md
tools/
  compute-hashes.sh     # computes size + SHA-256 of a URL/file to update the manifest
```

The Core / App / Cli separation is mandatory: all logic must be testable and usable from the command line.

---

## 3. Manifest

- JSON file (`manifest/manifest.json`), versioned in the repo.
- The launcher uses a copy bundled in the build and, if reachable, downloads the latest version from the GitHub repo (configurable URL). If the remote one is invalid, it falls back to the bundled copy.
- Contains: external tools (7-Zip, unshield, umu, Proton) with URLs and hashes, each game's sources with multiple URLs + size + SHA-256, patches pinned by tag, launch commands, special configurations.
- **Golden rule:** no file enters an installation unless its SHA-256 matches an entry in the manifest.

---

## 4. Cross-cutting rules

### 4.1 Tasks and feedback
Every operation is a `LauncherTask` with:
- status: `Pending`, `Running`, `Completed`, `Failed`, `Cancelled`;
- progress: **indeterminate** (activity indicator) or **determinate** (percentage);
- current step text (e.g. "Extracting Maps/DM-Deck16][.unr");
- for downloads: current/total bytes, speed, estimated time.

Rules:
- Any non-instant operation shows at least an **activity indicator**.
- Any operation that can take **more than 10 seconds** also shows a **completion bar** when the total is known.
- Long-running tasks are **cancellable**; cancellation cleans up partial files (except downloads, which remain resumable).

### 4.2 Logging
- **Console** panel always available (fixed button + `F12` shortcut): level, time, module, filter by level, search, **Copy** and **Export**.
- Rotated log files (last 5):
  - Windows: `%LOCALAPPDATA%\UTLauncher\logs\`
  - Linux: `${XDG_DATA_HOME:-~/.local/share}/UTLauncher/logs/`
- Stdout/stderr and **exit code** of every external process (7-Zip, unshield, umu, winetricks, vcredist, DXSETUP, the game itself) end up in the log.
- Verbose mode (setting or `--verbose`): full commands, URLs, environment variables passed to umu/Proton.
- Every error shown to the user has a **Show log** button that opens the console at the error.

### 4.3 Downloads
- Multiple URLs per source: the next one is tried if one fails.
- **Resume** via HTTP Range (`.part` file + metadata).
- SHA-256 computed in streaming during the download; after a resume it is recomputed over the full file.
- Some sources are HTTP-only (UT4ever): hash verification is therefore mandatory.

### 4.4 Alternative sources
For each main game source the user can:
1. **Download from a link**: field pre-filled with the manifest URL, editable (mirror, Drive, Nextcloud…).
2. **Open a local file** (zip/ISO).

In both cases the file must match a manifest hash. If it doesn't, the installation is **rejected** with an explanation (corrupted file or different version) and an offer to re-download or pick another file. No "install anyway" option.

### 4.5 Version code
Every installation shows a short **version code** (e.g. `UT04-3374p23`, from the manifest). Two players with the same code can play together.

### 4.6 Installation registry
JSON file in the launcher's data folder: for each installed game, path, version code, hashes of the sources used, date, platform, (Linux/UT4) prefix path.

### 4.7 Per-game operations
Install · Play · Open folder · Verify/Repair · Uninstall.

---

## 5. Linux: system check and Proton

### 5.1 System check (first launch + on demand)
Status indicators with explanation and a copyable command for Fedora / Ubuntu-Mint / Arch:
- disk space (per game);
- working Vulkan (`vulkaninfo --summary`) — needed only for UT4;
- Python 3 (for the portable umu build) — UT4 only;
- available user namespaces (Steam Runtime container) — UT4 only;
- optional system libraries for UT99/UT2004 (OpenAL, SDL3, libomp): if missing, the ones bundled with the patch are used.

### 5.2 Proton managed by the launcher (UT4 only)
- `umu-run`: if present on the system it is used, otherwise the portable version (manifest) is downloaded to `…/UTLauncher/tools/umu/`.
- Proton: version **pinned in the manifest**, downloaded and verified by the launcher into `…/UTLauncher/tools/proton/`, passed to umu via `PROTONPATH`.
- Dedicated prefix per game: `…/UTLauncher/prefixes/ut4/`.
- `vcrun2013` installed in the prefix via winetricks through umu.
- The user sees none of this: only tasks with understandable names ("Preparing compatibility environment").

---

## 6. Game modules

The steps below are a summary. **For UT99 and UT2004, the source of truth for details is the OldUnreal scripts** at the commit referenced in the manifest (`reference`): the steps must be ported to C# faithfully, including ignored files, case fixes, and libraries.

### 6.1 UT99 (GOTY) — native on both systems
1. Download/verify `UT_GOTY_CD1.iso` and `utbonuspack4-zip.7z`.
2. Download/verify the `v469e` patch for the platform (Windows `-Windows-x86.zip`, Linux `-Linux-amd64.tar.bz2`).
3. Extract the ISO (7-Zip) to the destination, **excluding** the patterns listed in the OldUnreal scripts (default inis, Windows binaries on Linux, old setup files, translations).
4. Extract Bonus Pack 4.
5. Extract the patch on top of the files.
6. Decompress the `.uz` maps (like `steps/unpack_uz_maps.sh`).
7. Apply the specific fixes (`steps/ut99_special_fixes.sh`) and remove the extra translations.
8. Windows: dependencies like the OldUnreal NSIS installer (VC++ Redistributable x86/x64, DirectX).
9. Create a menu entry / shortcut (optional, asked to the user).
10. Launch: Windows `System/UnrealTournament.exe`, Linux `System64/ut-bin`.

### 6.2 UT2004 — native on both systems
1. Download/verify `UT2004.iso` (two alternatives with different hashes, see manifest).
2. Download/verify the `3374-preview-23` patch.
3. Extract the ISO to a staging folder.
4. Extract the InstallShield `.cab` files with **unshield** (`steps/ut2004_unpack_cabs.sh`).
5. Copy the data folders to the destination per the table in `steps/ut2004_install_files.sh`.
6. Extract the patch.
7. Apply the fixes (`steps/ut2004_special_fixes.sh`): wrong-case files, system libraries preferred over the bundled ones, `MainMenuClass` → `GUI2K4.UT2K4MainMenuWS`.
8. Windows: dependencies like the OldUnreal NSIS installer.
9. Launch: Linux `System/ut2004-bin-amd64`, Windows to be verified after the patch.
10. **CD key:** not managed. If testing shows it's needed → optional field in the game's settings, saved locally only. Never keys in the manifest or the code.

### 6.3 UT4 (2017 pre-alpha, UT4ever build v1.1.0)
Logic derived from `utshakka/ut4installer`, **reimplemented** (the repo has no explicit license: do not copy code).

Accepted sources: the full `UT4 Installer - v1.1.0.zip` package (the inner zip is extracted) or the game zip directly. Both with manifest hashes.

Common steps:
1. Space check (≥ 19.5 GB for the extracted game, plus the zip if still present).
2. The destination must end in `UnrealTournament` and must not already exist: the zip already contains that folder and must be extracted into the **parent** folder.
3. Extraction with progress (System.IO.Compression, zip64).
4. **Engine.ini**: at `Documents/UnrealTournament/Saved/Config/WindowsNoEditor/Engine.ini`
   - if it doesn't exist: create it with the manifest's 7 sections (`Domain=<masterServer>`, `Protocol=https`);
   - if it exists: **add only the missing sections**, without touching the rest.
5. **UT4UU's InstallInfo.bin** (both manifest paths). .NET `BinaryWriter` format, in order: 6 × `bool` (createShortcut, isDryRun, createSymbolicLinks, upgradeEngineModules, refreshingExperience, tryToInstallInLocalGameServer), 3 × `string` (sourceLocation, installLocation, replacementSuffix), 2 × `byte` (platformTarget, buildConfiguration). Read the existing values, replace `sourceLocation` with `C:\Generic\Install\Path` and `installLocation` with the install path **as seen by Windows**, rewrite the rest unchanged.
6. **First access** panel: registration at ut4.timiimit.com (button that opens the page), code generation, paste it into the game on first launch. No credentials handled by the launcher.

Windows only:
- Dependencies: DirectX June 2010 (checks 4 files in System32) and VC++ 2013 x64 (checks registry keys). If missing: silent install, with elevation **only for that step**.
- Default folder under the user folder (not `Program Files`), to avoid UAC on game install.
- Launch: `Engine/Binaries/Win64/UE4-Win64-Shipping.exe` with `UnrealTournament -epicapp=UnrealTournamentDev -epicenv=Prod -EpicPortal`, working dir = the exe's folder.

Linux only:
- Game installed in the folder chosen by the user; the prefix gets a `drive_c/Games/UnrealTournament` symlink → the real folder.
- `installLocation` in InstallInfo.bin = `C:\Games\UnrealTournament` (always the same).
- Engine.ini written to `<prefix>/drive_c/users/steamuser/Documents/...`.
- Launch via `umu-run` with `WINEPREFIX`, `PROTONPATH`, `GAMEID`, same exe and same arguments as Windows, equivalent working dir.
- To be verified in testing: copy-paste of the login code under Wayland/XWayland, login persistence.

---

## 7. CLI (for testing and VM use)

```
utlauncher list
utlauncher install <ut99|ut2004|ut4> [--dest <path>] [--source-url <url>] [--source-file <path>] [--verbose]
utlauncher verify  <game>
utlauncher launch  <game>
utlauncher uninstall <game>
utlauncher doctor            # system check
utlauncher hash <file|url>   # size + SHA-256
```
Same tasks, same logging, textual progress.

---

## 8. Development plan

1. **Skeleton**: solution, projects, logging (file + console), manifest loader with validation, CLI `list` and `hash`.
2. **Infrastructure**: downloader (mirrors, resume, streaming SHA-256), task/progress, installation registry, external tool management (7-Zip, unshield) downloaded and verified.
3. **UT99** end-to-end on Linux (CLI) → VM test.
4. **UT2004** end-to-end on Linux (CLI) → VM test.
5. **Avalonia UI**: game list, installation with progress, console log, settings, system check.
6. **Windows**: UT99 and UT2004 with dependencies.
7. **UT4**: Windows, then Linux with umu/Proton.
8. Packaging: single-file exe for Windows; self-contained tarball + `install.sh` for Linux (AppImage optional, secondary).

### Acceptance criteria (Linux VM phase)
- The launcher starts (UI) and the CLI works.
- `install ut99` and `install ut2004` complete without errors, with hashes verified and clean logs.
- `verify` passes on both installations.
- Interrupting a download and relaunching it resumes the download.
- A file with a wrong hash is rejected.

---

## 9. Open questions
- UT2004 CD key (probably not needed).
- UT2004's Windows executable name after patch 3374.
- UT4 on Linux: UT4UU's behavior with `InstallInfo.bin` under Proton, login and copy-paste.
- Hash of the UT4 v1.0.3 file on the UT4ever server (possible mirror).
- Choice and hash of 7-Zip, unshield, umu, GE-Proton.
- Final name and license for the launcher; courtesy contact with UT4ever (_shakka) and OldUnreal.
