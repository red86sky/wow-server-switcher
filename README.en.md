# WoW Region Switcher

**v0.5.3-beta.1** — Windows 10/11 x64 · C# WinForms · MIT License

[简体中文](README.md) · [Release notes](docs/RELEASE_NOTES_v0.5.3-beta.1.md) · [Security](SECURITY.md)

An **unofficial** Windows GUI tool for two configurable Battle.net launch profiles. Each profile stores a display name, World of Warcraft installation root, **Battle.net Downloads default install directory**, and launch-region argument. When selected, the app attempts to redirect Battle.net installation records for the retail WoW (`wow`) and WoW Forever (`wowf`, falling back to `wow_classic_beta` if that is all the machine has), then launches Battle.net.

> **Beta caution:** Battle.net/Agent may rewrite installation paths after launch. This failure has been observed and **is not yet conclusively fixed**. Use only if you can test and recover your installation records. Do not switch during downloads, updates, repairs, or while WoW is running.

## Features

- Two independently configurable WinForms launch buttons; each stores a WoW game root **and a separate Battle.net Downloads default install directory**. Paths are not hard-coded to `D:`.
- Supported Battle.net launch regions: `CN` (China), `TW` (Asia/Taiwan), `KR` (Asia/Korea), `US` (Americas), `EU` (Europe).
- Per-user configuration stored at `%LOCALAPPDATA%\WoWRegionSwitcherWinForms\settings.xml`.
- Narrow Protobuf `product.db` install path edit, preserving unrelated products and unknown fields.
- Checks active installation/update tasks and game processes, automatically requests normal Battle.net exit, waits for Agent exit, and aborts with copyable diagnostics if any process stays running. **Never force-kills a game, Battle.net, or Agent.**
- Automatic paired backup of product.db and Battle.net client config, explicit consent for unexpected old paths, atomic replace and restore-latest-backup UI.
- Post-launch verification, visible copyable error dialogs and local diagnostics.
- No game-file copying, account passwords, or credentials.

## Download / first launch

Download the [v0.5.3-beta.1 pre-release](https://github.com/red86sky/wow-server-switcher/releases/tag/v0.5.3-beta.1) or a CI artifact from Actions. Double-click it on Windows. No command prompt or VBS is needed.

**Single-EXE implementation detail:** This is a Go Windows-GUI bootstrap executable containing all WinForms C# sources. On first run it invokes the local .NET Framework 4.x `csc.exe` and caches the managed WinForms executable in `%LOCALAPPDATA%\WoWRegionSwitcherWinForms\managed_v0_5_3`. This is a single *entry point*; it is **not** an AOT or self-contained WinForms binary and still needs the Windows C# compiler available.

1. Finish installation/update tasks and exit WoW. Battle.net will be closed automatically using normal window-close requests; if it remains in the tray you will receive an explicit error.
2. Open the EXE and select **配置入口 / Configure profiles**.
3. For each profile, choose a **game installation root** (parent of `_retail_`, `_forever_` etc.), a **Downloads default installation directory**, a launch-region code, and an optional button name. Save.
4. Click a profile. The tool checks for updates and game processes, requests normal Battle.net shutdown, waits for the Agent to stop, verifies all blockers are gone, then backs up and patches **both `product.db` and Battle.net default-install configuration**, relaunches Battle.net, and verifies all three paths for around one minute.
5. Confirm both paths in **Battle.net's actual installation settings**. A green status only means the database check passed, not that Blizzard's UI must agree.

Defaults, both editable:

| Profile | WoW game root | Default install directory | Launch argument |
| --- | --- | --- | --- |
| 1 | `D:\Games\Battle.netLibrary_cn\World of Warcraft` | `D:\Games\Battle.netLibrary_cn` | `--setregion=CN` |
| 2 | `D:\Games\Battle.netLibrary\World of Warcraft` | `D:\Games\Battle.netLibrary` | `--setregion=TW` |

**Battle.net login region is not the World of Warcraft game/realm region.** This tool only passes `--setregion=...` at launch and deliberately does **not** rewrite `product.db`'s `play_region`. You may still need to sign out and select the login region in Battle.net's sign-in UI. It does not move accounts, entitlements, characters or purchases across regions. See [Blizzard support](https://us.support.blizzard.com/en/article/370838).

## Data safety and limitations

The tool inspects `%ProgramData%\Battle.net\Agent\product.db` and the detected `%APPDATA%\Battle.net\*.config`. Only selected Protobuf `settings.install_path` fields and the precise JSON string `Client.Install.DefaultInstallPath` may be rewritten. Unknown fields and other JSON settings are preserved exactly. It expects current records under either configured root; any other existing path triggers a user-visible confirmation before it is allowed as a source. It stores paired backups locally and attempts rollback on write verification failure. Older v1 profile XML is read and migrated without resetting existing profiles.

This is an unsupported internal Battle.net format and compatibility can break. The app does not modify live Agent databases or retry writes during game updates. If Blizzard's app changes a path after launch, the tool reports an error instead of claiming success.

Local configuration, cache, backups and logs reside under `%LOCALAPPDATA%\WoWRegionSwitcherWinForms\` and must **never** be committed or uploaded to Issues without review. Error messages may contain Windows usernames, directory paths and process information. **Do not share account information or `product.db`**.

## Build / tests / releases

See [Chinese build guide](README.md#本地构建windows), [`docs/PUBLISHING.md`](docs/PUBLISHING.md) and [GitHub Actions workflow](.github/workflows/ci.yml). Go 1.23.2+ cross-builds the Windows GUI bootstrap. On Windows, .NET Framework 4.x `csc.exe` compiles the WinForms sources. `OfflineTests.exe --self-test` runs only in-memory regressions.

Normal pushes and PRs run test-only CI. v0.5.3-beta.1 uses a one-time release workflow to create a **pre-release** with EXE/ZIP/SHA256 after Windows tests pass. MIT licensed; not affiliated with Blizzard Entertainment. World of Warcraft and Battle.net are trademarks of their respective holders.
