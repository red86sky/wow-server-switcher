// Native Windows Forms application (.NET Framework 4.0+). No VBScript / hidden console.
using System;
using System.IO;
using System.Threading;
using System.Windows.Forms;

namespace WowRegionSwitcher
{
    internal static class Program
    {
        [STAThread]
        private static void Main(string[] args)
        {
            Application.EnableVisualStyles();
            Application.SetCompatibleTextRenderingDefault(false);
            Application.SetUnhandledExceptionMode(UnhandledExceptionMode.CatchException);
            Application.ThreadException += delegate(object sender, ThreadExceptionEventArgs e)
            {
                ReportFatal(e.Exception, "WinForms UI thread");
            };
            AppDomain.CurrentDomain.UnhandledException += delegate(object sender, UnhandledExceptionEventArgs e)
            {
                ReportFatal(e.ExceptionObject as Exception ?? new Exception("Unknown fatal error"), "Background thread");
            };

            bool selfTest = Array.Exists(args,
                arg => string.Equals(arg, "--self-test", StringComparison.OrdinalIgnoreCase));
            try
            {
                OfflineSelfTests.Run(); // Safety check, in memory only.
                if (selfTest)
                {
                    Console.WriteLine("Offline self-tests: PASS");
                    return; // No user settings, game files or Battle.net processes accessed.
                }
                string requestedRegion = "";
                foreach (string arg in args)
                {
                    if (string.Equals(arg, "--auto=CN", StringComparison.OrdinalIgnoreCase)) requestedRegion = "CN";
                    if (string.Equals(arg, "--auto=ASIA", StringComparison.OrdinalIgnoreCase)) requestedRegion = "ASIA";
                }
                Application.Run(new MainForm(requestedRegion));
            }
            catch (Exception ex)
            {
                Environment.ExitCode = 1;
                if (selfTest) Console.Error.WriteLine("Offline self-tests: FAIL\r\n" + ex);
                else ReportFatal(ex, "Application startup");
            }
        }

        private static void ReportFatal(Exception e, string stage)
        {
            try
            {
                Logger.Error(stage, e);
                string message = "程序发生意外错误。没有执行任何进一步操作。\r\n\r\n" +
                    "阶段：" + stage + "\r\n" +
                    "原因：" + e.Message + "\r\n\r\n" +
                    "完整日志：" + Logger.LogFile + "\r\n\r\n" +
                    "请把这段文字或者报错截图发给我。";
                try
                {
                    using (ErrorForm errorDialog = new ErrorForm(message + "\r\n\r\n" + e.ToString(),
                        DateTime.Now.ToString("yyyyMMdd-HHmmss"))) errorDialog.ShowDialog();
                }
                catch
                {
                    MessageBox.Show(message, "魔兽双区服助手 - 错误", MessageBoxButtons.OK, MessageBoxIcon.Error);
                }
            }
            catch { /* Last-resort handler; never throw recursively. */ }
        }
    }

    internal static class Logger
    {
        public static string DirectoryPath
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData), "WoWRegionSwitcherWinForms"); }
        }
        public static string BackupsDirectory { get { return Path.Combine(DirectoryPath, "backups"); } }
        public static string LogFile { get { return Path.Combine(DirectoryPath, "launcher.log"); } }
        public static string SavedLauncherFile { get { return Path.Combine(DirectoryPath, "battlenet_exe.txt"); } }
        public static void Info(string context, string data) { Write("INFO", context + " | " + data); }
        public static void Error(string context, Exception ex) { Write("ERROR", context + " | " + ex); }
        public static void Write(string level, string message)
        {
            try
            {
                Directory.CreateDirectory(DirectoryPath);
                File.AppendAllText(LogFile, DateTime.Now.ToString("yyyy-MM-dd HH:mm:ss.fff") + " " + level + " " + message + Environment.NewLine);
            }
            catch { /* Logging failure must not suppress the original error dialog. */ }
        }
    }
}
