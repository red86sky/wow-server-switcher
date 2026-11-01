# Security & Privacy

This is a **beta**, unofficial tool that edits a local Battle.net-managed Protobuf database. Blizzard may change the schema or rewrite records after launch. Test only on installations for which you can restore a backup.

- Never submit original `product.db`, account details, session tokens, or credentials to issues or pull requests.
- Sanitize logs/screenshots: Windows profile names, file paths, process command lines, mounted volumes and network locations may identify you.
- Configuration (`settings.xml`), cached managed executables, backups and logs are stored under `%LOCALAPPDATA%\WoWRegionSwitcherWinForms\`.
- Game files are not moved or deleted; the tool does not download dependencies or contact external networks.
- It refuses to write during known game/Agent activity, creates a backup before changes, checks output, and offers restore.
- If unexpected third-party installation paths are found, users must explicitly authorize the source paths shown on-screen before any write.
- Report vulnerabilities privately to the repository maintainer through GitHub's private vulnerability reporting feature if enabled, rather than posting a public exploit.

**No warranties.** The Microsoft Windows binaries are only as trusted as the source/build pipeline and should be verified using the release SHA256 checksums.
