# Unreleased changes / 未发布变更 (v0.5.2-dev)

- Click either profile: check ongoing downloads/updates and running WoW, then automatically request normal Battle.net shutdown.
- Wait for the Battle.net client to exit; then wait for the updater Agent to stop; verify all blocking processes remain absent before patching.
- Preserve game-process safety (never auto-close the game) and never force-kill Battle.net or Agent.
- Auto-launch the selected Battle.net region after successful verified installation-path switch.
- Detailed errors with process name, PID and image path when graceful shutdown fails.
- Existing custom folders, login regions, backup/restore, database path verification and post-launch checks retained.

**Not published.** No GitHub Release or tag; real Battle.net writeback behavior still requires Windows end-to-end validation.

## v0.5.2-dev diagnostics / 修复

- Fix Asia -> CN false blocker: a persisted `agent: OP_UPDATE` product operation was treated as a running game update even after the Agent had exited.
- Keep blocking actual `wow` / `wowf` and other game operations, install handshakes, Agent processes and pending `product.db.new`.
- Only ignore the Agent *self-operation* record when the updater process has been confirmed absent and no pending DB replacement file exists.
- Include discarded historical Agent marker in diagnostic output, never silently erase it.
- Add in-memory regressions for Agent self-update vs. other game/handshake statuses.
- Unreleased development build; no GitHub tag or Release.
