# Contributing

Contributions that improve safety, repeatability, tests, and compatibility are welcome. The highest-priority unsolved issue is Battle.net/Agent rewriting `product.db` paths after launch.

Before opening a PR:

1. Keep the editor's scope narrow: do not rewrite `play_region`, account fields, product identifiers or arbitrary Protobuf fields.
2. Add an in-memory regression to `src/OfflineSelfTests.cs` for any bug fix; tests must **not** read or alter real Battle.net installations.
3. Run Windows CI tests; if changing startup behavior, test on a fresh Windows 10/11 VM.
4. Keep WinForms errors visible and copyable. Never introduce hidden/silent failure paths.
5. Do not add user data or personal screenshots, credentials, logs, backups or Blizzard game files.
6. Document which paths and regions are supported, and distinguish login region from WoW realm region.

Issues should state the version, Windows version, selected *sanitized* paths/region code, expected vs observed outcome, and any redacted error message. Use English or Chinese.

The project is under MIT License. This project is unofficial and is not affiliated with Blizzard Entertainment.
