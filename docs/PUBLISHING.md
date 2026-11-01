# Release process / 发布流程

Repository: https://github.com/red86sky/wow-server-switcher

- Normal `main` pushes run `.github/workflows/ci.yml` and upload CI artifacts; they **do not create Releases**.
- `v0.5.3-beta.1` is created by a dedicated **one-time** GitHub Actions workflow after Windows compilation and offline tests pass.
- GitHub Actions creates the `v0.5.3-beta.1` tag, uploads the single Windows x64 GUI EXE, portable ZIP, and SHA256SUMS, and marks the Release as **pre-release**.
- The one-time workflow runs on its own workflow-file addition; later code commits do not automatically publish.
- Future releases require a deliberate new workflow dispatch or new reviewed release automation. Do not reuse a beta tag for different builds.
- Never commit local settings, passwords, login tokens, `product.db`, `Battle.net.config`, logs or backups.
- The app is not affiliated with Blizzard and Battle.net can change undocumented config formats or overwrite game paths after launch.
