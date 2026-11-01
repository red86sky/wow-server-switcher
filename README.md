# WoW Region Switcher · 魔兽世界双区服助手

> **v0.5.1-dev（未发布测试源码）** · Windows 10/11 x64 · C# WinForms · MIT License  
> 非官方开源工具，与 Blizzard Entertainment / Battle.net / 网易无关联。

[English](README.en.md) · [未发布变更](docs/UNRELEASED.md) · [安全说明](SECURITY.md)

这是一个 Windows 桌面工具，让使用者通过 **两个可配置的启动入口**，切换 Battle.net 记录的《魔兽世界》（`wow`）和《魔兽世界：无限》（`wowf`，若本机只有测试版则为 `wow_classic_beta`）的游戏根目录，然后通过选定的登录区域启动 Battle.net。

**重要现状：** 已完成离线的数据库结构保护及回归测试设计，但当前版本仍存在待核实问题：部分用户的 Battle.net / Agent 会在启动后**回写**安装路径。工具会在启动后复核并弹窗报告错误，**不保证任何 Battle.net 版本都能稳定切换成功**。请勿在更新、安装、修复或游戏运行期间使用。建议仅用于愿意测试并能恢复安装记录的用户。

## 功能

- 原生 WinForms 图形窗口：两个大按钮，直接点击进入所配置的入口。
- **两个游戏根目录自定义**：每个入口可使用不同磁盘、不同目录，支持浏览文件夹。
- **战网登录区域可分别自定义**：`CN` 中国大陆、`TW` 亚洲（台湾入口）、`KR` 亚洲（韩国入口）、`US` 美洲、`EU` 欧洲。
- **入口按钮名称可自定义**：例如“国服”“亚服”，或“美服”“欧服”。
- 用户配置保存在 `%LOCALAPPDATA%\WoWRegionSwitcherWinForms\settings.xml`，重启后自动读取。
- 同时处理普通魔兽世界和“无限”的安装记录，包括同一产品代码的多条记录。
- 在修改数据库前检查游戏、Battle.net、Agent 和下载任务；**自动尝试正常关闭战网并等待后台停止**。游戏运行时中止；若后台无法退出则弹出明细，不强制结束任何进程。
- 修改前备份原始 `product.db`；未知历史目录需要用户显式确认；支持恢复最近备份。
- 出错时显示可复制的完整 WinForms 报错框，并提供本机诊断信息和日志。
- 不移动、删除游戏文件；不读取账号、密码和登录令牌。

## 下载与运行

**本次只上传源码，不创建 Release。** 可在 GitHub Actions 的 CI 成功运行中下载构建产物，或依下文在本地构建 Windows EXE。

**“单 EXE”说明：** 提供的 EXE 是无控制台的 Windows 启动器，内嵌 WinForms C# 源码；首次运行时使用 Windows 自带的 **.NET Framework 4.x C# 编译器**，在用户本地缓存生成 WinForms 主程序并启动。用户只需要双击一个 EXE，**但并不是无需本地编译的原生单文件 WinForms 应用**。如果系统缺少 `csc.exe`，会明确弹出错误框而不会静默失败。

### 第一次使用

1. 先完成游戏下载、更新和修复，并正常退出魔兽世界；**不需要事先手动退出 Battle.net**（工具会尝试正常关闭战网）。
2. 双击 EXE。点击窗口底部的 **配置入口**。
3. 分别选择两个魔兽世界 **游戏根目录**（不是 `_retail_`、`_forever_` 等子目录），并为每个入口选定战网启动区域，点击“保存配置”。
4. 点击目标入口：检查更新与游戏 → 自动正常关闭 Battle.net → 等待 Agent 停止 → 确认所有进程退出 → 备份 → 修改并复核数据库 → 自动重启目标区域 Battle.net → 持续路径复核。
5. 登录 Battle.net 后，手动查看“游戏设置 → 安装位置”，确认两个游戏都显示目标目录。**不能只看工具按钮或登录区域判定成功。**

默认配置（可修改）：

| 入口 | 默认按钮名称 | 默认游戏根目录 | 战网参数 |
| --- | --- | --- | --- |
| 入口一 `CN` | 国服 | `D:\Games\Battle.netLibrary_cn\World of Warcraft` | `--setregion=CN` |
| 入口二 `ASIA` | 亚服 | `D:\Games\Battle.netLibrary\World of Warcraft` | `--setregion=TW` |

例如你也可以将入口一设为 `E:\WoW-China\World of Warcraft`、`CN`，入口二设为 `F:\WoW-International\World of Warcraft`、`EU`。两个目录不能相同，也必须是实际可识别的魔兽游戏根目录。

### “登录区域”与“游戏服务器区域”不是一回事

工具的区域下拉框设置 **启动 Battle.net 时的 `--setregion=` 参数**。它**不会**修改账号归属、购买的游戏许可、魔兽角色所在服务器，也不会更改 `product.db` 的 `play_region` 字段。你若已登录 Battle.net，仍可能需要先在客户端注销，并在登录界面手动选择对应登录区域。最终实际大区以 Battle.net / 游戏自身为准。

Battle.net 官方关于登录区域的说明：<https://us.support.blizzard.com/en/article/370838>（不同语言站点可能自动跳转）。

## 数据处理及安全机制

1. 程序只编辑 `%ProgramData%\Battle.net\Agent\product.db` 中匹配产品代码的 `settings.install_path` 字段。
2. 读入 Protobuf 字段时保留未知字段的原始字节，重写后验证非目标记录未改变；代码和重复记录的顺序不变。
3. 仅允许修改来源于用户两套已配置目录的记录。若发现第三种旧目录，显示完整路径并等待用户**明确授权本次修改**，否则退出。
4. 数据库更改之前检查运行中任务、Agent 和相关进程；修改前生成 `backups\product_*.db.bak`。
5. 用临时文件和文件替换完成原子写入，并验证输出与预期一致；失败尝试自动回滚。
6. Battle.net 启动后持续检查数据库；如果 Agent 覆盖路径，会弹出错误并留下日志。为了避免与 Agent 竞争，**不会在 Agent 运行时重复强行写入**。

> **注意：** `product.db` 属于 Battle.net 内部数据，其格式和行为可能更新。本工具不受 Blizzard 官方支持。即使有备份，也无法排除软件升级造成的兼容性问题。

## 备份与故障排查

本地文件目录：

```text
%LOCALAPPDATA%\WoWRegionSwitcherWinForms\
├── settings.xml              # 两个入口配置，不上传到仓库
├── launcher.log              # 本地诊断日志，不上传到仓库
├── backups\                  # 原始 product.db 备份，不上传到仓库
├── managed_v0_5_1\             # 首次运行自动生成的 WinForms 程序和编译日志
└── battlenet_exe.txt         # 战网可执行文件路径（首次选择后保存）
```

战网正常关闭失败时，不会强制 Kill：如果托盘仍在运行或 Agent 未退出，将弹窗显示进程名、PID 和路径，避免在其运行时修改数据库。

如无法切换：打开软件的 **复制诊断信息** 或错误框 **复制完整报错**，在 GitHub Issues 里描述复现步骤。提交前请检查并遮蔽个人用户名、目录、进程路径等敏感信息，**不要上传 `product.db`、日志原文件、账号或密码**。看到“更新任务进行中”时先完成更新，不要通过删除战网文件绕过保护。

当战网界面显示与工具状态不一致时，**优先相信战网界面**。可使用“恢复最近备份”把数据库恢复到上一次切换前状态（同样需要确保 Battle.net 和游戏已退出）。

## 设计与源码

```text
.
├── src/
│   ├── AppSettings.cs         # 两个入口的持久化设置 / 安全校验
│   ├── SettingsForm.cs        # WinForms 图形化设置对话框
│   ├── MainForm.cs            # 主窗口、切换状态机、报错、备份与回滚
│   ├── ProductDbEditor.cs     # Protobuf 安装路径编辑器（严格字段范围）
│   ├── ProcessClassifier.cs  # 战网与游戏进程识别
│   ├── OfflineSelfTests.cs   # 内存内回归测试（不接触真实战网）
│   └── Program.cs            # WinForms 入口、全局错误处理
├── main.go                   # 单 EXE 启动器，嵌入上述源码并本机编译
├── .github/workflows/        # Windows CI，**不会自动创建 Release**
├── docs/UNRELEASED.md
└── docs/UNRELEASED.md
```

### 本地构建（Windows）

运行环境：Windows 10/11 x64；Go 1.23.2+；.NET Framework 4.x 自带的 `csc.exe`。构建 EXE 的 Windows 主机和首次运行的 Windows 主机均需能访问相应编译器；不需要 Visual Studio。

```powershell
# 仓库根目录；构建带内嵌源码的 Windows GUI 启动器
$env:GOOS = 'windows'
$env:GOARCH = 'amd64'
$env:CGO_ENABLED = '0'
go build -trimpath -ldflags '-H=windowsgui -s -w' -o dist/WoWRegionSwitcher.exe .
```

可按 `.github/workflows/ci.yml` 内的命令使用 `csc.exe` 单独编译测试版 WinForms 主程序，并执行 `OfflineTests.exe --self-test`。该测试只在内存中使用模拟 Protobuf 数据，不访问真实 Battle.net 文件。

### Release 发布流程

推送 `main` / PR 会运行 Windows 构建及离线测试并提供 Actions 构建产物，但**不会创建 GitHub Release**，不自动打标签。经实机验证并得到明确授权后再单独安排发布。

本次按要求只上传代码，不创建版本标签或 Release；详情参见 [仓库交付说明](docs/PUBLISHING.md)。

## 开发状态 / 已知问题

- ⚠️ **Beta**：Battle.net/Agent 启动后可能回写路径，真实环境兼容性待验证。
- ⚠️ 不改写 `play_region`；因此客户端最终会选择哪个游戏记录，由 Battle.net 决定。
- ⚠️ 目前只提供两套独立入口；区域代码限于预定义白名单。
- ⚠️ 首次运行需要操作系统的 .NET Framework C# 编译器。
- ✅ 已增加重复 `wowf` 记录、任意目录切换、未知路径保护、异常进程防误判等离线测试。
- ✅ 失败显示报错而非假成功；登录区服和游戏区服未被混同。

欢迎提交 Issue / PR。请阅读 [CONTRIBUTING.md](CONTRIBUTING.md)。

## 许可证及声明

源码按 [MIT License](LICENSE) 分发。魔兽世界、World of Warcraft、Battle.net 等名称及商标归各权利人所有。本项目非官方，也不提供游戏文件或账号服务。
