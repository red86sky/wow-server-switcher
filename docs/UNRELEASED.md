# Unreleased changes / 未发布变更 (v0.5.1-dev)

- Click either profile: check ongoing downloads/updates and running WoW, then automatically request normal Battle.net shutdown.
- Wait for the Battle.net client to exit; then wait for the updater Agent to stop; verify all blocking processes remain absent before patching.
- Preserve game-process safety (never auto-close the game) and never force-kill Battle.net or Agent.
- Auto-launch the selected Battle.net region after successful verified installation-path switch.
- Detailed errors with process name, PID and image path when graceful shutdown fails.
- Existing custom folders, login regions, backup/restore, database path verification and post-launch checks retained.

**Not published.** No GitHub Release or tag; real Battle.net writeback behavior still requires Windows end-to-end validation.
