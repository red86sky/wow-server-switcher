using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Diagnostics;
using System.Drawing;
using System.IO;
using System.Linq;
using System.Security.Principal;
using System.Runtime.InteropServices;
using System.Security.Cryptography;
using System.Text;
using System.Windows.Forms;

namespace WowRegionSwitcher
{
    internal sealed class MainForm : Form
    {
        private AppSettings settings;
        private string ChinaRoot { get { return settings.First.GameFolder; } }
        private string AsiaRoot { get { return settings.Second.GameFolder; } }

        private readonly string dbPath = Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.CommonApplicationData),
            @"Battle.net\Agent\product.db");
        private readonly string initialAutoRegion;
        private readonly Color background = Color.FromArgb(18, 22, 32);
        private readonly Color card = Color.FromArgb(30, 36, 49);
        private readonly Color muted = Color.FromArgb(161, 171, 189);
        private readonly Color white = Color.FromArgb(237, 241, 248);
        private readonly Color green = Color.FromArgb(86, 213, 166);
        private readonly Color orange = Color.FromArgb(247, 186, 106);
        private readonly Color red = Color.FromArgb(255, 125, 135);

        private Label lblCurrent;
        private Label lblRetail;
        private Label lblForever;
        private Label lblStatus;
        private Label lblCnFolder;
        private Label lblAsiaFolder;
        private Label lblCnRegion;
        private Label lblAsiaRegion;
        private Button btnConfig;
        private Button btnCn;
        private Button btnAsia;
        private Button btnRefresh;
        private Button btnDiagnostics;
        private Button btnRestore;
        private Button btnLogs;
        private Timer verificationTimer;
        private string verificationRegion = "";
        private string verificationTarget = "";
        private string verificationNormalCode = "";
        private string verificationSecondCode = "";
        private int verificationAttempts;
        private int verificationMismatches;
        private bool prelaunchWasVerified;
        private bool busy;

        public MainForm(string autoRegion)
        {
            initialAutoRegion = autoRegion;
            settings = AppSettings.Load();
            Text = "WoW Region Switcher v0.5.2-dev";
            Icon = SystemIcons.Application;
            StartPosition = FormStartPosition.CenterScreen;
            MinimumSize = new Size(730, 610);
            Size = new Size(830, 650);
            Font = new Font("Microsoft YaHei UI", 10F, FontStyle.Regular);
            BackColor = background;
            ForeColor = white;
            AutoScaleMode = AutoScaleMode.Font;
            FormBorderStyle = FormBorderStyle.Sizable;
            MaximizeBox = false;
            CreateLayout();
            UpdateProfileUi();
            verificationTimer = new Timer();
            verificationTimer.Interval = 2500;
            verificationTimer.Tick += VerifyAfterLaunch;
            Shown += OnShown;
        }

        private Font MakeFont(float size, bool bold)
        {
            return new Font("Microsoft YaHei UI", size, bold ? FontStyle.Bold : FontStyle.Regular);
        }
        private Label NewLabel(string text, float fontSize, Color color, bool bold)
        {
            Label lbl = new Label();
            lbl.Text = text;
            lbl.Font = MakeFont(fontSize, bold);
            lbl.ForeColor = color;
            lbl.AutoSize = false;
            lbl.BackColor = Color.Transparent;
            lbl.AutoEllipsis = true;
            return lbl;
        }
        private Button NewButton(string text, Color color)
        {
            Button btn = new Button();
            btn.Text = text;
            btn.Font = MakeFont(11F, true);
            btn.BackColor = color;
            btn.ForeColor = white;
            btn.FlatStyle = FlatStyle.Flat;
            btn.FlatAppearance.BorderSize = 0;
            btn.Cursor = Cursors.Hand;
            btn.UseVisualStyleBackColor = false;
            return btn;
        }
        private void CreateLayout()
        {
            // A single WinForms window; no VBS, no embedded browser, and no hidden errors.
            TableLayoutPanel root = new TableLayoutPanel();
            root.Dock = DockStyle.Fill;
            root.Padding = new Padding(28, 20, 28, 20);
            root.BackColor = background;
            root.ColumnCount = 1;
            root.RowCount = 6;
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 82));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 118));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 190));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 66));
            root.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.RowStyles.Add(new RowStyle(SizeType.Absolute, 25));
            Controls.Add(root);

            Panel heading = new Panel() { Dock = DockStyle.Fill };
            Label title = NewLabel("魔兽世界 · 双区服助手", 19F, white, true);
            title.Dock = DockStyle.Top;
            title.Height = 47;
            Label tagline = NewLabel("两套自定义安装目录和战网登录区域 · WinForms 单 EXE 入口（测试版）", 9F, muted, false);
            tagline.Dock = DockStyle.Top;
            tagline.Height = 26;
            heading.Controls.Add(tagline);
            heading.Controls.Add(title);
            root.Controls.Add(heading, 0, 0);

            Panel statusPanel = new Panel() { BackColor = card, Dock = DockStyle.Fill, Padding = new Padding(16, 9, 16, 8) };
            TableLayoutPanel statusGrid = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            statusGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 30));
            statusGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            statusGrid.RowStyles.Add(new RowStyle(SizeType.Absolute, 28));
            lblCurrent = NewLabel("正在检测游戏安装记录...", 10F, white, true);
            lblRetail = NewLabel("普通魔兽世界：读取中", 9F, muted, false);
            lblForever = NewLabel("魔兽世界：无限：读取中", 9F, muted, false);
            lblCurrent.Dock = lblRetail.Dock = lblForever.Dock = DockStyle.Fill;
            statusGrid.Controls.Add(lblCurrent, 0, 0);
            statusGrid.Controls.Add(lblRetail, 0, 1);
            statusGrid.Controls.Add(lblForever, 0, 2);
            statusPanel.Controls.Add(statusGrid);
            root.Controls.Add(statusPanel, 0, 1);

            TableLayoutPanel buttons = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 2, RowCount = 1, Padding = new Padding(0, 15, 0, 14) };
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            buttons.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 50));
            root.Controls.Add(buttons, 0, 2);

            Panel cnPanel = BuildRegionPanel(Color.FromArgb(24, 91, 164), out btnCn, out lblCnFolder, out lblCnRegion);
            Panel asiaPanel = BuildRegionPanel(Color.FromArgb(22, 117, 97), out btnAsia, out lblAsiaFolder, out lblAsiaRegion);
            cnPanel.Margin = new Padding(0, 0, 8, 0);
            asiaPanel.Margin = new Padding(8, 0, 0, 0);
            cnPanel.Dock = asiaPanel.Dock = DockStyle.Fill;
            buttons.Controls.Add(cnPanel, 0, 0);
            buttons.Controls.Add(asiaPanel, 1, 0);
            btnCn.Click += delegate { SwitchRegion("CN"); };
            btnAsia.Click += delegate { SwitchRegion("ASIA"); };

            Panel help = new Panel() { Dock = DockStyle.Fill, BackColor = card, Padding = new Padding(16, 8, 16, 5) };
            Label helpTitle = NewLabel("运行方式", 9.4F, white, true);
            helpTitle.Height = 22; helpTitle.Dock = DockStyle.Top;
            Label helpText = NewLabel("点击“配置入口”可自定义名称、游戏根目录和登录区域。切换前自动正常关闭战网、等待 Agent 退出；\r\n若游戏仍运行或后台无法退出会弹窗阻止切换。修改前自动备份，不移动文件。", 8.7F, muted, false);
            helpText.Height = 42; helpText.Dock = DockStyle.Fill;
            help.Controls.Add(helpText);
            help.Controls.Add(helpTitle);
            root.Controls.Add(help, 0, 3);

            TableLayoutPanel footer = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 5, RowCount = 2, Padding = new Padding(0, 16, 0, 2) };
            for (int i = 0; i < 5; i++) footer.ColumnStyles.Add(new ColumnStyle(SizeType.Percent, 20));
            footer.RowStyles.Add(new RowStyle(SizeType.Absolute, 38));
            footer.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            root.Controls.Add(footer, 0, 4);
            btnRefresh = NewButton("重新检测", Color.FromArgb(50, 58, 74));
            btnDiagnostics = NewButton("复制诊断信息", Color.FromArgb(50, 58, 74));
            btnRestore = NewButton("恢复最近备份", Color.FromArgb(50, 58, 74));
            btnLogs = NewButton("打开日志目录", Color.FromArgb(50, 58, 74));
            btnConfig = NewButton("配置入口", Color.FromArgb(50, 58, 74));
            Button[] small = {btnConfig, btnRefresh, btnDiagnostics, btnRestore, btnLogs};
            for (int i = 0; i < small.Length; i++)
            {
                small[i].Font = MakeFont(9F, false);
                small[i].Dock = DockStyle.Fill;
                small[i].Margin = new Padding(i == 0 ? 0 : 4, 0, i == 4 ? 0 : 4, 0);
                footer.Controls.Add(small[i], i, 0);
            }
            lblStatus = NewLabel("准备就绪。请选择国服或亚服。", 9F, muted, false);
            lblStatus.Dock = DockStyle.Fill;
            lblStatus.Padding = new Padding(0, 12, 0, 0);
            footer.Controls.Add(lblStatus, 0, 1);
            footer.SetColumnSpan(lblStatus, 5);

            btnConfig.Click += delegate { ConfigureProfiles(); };
            btnRefresh.Click += delegate { SafeRefresh(true); };
            btnDiagnostics.Click += delegate { CopyDiagnostics(); };
            btnRestore.Click += delegate { RestoreLatestBackup(); };
            btnLogs.Click += delegate { OpenLogs(); };

            Label footnote = NewLabel("独立非官方工具 · 首次测试请先退出游戏 · 不读取账号密码", 8F, muted, false);
            footnote.Dock = DockStyle.Fill;
            footnote.TextAlign = ContentAlignment.MiddleRight;
            root.Controls.Add(footnote, 0, 5);
        }

        private Panel BuildRegionPanel(Color buttonColor, out Button button, out Label folderLabel, out Label regionLabel)
        {
            Panel box = new Panel() { BackColor = card, Dock = DockStyle.Fill, Padding = new Padding(13) };
            TableLayoutPanel grid = new TableLayoutPanel() { Dock = DockStyle.Fill, ColumnCount = 1, RowCount = 3 };
            grid.RowStyles.Add(new RowStyle(SizeType.Percent, 68));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 26));
            grid.RowStyles.Add(new RowStyle(SizeType.Absolute, 29));
            button = NewButton("▶  启动", buttonColor);
            button.Dock = DockStyle.Fill;
            button.Font = MakeFont(17F, true);
            folderLabel = NewLabel("（未配置）", 8.6F, muted, false);
            folderLabel.TextAlign = ContentAlignment.MiddleCenter;
            folderLabel.Dock = DockStyle.Fill;
            regionLabel = NewLabel("--setregion=...", 8.3F, muted, false);
            regionLabel.TextAlign = ContentAlignment.MiddleCenter;
            regionLabel.Dock = DockStyle.Fill;
            grid.Controls.Add(button, 0, 0);
            grid.Controls.Add(folderLabel, 0, 1);
            grid.Controls.Add(regionLabel, 0, 2);
            box.Controls.Add(grid);
            return box;
        }

        private void UpdateProfileUi()
        {
            btnCn.Text = "▶  启动" + settings.First.Name;
            btnAsia.Text = "▶  启动" + settings.Second.Name;
            lblCnFolder.Text = settings.First.GameFolder;
            lblAsiaFolder.Text = settings.Second.GameFolder;
            lblCnRegion.Text = "战网启动区域：" + settings.First.LoginRegion;
            lblAsiaRegion.Text = "战网启动区域：" + settings.Second.LoginRegion;
            lblCnFolder.ForeColor = SeemsLikeGameFolder(ChinaRoot) ? green : orange;
            lblAsiaFolder.ForeColor = SeemsLikeGameFolder(AsiaRoot) ? green : orange;
        }

        private void ConfigureProfiles()
        {
            if (busy) return;
            if (verificationTimer != null && verificationTimer.Enabled)
            {
                MessageBox.Show(this, "战网启动后的安装路径复核仍在进行。请等复核完成后再修改入口配置。",
                    "正在复核路径", MessageBoxButtons.OK, MessageBoxIcon.Information);
                return;
            }
            using (SettingsForm dialog = new SettingsForm(settings))
            {
                if (dialog.ShowDialog(this) != DialogResult.OK) return;
                settings = dialog.SavedSettings;
            }
            UpdateProfileUi();
            SafeRefresh(true);
            SetStatus("配置已保存。启动入口会使用新目录和战网登录区域。", green);
        }

        private void OnShown(object sender, EventArgs e)
        {
            SafeRefresh(true);
            if (initialAutoRegion == "CN" || initialAutoRegion == "ASIA")
            {
                // Elevated restart continues the original one-click request automatically.
                BeginInvoke((MethodInvoker)delegate { SwitchRegion(initialAutoRegion); });
            }
        }

        private static bool SeemsLikeGameFolder(string root)
        {
            if (!Directory.Exists(root)) return false;
            string[] markers = { "Data", "_retail_", "_forever_", "_classic_beta_", ".build.info", ".product.db", "World of Warcraft Launcher.exe" };
            foreach (string marker in markers)
            {
                string path = Path.Combine(root, marker);
                if (File.Exists(path) || Directory.Exists(path)) return true;
            }
            return false;
        }
        private static ProductRecord[] FindProducts(IEnumerable<ProductRecord> items, string code)
        {
            return items.Where(p => string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)).ToArray();
        }
        private static bool HasProductAt(ProductRecord[] items, string code, string folder)
        {
            return ProductDbEditor.AllPathsAt(items, code, folder);
        }
        private string DescribeProduct(ProductRecord[] items, string code)
        {
            ProductRecord[] matching = FindProducts(items, code);
            if (matching.Length == 0) return "未找到安装记录";
            if (matching.Length == 1) return matching[0].InstallPath;
            string[] names = matching.Select(p => EqualFolder(p.InstallPath, ChinaRoot) ? settings.First.Name :
                EqualFolder(p.InstallPath, AsiaRoot) ? settings.Second.Name : p.InstallPath).Distinct().ToArray();
            return matching.Length + " 条记录：" + string.Join(" / ", names);
        }
        private static string IdentifySecondary(ProductRecord[] records)
        {
            // Prefer the current official product code. Duplicate registrations are supported.
            if (records.Any(p => string.Equals(p.Code, "wowf", StringComparison.OrdinalIgnoreCase))) return "wowf";
            if (records.Any(p => string.Equals(p.Code, "wow_classic_beta", StringComparison.OrdinalIgnoreCase))) return "wow_classic_beta";
            return "";
        }
        private ProductRecord[] ReadRecords()
        {
            if (!File.Exists(dbPath))
                throw new FileNotFoundException("没有找到战网 Agent 的产品安装数据库。请先通过战网正常安装或定位游戏。", dbPath);
            return ProductDbEditor.Inspect(File.ReadAllBytes(dbPath));
        }
        private void SafeRefresh(bool showDialog = false)
        {
            try { RefreshData(); }
            catch (Exception e)
            {
                lblCurrent.Text = "读取战网安装记录失败";
                lblCurrent.ForeColor = red;
                lblRetail.Text = "普通魔兽世界：未知";
                lblForever.Text = "魔兽世界：无限：未知";
                SetStatus("检测失败；点击“启动”会弹出具体报错。", red);
                Logger.Error("Refresh status", e);
                if (showDialog) ShowError("读取战网安装记录", e, "CHECK");
            }
        }
        private void RefreshData()
        {
            lblCnFolder.ForeColor = SeemsLikeGameFolder(ChinaRoot) ? green : orange;
            lblAsiaFolder.ForeColor = SeemsLikeGameFolder(AsiaRoot) ? green : orange;
            ProductRecord[] records = ReadRecords();
            string secondCode = IdentifySecondary(records);
            lblRetail.Text = "普通魔兽世界（wow）：" + DescribeProduct(records, "wow");
            lblForever.Text = "魔兽世界：无限（" + (secondCode == "" ? "未识别" : secondCode) + "）：" +
                (secondCode == "" ? "未找到安装记录" : DescribeProduct(records, secondCode));
            bool chinaReady = secondCode != "" &&
                HasProductAt(records, "wow", ChinaRoot) && HasProductAt(records, secondCode, ChinaRoot);
            bool asiaReady = secondCode != "" &&
                HasProductAt(records, "wow", AsiaRoot) && HasProductAt(records, secondCode, AsiaRoot);
            if (chinaReady && asiaReady)
            {
                lblCurrent.Text = "当前安装记录：两套目录均已登记（登录区域以战网为准）";
                lblCurrent.ForeColor = green;
            }
            else if (chinaReady)
            {
                lblCurrent.Text = "当前安装记录：" + settings.First.Name + "路径已匹配";
                lblCurrent.ForeColor = green;
            }
            else if (asiaReady)
            {
                lblCurrent.Text = "当前安装记录：" + settings.Second.Name + "路径已匹配";
                lblCurrent.ForeColor = green;
            }
            else
            {
                lblCurrent.Text = "当前安装记录：混合 / 目标路径未配置完整";
                lblCurrent.ForeColor = orange;
            }
            string[] blockers = EffectiveInstallBlockers(File.ReadAllBytes(dbPath), false);
            if (blockers.Length > 0)
            {
                lblCurrent.Text = "检测到仍有更新 / 安装任务：切换已被保护锁拦截";
                lblCurrent.ForeColor = orange;
                SetStatus("更新期间不能切换安装路径；请先让战网更新结束。", orange);
            }
        }
        private static bool EqualFolder(string left, string right)
        {
            return ProductDbEditor.SameFolder(left, right);
        }
        private void SetStatus(string message, Color color)
        {
            lblStatus.Text = message;
            lblStatus.ForeColor = color;
            lblStatus.Refresh();
        }
        private void SetBusy(bool value)
        {
            busy = value;
            btnCn.Enabled = btnAsia.Enabled = btnRefresh.Enabled = btnRestore.Enabled = btnConfig.Enabled = !value;
            Cursor = value ? Cursors.WaitCursor : Cursors.Default;
            Application.DoEvents();
        }
        // PROCESS_QUERY_LIMITED_INFORMATION works for ordinary/elevated agent
        // instances more reliably than Process.MainModule from non-admin callers.
        private const uint ProcessQueryLimitedInformation = 0x1000;
        [DllImport("kernel32.dll", SetLastError = true)]
        private static extern IntPtr OpenProcess(uint desiredAccess, bool inherit, int processId);
        [DllImport("kernel32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
        private static extern bool QueryFullProcessImageName(IntPtr process, int flags, StringBuilder buffer, ref int size);
        [DllImport("kernel32.dll", SetLastError = true)]
        [return: MarshalAs(UnmanagedType.Bool)]
        private static extern bool CloseHandle(IntPtr handle);

        private static string ProcessImagePath(Process process)
        {
            try { return process.MainModule.FileName; }
            catch { /* Try limited-access Win32 query before marking as unknown. */ }
            IntPtr handle = OpenProcess(ProcessQueryLimitedInformation, false, process.Id);
            if (handle == IntPtr.Zero) return "";
            try
            {
                StringBuilder value = new StringBuilder(32768);
                int length = value.Capacity;
                return QueryFullProcessImageName(handle, 0, value, ref length) ? value.ToString() : "";
            }
            finally { CloseHandle(handle); }
        }
        private static bool IsConfirmedAgent(Process p)
        {
            string file = ProcessImagePath(p).Replace('/', '\\');
            return file.IndexOf("\\Battle.net\\Agent\\", StringComparison.OrdinalIgnoreCase) >= 0 &&
                (file.EndsWith("\\Agent.exe", StringComparison.OrdinalIgnoreCase) ||
                 file.EndsWith("\\Blizzard Update Agent.exe", StringComparison.OrdinalIgnoreCase));
        }
        private bool IsGameProcess(Process p)
        {
            try
            {
                string name = p.ProcessName;
                // For known WoW executables, no path lookup is needed. Unknown
                // Wow-prefixed process names need verified image location.
                string image = ProcessClassifier.IsPossibleAdditionalGameName(name) ? ProcessImagePath(p) : "";
                return ProcessClassifier.IsGameProcess(name, image, ChinaRoot, AsiaRoot);
            }
            catch (InvalidOperationException) { return false; } // Process just exited.
        }
        private static bool IsBattleNetProcess(Process p)
        {
            try { return ProcessClassifier.IsBattleNetName(p.ProcessName); }
            catch (InvalidOperationException) { return false; }
        }
        private static bool IsAgentProcess(Process p)
        {
            try { return ProcessClassifier.IsAgentName(p.ProcessName); }
            catch (InvalidOperationException) { return false; }
        }
        private static string DescribeProcess(Process p)
        {
            string name = "<已退出>";
            int id = -1;
            string path = "";
            try { id = p.Id; name = p.ProcessName; path = ProcessImagePath(p); }
            catch (InvalidOperationException) { }
            catch (Win32Exception) { }
            return name + " (PID " + id + ")" +
                (path == "" ? " [路径不可读取或进程已退出]" : " [" + path + "]");
        }
        private Process[] BlockingProcesses()
        {
            Process[] all = Process.GetProcesses();
            List<Process> found = new List<Process>();
            int ownPid = Process.GetCurrentProcess().Id;
            try
            {
                foreach (Process process in all)
                {
                    bool keep = false;
                    try
                    {
                        string name = process.ProcessName;
                        // NEVER block the current WinForms process, its bootstrapper,
                        // or another copy of the switcher merely because its name starts "Wow".
                        if (process.Id == ownPid || ProcessClassifier.IsOwnApplicationName(name))
                            continue;
                        if (ProcessClassifier.IsAgentName(name))
                        {
                            string image = ProcessImagePath(process);
                            if (image == "")
                                throw new UnauthorizedAccessException("无法确认后台 Agent 的程序路径（PID " + process.Id +
                                    "）。为防止在战网 Agent 运行时修改数据库，已中止。请以管理员身份重试。");
                            keep = IsConfirmedAgent(process);
                        }
                        else
                        {
                            keep = IsBattleNetProcess(process) || IsGameProcess(process);
                        }
                        if (keep) found.Add(process);
                    }
                    finally { if (!keep) process.Dispose(); }
                }
                return found.ToArray();
            }
            catch
            {
                foreach (Process process in found) process.Dispose();
                throw;
            }
        }
        // Normal shutdown only: no Process.Kill and no automatic termination of WoW.
        // Battle.net may be configured to minimize to tray; in that case WM_CLOSE may
        // not exit it. We report a visible error rather than silently forcing exit.
        private const uint WmClose = 0x0010;
        private delegate bool EnumWindowCallback(IntPtr handle, IntPtr data);
        [DllImport("user32.dll")]
        private static extern bool EnumWindows(EnumWindowCallback callback, IntPtr data);
        [DllImport("user32.dll")]
        private static extern uint GetWindowThreadProcessId(IntPtr handle, out uint pid);
        [DllImport("user32.dll", CharSet = CharSet.Unicode)]
        private static extern int GetWindowText(IntPtr handle, StringBuilder title, int capacity);
        [DllImport("user32.dll", SetLastError = true)]
        private static extern bool PostMessage(IntPtr handle, uint message, IntPtr wParam, IntPtr lParam);

        private static bool RequestNormalExit(Process process)
        {
            bool signaled = false;
            try { signaled = process.CloseMainWindow(); }
            catch (InvalidOperationException) { return true; } // already exited
            catch (Win32Exception ex) { Logger.Error("CloseMainWindow PID=" + process.Id, ex); }
            // A Battle.net instance minimized to tray can have a hidden titled
            // top-level window. WM_CLOSE is a normal window message, not a kill.
            // Never address other processes' windows or untitled helper windows.
            if (IsBattleNetProcess(process))
            {
                int targetId;
                try { targetId = process.Id; }
                catch (InvalidOperationException) { return true; }
                EnumWindowCallback visit = delegate(IntPtr handle, IntPtr data)
                {
                    uint owner;
                    GetWindowThreadProcessId(handle, out owner);
                    if (owner != (uint)targetId) return true;
                    StringBuilder text = new StringBuilder(256);
                    GetWindowText(handle, text, text.Capacity);
                    string title = text.ToString();
                    if (title.IndexOf("Battle.net", StringComparison.OrdinalIgnoreCase) < 0 &&
                        title.IndexOf("Blizzard", StringComparison.OrdinalIgnoreCase) < 0 &&
                        title.IndexOf("战网", StringComparison.OrdinalIgnoreCase) < 0)
                        return true;
                    if (PostMessage(handle, WmClose, IntPtr.Zero, IntPtr.Zero)) signaled = true;
                    return true;
                };
                EnumWindows(visit, IntPtr.Zero);
                GC.KeepAlive(visit);
            }
            return signaled;
        }

        private void WaitForProcessesToExit(Func<Process, bool> isBlocked, int timeoutMs,
            string progressMessage, string errorMessage)
        {
            DateTime until = DateTime.UtcNow.AddMilliseconds(timeoutMs);
            while (true)
            {
                Process[] active = BlockingProcesses();
                try
                {
                    Process[] blocking = active.Where(isBlocked).ToArray();
                    if (blocking.Length == 0) return;
                    if (DateTime.UtcNow >= until)
                        throw new InvalidOperationException(errorMessage + "\r\n\r\n仍在运行：\r\n" +
                            string.Join("\r\n", blocking.Select(DescribeProcess).ToArray()) +
                            "\r\n本次未开始修改安装数据库；程序没有强制结束这些进程。" );
                }
                finally { foreach (Process process in active) process.Dispose(); }
                SetStatus(progressMessage, orange);
                System.Threading.Thread.Sleep(250);
                Application.DoEvents();
            }
        }

        private void EnsureClosed()
        {
            Process[] initial = BlockingProcesses();
            try
            {
                Process[] games = initial.Where(IsGameProcess).ToArray();
                if (games.Length > 0)
                    throw new InvalidOperationException("魔兽世界游戏正在运行，请先在游戏内正常退出：\r\n" +
                        string.Join("\r\n", games.Select(DescribeProcess).ToArray()) +
                        "\r\n本工具不会自动关闭或强行结束游戏。" );
                if (initial.Length == 0)
                {
                    Logger.Info("AUTO_CLOSE", "Battle.net, Agent, and WoW are already stopped");
                }
                else
                {
                    Logger.Info("AUTO_CLOSE_DETECTED", string.Join(" | ", initial.Select(DescribeProcess).ToArray()));
                    SetStatus("检测到战网客户端，正在自动正常退出...", orange);
                    foreach (Process process in initial.Where(IsBattleNetProcess))
                    {
                        string description = DescribeProcess(process);
                        bool sent = RequestNormalExit(process);
                        Logger.Info("AUTO_CLOSE_REQUEST", description + " window_close_sent=" + sent);
                    }
                }
            }
            finally { foreach (Process process in initial) process.Dispose(); }

            // Give Battle.net sufficient time to end all of its processes.
            WaitForProcessesToExit(IsBattleNetProcess, 15000,
                "正在等待战网客户端正常退出（包含托盘进程）...",
                "战网客户端没有正常退出。若已最小化到系统托盘，请右键战网图标并选择“退出”，然后重试。" );

            // After Battle.net exits, the updater Agent often shuts down on its
            // own. Request a normal window close when available; NEVER Kill it.
            Process[] afterClient = BlockingProcesses();
            try
            {
                foreach (Process process in afterClient.Where(IsAgentProcess))
                {
                    string description = DescribeProcess(process);
                    bool sent = RequestNormalExit(process);
                    Logger.Info("AGENT_GRACEFUL_CLOSE", description + " window_close_sent=" + sent);
                }
            }
            finally { foreach (Process process in afterClient) process.Dispose(); }
            WaitForProcessesToExit(IsAgentProcess, 10000,
                "战网已退出，正在等待后台更新 Agent 正常停止...",
                "战网后台 Agent 仍在运行，无法保证安装数据库安全。请确认没有下载、安装或更新任务，退出战网托盘后重试；本工具不会强制结束 Agent。" );

            // Wait for a stable absence of ALL blocking processes (including WoW)
            // before touching product.db. Agent can briefly respawn.
            SetStatus("正在确认战网和游戏进程均已完全退出...", orange);
            for (int attempt = 0; attempt < 5; attempt++)
            {
                System.Threading.Thread.Sleep(350);
                Process[] current = BlockingProcesses();
                try
                {
                    if (current.Length != 0)
                        throw new InvalidOperationException("关闭后又检测到战网、Agent 或游戏进程，切换已停止：\r\n" +
                            string.Join("\r\n", current.Select(DescribeProcess).ToArray()));
                }
                finally { foreach (Process process in current) process.Dispose(); }
                Application.DoEvents();
            }
            Logger.Info("AUTO_CLOSE_VERIFIED", "Battle.net, Agent, WoW absent for stability window; patch may proceed");
        }

        private string FindLauncher()
        {
            List<string> choices = new List<string>();
            try
            {
                if (File.Exists(Logger.SavedLauncherFile))
                    choices.Add(File.ReadAllText(Logger.SavedLauncherFile, Encoding.UTF8).Trim().Trim((char)0xFEFF));
            }
            catch (Exception e) { Logger.Error("Read saved launcher", e); }
            string[] bases = {
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFilesX86),
                Environment.GetFolderPath(Environment.SpecialFolder.ProgramFiles),
                Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData)
            };
            foreach (string path in bases)
            {
                if (string.IsNullOrEmpty(path)) continue;
                foreach (string child in new string[] { "Battle.net", @"Battle.net\Battle.net" })
                {
                    foreach (string name in new string[] { "Battle.net Launcher.exe", "Battle.net.exe" })
                        choices.Add(Path.Combine(Path.Combine(path, child), name));
                }
            }
            foreach (string choice in choices)
            {
                if (!string.IsNullOrEmpty(choice) && File.Exists(choice) && AllowedLauncherName(choice)) return choice;
            }
            using (OpenFileDialog picker = new OpenFileDialog())
            {
                picker.Title = "首次使用：请选择战网启动程序（只需选择一次）";
                picker.Filter = "Battle.net 程序|Battle.net Launcher.exe;Battle.net.exe|所有程序|*.exe";
                picker.CheckFileExists = true;
                if (picker.ShowDialog(this) != DialogResult.OK)
                    throw new OperationCanceledException("没有选择战网启动程序。本次切换已取消，游戏安装记录未修改。");
                if (!AllowedLauncherName(picker.FileName))
                    throw new InvalidDataException("只允许选择 Battle.net Launcher.exe 或 Battle.net.exe，不能选择其他程序。\r\n选择位置：" + picker.FileName);
                Directory.CreateDirectory(Logger.DirectoryPath);
                File.WriteAllText(Logger.SavedLauncherFile, picker.FileName, Encoding.UTF8);
                return picker.FileName;
            }
        }
        private static bool AllowedLauncherName(string fullPath)
        {
            string name = Path.GetFileName(fullPath);
            return string.Equals(name, "Battle.net.exe", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Battle.net Launcher.exe", StringComparison.OrdinalIgnoreCase);
        }

        private static string DbFingerprint(byte[] bytes)
        {
            using (SHA256 hash = SHA256.Create())
            {
                byte[] digest = hash.ComputeHash(bytes);
                return BitConverter.ToString(digest, 0, 8).Replace("-", "");
            }
        }

        private bool IsAgentConfirmedStopped()
        {
            // An agent cannot be considered dormant merely because product.db
            // looks unchanged. Verify both the live process list and pending DB
            // replacement file. Access/permission failure must fail closed.
            if (File.Exists(Path.Combine(Path.GetDirectoryName(dbPath), "product.db.new")))
                return false;
            Process[] processes = BlockingProcesses();
            try { return !processes.Any(IsAgentProcess); }
            finally { foreach (Process process in processes) process.Dispose(); }
        }

        private string[] EffectiveInstallBlockers(byte[] bytes, bool logDormantStatus)
        {
            bool agentStopped = IsAgentConfirmedStopped();
            string[] blockers = ProductDbEditor.FindSwitchBlockers(bytes, agentStopped);
            if (logDormantStatus && agentStopped)
            {
                string[] raw = ProductDbEditor.FindSwitchBlockers(bytes);
                string[] dormant = raw.Except(blockers).ToArray();
                if (dormant.Length != 0)
                    Logger.Info("DORMANT_AGENT_OPERATION", "Ignoring persisted agent-only metadata after process/new-file check: " +
                        string.Join("; ", dormant));
            }
            return blockers;
        }

        private void AssertNoActiveInstall(string secondaryCode)
        {
            byte[] bytes = File.ReadAllBytes(dbPath);
            // Block live game tasks, other Blizzard game updates and active
            // install handshakes. Only ignore *agent's own* operation marker
            // when there is no Agent process and no pending database file.
            string[] blockers = EffectiveInstallBlockers(bytes, true);
            if (blockers.Length > 0)
                throw new InvalidOperationException(
                    "检测到仍有下载、更新、修复或安装任务：\r\n" +
                    string.Join("\r\n", blockers) +
                    "\r\n\r\n本次没有修改安装数据库，也不会强制结束下载。" +
                    "请先在战网完成更新，完全退出战网后再次点击目标区服。" +
                    "如果战网已经显示全部完成，请复制诊断信息给我检查是否有残留任务记录。");
            string pending = Path.Combine(Path.GetDirectoryName(dbPath), "product.db.new");
            if (File.Exists(pending) && (DateTime.UtcNow - File.GetLastWriteTimeUtc(pending)).TotalSeconds < 90)
                throw new InvalidOperationException("Agent 最近正在生成 product.db.new（数据库更新文件）。" +
                    "请等待战网更新结束并完全退出后重试；本次未修改任何文件。");
        }

        private string NewBackup(string region)
        {
            Directory.CreateDirectory(Logger.BackupsDirectory);
            string initial = Path.Combine(Logger.BackupsDirectory, "first_original_product.db.bak");
            if (!File.Exists(initial)) File.Copy(dbPath, initial, false);
            string destination = Path.Combine(Logger.BackupsDirectory,
                (region == "before_restore" ? "safety_" : "product_") + region + "_" + DateTime.Now.ToString("yyyyMMdd_HHmmss_fff") + "_" + Process.GetCurrentProcess().Id + ".db.bak");
            File.Copy(dbPath, destination, false);
            return destination;
        }
        private void ReplaceAtomically(byte[] newBytes, string backupFile, byte[] expectedOld)
        {
            string tmp = Path.Combine(Path.GetDirectoryName(dbPath),
                "product.db.region-switch." + Process.GetCurrentProcess().Id + ".tmp");
            bool replaced = false;
            try
            {
                if (!File.ReadAllBytes(dbPath).SequenceEqual(expectedOld))
                    throw new IOException("战网数据库在本次检查期间发生了变化。为避免覆盖战网新写入的内容，已经取消本次修改。");
                File.WriteAllBytes(tmp, newBytes);
                ProductDbEditor.Inspect(File.ReadAllBytes(tmp));
                File.Replace(tmp, dbPath, null);
                replaced = true;
                if (!File.ReadAllBytes(dbPath).SequenceEqual(newBytes))
                    throw new InvalidDataException("数据库写入后内容核对失败。程序将尝试自动回滚。");
            }
            catch
            {
                if (replaced)
                {
                    try { File.Copy(backupFile, dbPath, true); }
                    catch (Exception rollbackError)
                    {
                        Logger.Error("CRITICAL: rollback failed; original backup at " + backupFile, rollbackError);
                        MessageBox.Show(this,
                            "严重警告：修改后的验证失败，并且自动恢复也失败！\r\n" +
                            "原始备份仍位于：" + backupFile + "\r\n" +
                            "请不要运行战网，先发送此截图及日志反馈。\r\n" + rollbackError.Message,
                            "需要手动恢复", MessageBoxButtons.OK, MessageBoxIcon.Error);
                    }
                }
                throw;
            }
            finally
            {
                try { if (File.Exists(tmp)) File.Delete(tmp); } catch { }
            }
        }
        private void SwitchRegion(string region)
        {
            if (busy) return;
            LaunchProfile profile = settings.ForId(region);
            string target = profile.GameFolder;
            string label = profile.Name;
            string stage = "准备";
            bool pathsReady = false;
            prelaunchWasVerified = false;
            SetBusy(true);
            try
            {
                verificationTimer.Stop();
                verificationRegion = "";
                stage = "检查目标游戏目录";
                SetStatus("正在检查" + label + "安装目录...", orange);
                if (!Directory.Exists(target))
                    throw new DirectoryNotFoundException("未找到" + label + "游戏目录：" + target + "\r\n请确认已在配置中选择真实的游戏根目录。本程序不会创建空游戏文件夹。");
                if (!SeemsLikeGameFolder(target))
                    throw new InvalidDataException("目标文件夹存在，但缺少游戏标记或 Data 等目录，无法证明这是魔兽世界安装目录：" + target);

                stage = "查找战网启动程序";
                SetStatus("正在识别战网程序...", orange);
                string launcher = FindLauncher();
                // Before asking to close Battle.net, check whether an active
                // installer operation exists. Do not kill Agent mid-update.
                stage = "检查正在进行的游戏下载 / 安装";
                byte[] early = File.ReadAllBytes(dbPath);
                ProductRecord[] earlyRecords = ProductDbEditor.Inspect(early);
                string earlySecond = IdentifySecondary(earlyRecords);
                AssertNoActiveInstall(earlySecond == "" ? "wowf" : earlySecond);
                stage = "自动正常关闭战网并确认进程退出";
                SetStatus("正在检测并自动退出战网...", orange);
                EnsureClosed();
                stage = "再次检查更新任务是否停止";
                AssertNoActiveInstall(earlySecond == "" ? "wowf" : earlySecond);
                stage = "读取游戏安装记录";
                byte[] before = File.ReadAllBytes(dbPath);
                ProductRecord[] products = ProductDbEditor.Inspect(before);
                if (FindProducts(products, "wow").Length == 0)
                    throw new InvalidDataException("没有找到普通魔兽世界（wow）安装记录，未修改任何游戏路径。\r\n请先在战网中通过“使用另一个文件夹”定位普通魔兽世界。");
                string secondCode = IdentifySecondary(products);
                if (secondCode == "")
                    throw new InvalidDataException("未找到“魔兽世界：无限”安装记录（wowf），也没有可用的测试版（wow_classic_beta）记录。\r\n已识别的 WoW 产品：" +
                        string.Join(", ", products.Select(p => p.Code).Where(c => c.StartsWith("wow", StringComparison.OrdinalIgnoreCase)).Distinct().ToArray()) +
                        "\r\n为了避免修改错误游戏，程序未作任何修改。请先用战网手动定位一次“无限”所在目录，然后重试。");

                stage = "检查允许修改的历史游戏目录";
                string[] trustedRoots = { ChinaRoot, AsiaRoot };
                string[] oldRoots = ProductDbEditor.FindUntrustedPaths(products, "wow", secondCode, trustedRoots);
                if (oldRoots.Length != 0)
                {
                    string oldText = string.Join("\r\n", oldRoots);
                    DialogResult consent = MessageBox.Show(this,
                        "战网的现有安装记录含有配置之外的路径：\r\n\r\n" + oldText +
                        "\r\n\r\n如果这是你之前安装游戏的目录，可以授权本次切换将这些记录指向：\r\n" + target +
                        "\r\n\r\n本工具不会移动文件；错误确认可能导致战网找不到原安装。是否明确允许本次修改？",
                        "确认历史目录", MessageBoxButtons.YesNo, MessageBoxIcon.Warning,
                        MessageBoxDefaultButton.Button2);
                    if (consent != DialogResult.Yes)
                        throw new OperationCanceledException("未授权修改配置之外的历史路径。没有修改数据库。");
                    trustedRoots = trustedRoots.Concat(oldRoots).ToArray();
                }
                stage = "准备并校验修改内容";
                byte[] updated = ProductDbEditor.PatchBoth(before, target, "wow", secondCode, trustedRoots);
                if (!updated.SequenceEqual(before))
                {
                    stage = "备份安装数据库";
                    string backup = NewBackup(region);
                    stage = "原子写入并复核安装数据库";
                    SetStatus("已备份，正在统一两款游戏的全部安装记录...", orange);
                    ReplaceAtomically(updated, backup, before);
                    Logger.Info("Switch", label + " target=" + target + " product=wow," + secondCode + " backup=" + backup);
                }
                else Logger.Info("Switch", "Already selected " + label + " target=" + target);

                stage = "启动前独立核查 / 记录校验指纹";
                byte[] ready = File.ReadAllBytes(dbPath);
                ProductRecord[] readyProducts = ProductDbEditor.Inspect(ready);
                if (!HasProductAt(readyProducts, "wow", target) || !HasProductAt(readyProducts, secondCode, target))
                    throw new InvalidDataException("战网尚未启动，但安装记录已与目标区服不一致。" +
                        "这说明存在其他进程写入数据库；已阻止启动战网。\r\n" +
                        "wow：" + DescribeProduct(readyProducts, "wow") + "\r\n" +
                        secondCode + "：" + DescribeProduct(readyProducts, secondCode));
                prelaunchWasVerified = true;
                Logger.Info("PRELAUNCH_VERIFIED", "region=" + region + " target=" + target +
                    " digest=" + DbFingerprint(ready) + " bytes=" + ready.Length +
                    " write_time_utc=" + File.GetLastWriteTimeUtc(dbPath).ToString("o"));
                pathsReady = true;
                stage = "启动战网";
                SetStatus("两项游戏的全部安装记录已写入目标路径，正在打开" + label + "战网...", orange);
                ProcessStartInfo start = new ProcessStartInfo(launcher);
                start.UseShellExecute = true;
                start.WorkingDirectory = Path.GetDirectoryName(launcher);
                // Whitelist avoids argument injection. Login region does not change WoW realm membership.
                if (!AppSettings.IsSupportedRegion(profile.LoginRegion))
                    throw new InvalidDataException("战网登录区域不受支持：" + profile.LoginRegion);
                start.Arguments = "--setregion=" + profile.LoginRegion;
                Logger.Info("Launch", "profile=" + region + " region_arg=" + profile.LoginRegion);
                Process launched = Process.Start(start);
                if (launched == null) throw new InvalidOperationException("Windows 没有返回战网启动进程。请检查战网安装情况。");
                launched.Dispose();
                RefreshData();
                SetStatus("数据库路径已切换，战网已启动；正在复核 Agent 是否覆盖记录...", orange);
                verificationRegion = region;
                verificationTarget = target;
                verificationNormalCode = "wow";
                verificationSecondCode = secondCode;
                verificationAttempts = 0;
                verificationMismatches = 0;
                verificationTimer.Start();
            }
            catch (OperationCanceledException e)
            {
                Logger.Info("Canceled at " + stage, e.Message);
                SetStatus("已取消：未完成切换。", orange);
            }
            catch (UnauthorizedAccessException e)
            {
                Logger.Error(stage, e);
                SetStatus("权限不足，切换失败。", red);
                if (!TryElevate(region, e.Message)) ShowError(stage, e, region);
            }
            catch (Exception e)
            {
                Logger.Error(stage, e);
                SetStatus((pathsReady ? "两项路径已切换，但战网未能正常启动：" : "切换失败：") + e.Message, red);
                ShowError(stage, e, region);
            }
            finally { SetBusy(false); SafeRefresh(); }
        }
        private static bool IsAdministrator()
        {
            try
            {
                WindowsPrincipal principal = new WindowsPrincipal(WindowsIdentity.GetCurrent());
                return principal.IsInRole(WindowsBuiltInRole.Administrator);
            }
            catch { return false; }
        }
        private bool TryElevate(string region, string detail)
        {
            if (IsAdministrator()) return false;
            DialogResult answer = MessageBox.Show(this,
                "Windows 拒绝写入战网安装记录。\r\n\r\n" + detail + "\r\n\r\n" +
                "是否允许通过 Windows UAC 以管理员身份重新启动并继续本次切换？",
                "需要管理员权限", MessageBoxButtons.YesNo, MessageBoxIcon.Question);
            if (answer != DialogResult.Yes) return false;
            try
            {
                ProcessStartInfo info = new ProcessStartInfo(Application.ExecutablePath);
                info.UseShellExecute = true;
                info.Verb = "runas";
                info.Arguments = "--auto=" + region;
                Process restarted = Process.Start(info);
                if (restarted != null) restarted.Dispose();
                BeginInvoke((MethodInvoker)delegate { Close(); });
                return true;
            }
            catch (Exception ex)
            {
                Logger.Error("Elevate attempt", ex);
                ShowError("申请管理员权限（UAC）", ex, region);
                return true;
            }
        }
        private void VerifyAfterLaunch(object sender, EventArgs e)
        {
            if (busy || verificationRegion == "") return;
            verificationAttempts++;
            try
            {
                ProductRecord[] records = ReadRecords();
                bool normalOk = HasProductAt(records, verificationNormalCode, verificationTarget);
                bool secondOk = HasProductAt(records, verificationSecondCode, verificationTarget);
                if (!normalOk || !secondOk)
                {
                    verificationMismatches++;
                    Logger.Info("POSTLAUNCH_MISMATCH", "attempt=" + verificationAttempts +
                        " mismatch=" + verificationMismatches +
                        " target=" + verificationTarget +
                        " digest=" + DbFingerprint(File.ReadAllBytes(dbPath)) +
                        " modified_utc=" + File.GetLastWriteTimeUtc(dbPath).ToString("o") +
                        " wow=" + DescribeProduct(records, verificationNormalCode) +
                        " secondary=" + DescribeProduct(records, verificationSecondCode));
                    if (verificationMismatches < 2)
                    {
                        SetStatus("战网可能正在重写安装路径，正在二次确认...", orange);
                        return;
                    }
                    verificationTimer.Stop();
                    string region = verificationRegion;
                    verificationRegion = "";
                    RefreshData();
                    InvalidDataException error = new InvalidDataException(
                        "本次切换在启动战网前" + (prelaunchWasVerified ? "已通过完整路径校验" : "未完成路径校验") +
                        "，但战网启动后连续两次检测到安装记录恢复或变更。\r\n" +
                        "期望路径：" + verificationTarget + "\r\n" +
                        "wow 当前安装记录：" + DescribeProduct(records, verificationNormalCode) + "\r\n" +
                        verificationSecondCode + " 当前安装记录：" + DescribeProduct(records, verificationSecondCode) + "\r\n\r\n" +
                        "这说明战网/Agent 可能在启动后重写数据库；尚无法确定具体来源。" +
                        "程序不会在战网运行时强制修改。请将本报错和日志中的 PRELAUNCH_VERIFIED、POSTLAUNCH_MISMATCH 发给我。");
                    Logger.Error("Post launch verification", error);
                    SetStatus("战网启动后回写安装路径，切换未真正成功。", red);
                    ShowError("启动后路径复核", error, region);
                    return;
                }
                verificationMismatches = 0;
                if (verificationAttempts >= 24)
                {
                    verificationTimer.Stop();
                    string verifiedRegion = verificationRegion;
                    verificationRegion = "";
                    Logger.Info("POSTLAUNCH_PASSED", "region=" + verifiedRegion + " attempts=" + verificationAttempts);
                    SetStatus("数据库连续复核通过。请同时查看战网设置界面确认。", green);
                }
            }
            catch (Exception verificationError)
            {
                // During launcher update, product.db can be briefly locked. Report a persistent failure
                // only after repeated attempts; never auto-rewrite while Agent runs.
                Logger.Error("Verify attempt " + verificationAttempts, verificationError);
                if (verificationAttempts >= 3)
                {
                    verificationTimer.Stop();
                    string region = verificationRegion;
                    verificationRegion = "";
                    SetStatus("战网启动后无法验证路径，请查看错误。", orange);
                    ShowError("启动后读取验证", verificationError, region);
                }
            }
        }
        private void RestoreLatestBackup()
        {
            if (busy) return;
            verificationTimer.Stop();
            verificationRegion = "";
            SetBusy(true);
            string stage = "选择备份";
            try
            {
                if (!Directory.Exists(Logger.BackupsDirectory))
                    throw new FileNotFoundException("尚无可恢复的备份。只有发生实际修改时才会生成备份。");
                FileInfo[] files = new DirectoryInfo(Logger.BackupsDirectory).GetFiles("product_*.db.bak");
                FileInfo latest = files.OrderByDescending(f => f.LastWriteTimeUtc).FirstOrDefault();
                if (latest == null) throw new FileNotFoundException("没有找到自动备份文件。", Logger.BackupsDirectory);
                if (MessageBox.Show(this,
                    "确定恢复最近一次切换前的战网数据库？\r\n\r\n" + latest.Name +
                    "\r\n\r\n这将把游戏路径恢复到这份备份里的记录。",
                    "确认恢复备份", MessageBoxButtons.YesNo, MessageBoxIcon.Question) != DialogResult.Yes) return;
                stage = "检查当前是否有游戏下载 / 更新";
                AssertNoActiveInstall("wowf");
                stage = "自动退出战网并确认所有进程停止";
                EnsureClosed();
                stage = "校验备份";
                byte[] backupBytes = File.ReadAllBytes(latest.FullName);
                ProductDbEditor.Inspect(backupBytes);
                stage = "备份当前数据库";
                string safetyBackup = NewBackup("before_restore");
                stage = "恢复安装数据库";
                ReplaceAtomically(backupBytes, safetyBackup, File.ReadAllBytes(safetyBackup));
                Logger.Info("Restore", "Restored " + latest.FullName + " ; previous backup=" + safetyBackup);
                SetStatus("✓ 已恢复最近备份，未修改任何游戏文件。", green);
                MessageBox.Show(this, "恢复成功。现在可以正常启动战网。", "恢复完成", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (OperationCanceledException) { SetStatus("已取消恢复。", orange); }
            catch (Exception e)
            {
                Logger.Error("Restore: " + stage, e);
                SetStatus("恢复失败，请查看报错。", red);
                ShowError("恢复备份：" + stage, e, "RESTORE");
            }
            finally { SetBusy(false); SafeRefresh(); }
        }
        private string Diagnostics()
        {
            StringBuilder data = new StringBuilder();
            data.AppendLine("WoW Region Switcher WinForms v0.5.2-dev 诊断");
            data.AppendLine("时间：" + DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss"));
            data.AppendLine("系统：" + Environment.OSVersion + " / " + (Environment.Is64BitOperatingSystem ? "64-bit" : "32-bit"));
            data.AppendLine("管理员权限：" + IsAdministrator());
            data.AppendLine("战网数据库：" + dbPath);
            data.AppendLine("数据库是否存在：" + File.Exists(dbPath));
            data.AppendLine("入口一目录：" + ChinaRoot + "，有效：" + SeemsLikeGameFolder(ChinaRoot));
            data.AppendLine("入口二目录：" + AsiaRoot + "，有效：" + SeemsLikeGameFolder(AsiaRoot));
            data.AppendLine("日志：" + Logger.LogFile);
            data.AppendLine("配置：" + AppSettings.FilePath);
            data.AppendLine("入口一：" + settings.First.Name + "，登录区域=" + settings.First.LoginRegion);
            data.AppendLine("入口二：" + settings.Second.Name + "，登录区域=" + settings.Second.LoginRegion);
            try
            {
                ProductRecord[] records = ReadRecords();
                foreach (ProductRecord record in records.Where(r => (r.Code ?? "").StartsWith("wow", StringComparison.OrdinalIgnoreCase)))
                    data.AppendLine("产品 " + record.Code + " -> " + record.InstallPath +
                        " [play_region=" + (record.PlayRegion == "" ? "<empty>" : record.PlayRegion) + "]");
                data.AppendLine("数据库指纹（SHA256前8字节）：" + DbFingerprint(File.ReadAllBytes(dbPath)));
                data.AppendLine("数据库修改时间UTC：" + File.GetLastWriteTimeUtc(dbPath).ToString("o"));
                byte[] snapshot = File.ReadAllBytes(dbPath);
                string[] blockers = EffectiveInstallBlockers(snapshot, false);
                string[] rawBlockers = ProductDbEditor.FindSwitchBlockers(snapshot);
                string[] dormantAgent = rawBlockers.Except(blockers).ToArray();
                data.AppendLine("当前应阻止切换的安装/更新任务：" + (blockers.Length == 0 ? "无" : string.Join("；", blockers)));
                data.AppendLine("已停止 Agent 的残留操作记录（仅供诊断）：" +
                    (dormantAgent.Length == 0 ? "无" : string.Join("；", dormantAgent)));
                string newFile = Path.Combine(Path.GetDirectoryName(dbPath), "product.db.new");
                data.AppendLine("product.db.new 是否存在：" + File.Exists(newFile));
                string secondary = IdentifySecondary(records);
                data.AppendLine("入口一目录匹配：" + (secondary != "" && HasProductAt(records, "wow", ChinaRoot) && HasProductAt(records, secondary, ChinaRoot)));
                data.AppendLine("入口二目录匹配：" + (secondary != "" && HasProductAt(records, "wow", AsiaRoot) && HasProductAt(records, secondary, AsiaRoot)));
            }
            catch (Exception e) { data.AppendLine("读取数据库失败：" + e); }
            try
            {
                Process[] processes = BlockingProcesses();
                try { data.AppendLine("运行中的相关进程：" + string.Join(", ", processes.Select(DescribeProcess).ToArray())); }
                finally { foreach (Process p in processes) p.Dispose(); }
            }
            catch (Exception e) { data.AppendLine("读取进程失败：" + e.Message); }
            return data.ToString();
        }
        private void CopyDiagnostics()
        {
            try
            {
                string result = Diagnostics();
                Clipboard.SetText(result);
                MessageBox.Show(this, "诊断信息已复制到剪贴板。\r\n可以直接粘贴给我排查，不包含账号密码。", "复制成功", MessageBoxButtons.OK, MessageBoxIcon.Information);
            }
            catch (Exception e) { Logger.Error("Copy diagnostics", e); ShowError("复制诊断信息", e, "DIAG"); }
        }
        private void OpenLogs()
        {
            try
            {
                Directory.CreateDirectory(Logger.DirectoryPath);
                Process.Start("explorer.exe", "\"" + Logger.DirectoryPath + "\"");
            }
            catch (Exception e) { Logger.Error("Open logs", e); ShowError("打开日志目录", e, "LOG"); }
        }
        private void ShowError(string stage, Exception error, string region)
        {
            string id = DateTime.Now.ToString("yyyyMMdd-HHmmss");
            StringBuilder message = new StringBuilder();
            message.AppendLine("错误编号：" + id);
            message.AppendLine("选择入口：" + region);
            message.AppendLine("出错步骤：" + stage);
            message.AppendLine("异常类型：" + error.GetType().FullName);
            message.AppendLine("错误原因：" + error.Message);
            message.AppendLine();
            message.AppendLine("技术详情：");
            message.AppendLine(error.ToString());
            message.AppendLine();
            message.AppendLine("---- 本机状态（无账号凭据） ----");
            message.Append(Diagnostics());
            message.AppendLine();
            message.AppendLine("日志路径：" + Logger.LogFile);
            try
            {
                if (File.Exists(Logger.LogFile))
                {
                    string[] all = File.ReadAllLines(Logger.LogFile);
                    message.AppendLine();
                    message.AppendLine("---- 最近日志（自动附带，无需另外打开文件） ----");
                    foreach (string line in all.Skip(Math.Max(0, all.Length - 30)))
                        message.AppendLine(line.Length > 1000 ? line.Substring(0, 1000) + " ..." : line);
                }
            }
            catch (Exception logError)
            {
                message.AppendLine("读取近期日志失败：" + logError.Message);
            }
            using (ErrorForm form = new ErrorForm(message.ToString(), id)) form.ShowDialog(this);
        }
        protected override void OnFormClosed(FormClosedEventArgs e)
        {
            verificationTimer.Stop();
            verificationTimer.Dispose();
            base.OnFormClosed(e);
        }
    }

    internal sealed class ErrorForm : Form
    {
        public ErrorForm(string detail, string errorId)
        {
            Text = "魔兽双区服助手 - 操作失败（" + errorId + "）";
            StartPosition = FormStartPosition.CenterParent;
            MinimumSize = new Size(650, 470);
            Size = new Size(770, 540);
            Font = new Font("Microsoft YaHei UI", 9F);
            BackColor = Color.FromArgb(23, 28, 38);
            ForeColor = Color.White;
            ShowIcon = true;
            Icon = SystemIcons.Error;
            TableLayoutPanel panel = new TableLayoutPanel() { Dock = DockStyle.Fill, RowCount = 3, Padding = new Padding(18) };
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 64));
            panel.RowStyles.Add(new RowStyle(SizeType.Percent, 100));
            panel.RowStyles.Add(new RowStyle(SizeType.Absolute, 52));
            Label header = new Label() { Text = "⚠ 操作失败，详情如下（可截图或复制后发给我）", Dock = DockStyle.Fill,
                TextAlign = ContentAlignment.MiddleLeft, Font = new Font("Microsoft YaHei UI", 12F, FontStyle.Bold), ForeColor = Color.FromArgb(255, 141, 145) };
            TextBox details = new TextBox() { Multiline = true, ScrollBars = ScrollBars.Both,
                WordWrap = false, ReadOnly = true, Dock = DockStyle.Fill, Text = detail,
                BackColor = Color.FromArgb(34, 40, 54), ForeColor = Color.White,
                Font = new Font("Consolas", 9F) };
            FlowLayoutPanel actions = new FlowLayoutPanel() { Dock = DockStyle.Fill,
                FlowDirection = FlowDirection.RightToLeft, Padding = new Padding(0, 10, 0, 0) };
            Button close = new Button() { Text = "关闭", Width = 105, Height = 33 };
            Button copy = new Button() { Text = "复制完整报错", Width = 155, Height = 33 };
            close.Click += delegate { Close(); };
            copy.Click += delegate
            {
                try { Clipboard.SetText(detail); MessageBox.Show(this, "已复制，可直接粘贴发送给我。", "已复制", MessageBoxButtons.OK, MessageBoxIcon.Information); }
                catch (Exception ex) { MessageBox.Show(this, ex.Message, "复制失败", MessageBoxButtons.OK, MessageBoxIcon.Error); }
            };
            actions.Controls.Add(close);
            actions.Controls.Add(copy);
            panel.Controls.Add(header, 0, 0);
            panel.Controls.Add(details, 0, 1);
            panel.Controls.Add(actions, 0, 2);
            Controls.Add(panel);
            AcceptButton = close;
        }
    }
}
