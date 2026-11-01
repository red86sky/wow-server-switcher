# v0.5.3-beta.2 · Prevent duplicate region switching during startup

**GitHub pre-release / Windows 10–11 x64**

## Fixed

- The launcher previously re-enabled the two region buttons immediately after launching Battle.net, even though the post-launch path verification remained active for ~60 seconds. A second request could then try to close the just-opened client and raise an error.
- Both region buttons and other state-changing actions remain disabled until verification completes or fails; internal / queued requests are independently rejected.
- Enforces a single WinForms controller per Windows login session, with an explicit dialog on duplicate launches. UAC elevation handoff waits briefly for the original process to exit.
- Adds explicit switch-request, skipped-request, process instance and post-launch start logs.
- Added in-memory regression tests for the state transition guard.

## Unchanged / caution

- Normal graceful Battle.net exit only; **no forced termination** of Battle.net, Agent or the game. Exit failures still show a detailed error.
- Maintains two custom game directories, two custom default download/install directories, login-region choices, persisted settings and backup/rollback protections.
- Battle.net may still rewrite its proprietary database / client settings after startup. Automatic tests cannot guarantee correct operation on every client version.
- Run only after game updates/repairs have completed. Never share account credentials, `product.db`, or unredacted private logs.
- This one-click GUI launcher bundles source and compiles C# WinForms locally on first run; it is not a native AOT WinForms executable.

**Download:** `WoWRegionSwitcher.exe` (Windows GUI bootstrapper) or `WoWRegionSwitcher_v0.5.3-beta.2_win-x64.zip`.
