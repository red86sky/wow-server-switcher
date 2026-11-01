# Repository delivery status / 仓库交付状态

This repository is a source-code snapshot for testing, **not a GitHub Release**.

- Push source to `main` only. **Do not push a version tag** until a Release is explicitly authorized.
- `.github/workflows/ci.yml` compiles and tests on Windows, and uploads **CI build artifacts** only.
- No `action-gh-release`, no token with contents:write, and no auto-publishing workflow.
- After validation on a clean Windows 10/11 machine, create a Release only on a separate explicit request.

当前仅上传代码，不创建 GitHub Release 或版本标签。Actions 仅上传 CI 构建产物。
