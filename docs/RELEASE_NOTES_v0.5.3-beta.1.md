# v0.5.3-beta.1 · Battle.net Downloads path support

**Pre-release — use only after backing up your existing Battle.net state.**

## Added

- One-click switching of the Battle.net **Settings → Downloads → Default install directory** along with WoW retail and WoW Forever game roots.
- Independent, user-editable default install folder for each profile; preserved under local `settings.xml`.
- Transparent v1→v2 settings migration: existing names, login regions and game roots retained; default install directory initially inferred from the game root's parent.
- Strict JSON scanner that changes only `Client.Install.DefaultInstallPath` (or `User.Client.Install.DefaultInstallPath` when unambiguous) in Battle.net client configuration; unrelated data remain unchanged.
- Paired backups of `product.db` and the Battle.net `.config`; matching backups restored together. Early failure attempts rollback of both files.
- Independent verification before launch and periodic checks after launch for all three paths; explicit, copyable errors.
- Offline JSON editor tests, safety checks for malformed/unexpected config and profile settings.

## Preserved

- Automatic graceful Battle.net shutdown and Agent exit check (no forced process termination).
- Safety blocks for real game/update/repair operations; dormant Agent self-update marker detection.
- Duplicate `wowf` records, fail-closed unknown install paths, configurable login regions, UI diagnostics.

## Known limitations

- Battle.net configuration is proprietary and may move/change between client releases. The tool will refuse to edit unrecognized or ambiguous files rather than guess.
- Some client/Agent versions may overwrite game install paths or default installation settings after launch. The tool detects this but cannot guarantee the UI accepts the edited values.
- This GUI entry EXE bootstraps a C# WinForms application with the local .NET Framework compiler on first run; it is not a native AOT single-file WinForms application.
- No real World of Warcraft game files, account credentials, agent database dumps or personal Windows settings are distributed.

**Installation:** Download `WoWRegionSwitcher.exe`, double-click on Windows 10/11 x64, configure each profile, and check both Battle.net Game Settings and Downloads Settings after switching.
