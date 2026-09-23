# UT Launcher

Cross-platform (Windows and Linux) launcher/installer for **Unreal Tournament 99**, **Unreal Tournament 2004**, and **Unreal Tournament 4 (2017)**, with guaranteed identical versions for playing together.

## Download

Get the latest build from the [Releases page](https://github.com/FilloPrinci/UT_Collection_manager/releases/latest):

- **Windows**: download `utlauncher-windows-x64.zip`, extract it anywhere, and run `UTLauncher.exe`. No installer needed.
- **Linux**: download `utlauncher-linux-x64.tar.gz`, extract it, then run `./install.sh` from inside the extracted folder. It installs to `~/.local/share/UTLauncher` (no root needed) and adds a menu entry. Launch it from your application menu, or run `~/.local/bin/utlauncher`.

Every downloaded/imported game file is verified against `manifest/manifest.json`'s SHA-256 hashes before it's used — see [`docs/SPEC.md`](docs/SPEC.md) §3.

- Specification: [`docs/SPEC.md`](docs/SPEC.md)
- Manifest (versions, URLs, hashes): [`manifest/manifest.json`](manifest/manifest.json)
- Instructions for Claude Code: [`CLAUDE.md`](CLAUDE.md)

## Credits
- [OldUnreal](https://www.oldunreal.com) for the UT99 and UT2004 patches and installers.
- [UT4ever](https://ut4ever.org) and timiimit (UT4UU, master server) for UT4.
