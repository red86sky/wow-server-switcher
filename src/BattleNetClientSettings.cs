// Only edits the precisely identified Client.Install.DefaultInstallPath JSON string.
// All unrelated JSON tokens, property order and whitespace are left byte-for-byte intact.
// Battle.net is undocumented software: fail closed if the expected structure isn't found.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WowRegionSwitcher
{
    internal sealed class ClientConfigChange
    {
        public string Path;
        public string PreviousDirectory;
        public byte[] Original;
        public byte[] Updated;
    }

    internal static class BattleNetClientSettings
    {
        private const int MaxConfigSize = 4 * 1024 * 1024;
        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);

        internal static string ConfigFolder
        {
            get { return Path.Combine(Environment.GetFolderPath(Environment.SpecialFolder.ApplicationData), "Battle.net"); }
        }
        private static string Decode(byte[] bytes)
        {
            int bom = bytes.Length >= 3 && bytes[0] == 239 && bytes[1] == 187 && bytes[2] == 191 ? 3 : 0;
            return StrictUtf8.GetString(bytes, bom, bytes.Length - bom);
        }
        private static byte[] Encode(string text, bool withBom)
        {
            byte[] raw = StrictUtf8.GetBytes(text);
            if (!withBom) return raw;
            byte[] bytes = new byte[raw.Length + 3];
            bytes[0] = 239; bytes[1] = 187; bytes[2] = 191;
            Buffer.BlockCopy(raw, 0, bytes, 3, raw.Length);
            return bytes;
        }
        private static byte[] ReadLimited(string path)
        {
            FileInfo info = new FileInfo(path);
            if (!info.Exists || info.Length == 0 || info.Length > MaxConfigSize)
                throw new InvalidDataException("战网客户端配置缺失、为空或超出安全大小限制：" + path);
            return File.ReadAllBytes(path);
        }

        // The official Battle.net.config is preferred. Some Battle.net revisions
        // keep settings in a separate alphanumeric .config file; accept that only
        // when there is exactly one unambiguous matching file.
        internal static string Locate()
        {
            if (!Directory.Exists(ConfigFolder))
                throw new FileNotFoundException("没有找到战网用户配置目录。请先正常启动战网并保存一次“下载 → 默认安装目录”。", ConfigFolder);
            string main = Path.Combine(ConfigFolder, "Battle.net.config");
            if (File.Exists(main))
            {
                try { if (FindValue(Decode(ReadLimited(main)), false) != null) return main; }
                catch (InvalidDataException) { throw; } // corrupt primary must not be silently skipped
            }
            List<string> matches = new List<string>();
            foreach (string candidate in Directory.GetFiles(ConfigFolder, "*.config"))
            {
                if (string.Equals(candidate, main, StringComparison.OrdinalIgnoreCase)) continue;
                try { if (FindValue(Decode(ReadLimited(candidate)), false) != null) matches.Add(candidate); }
                catch (InvalidDataException) { /* unrelated or unsupported .config; do not modify */ }
            }
            if (matches.Count == 1) return matches[0];
            if (matches.Count > 1)
                throw new InvalidDataException("找到多个包含默认安装目录的战网配置文件，无法确定哪个正在使用。\r\n" +
                    string.Join("\r\n", matches.ToArray()) + "\r\n本次没有修改文件。请提供诊断日志以完善自动识别。");
            throw new InvalidDataException("无法在 %APPDATA%\\Battle.net 的配置文件中定位 Client.Install.DefaultInstallPath。\r\n" +
                "当前版本的战网可能使用了不同的配置格式。为了防止修改账号或其他设置，已取消切换。\r\n" +
                "请在战网“设置 → 下载”手动更改一次默认安装目录，退出战网后重试。");
        }
        internal static ClientConfigChange Prepare(string desiredDirectory)
        {
            string path = Locate();
            byte[] old = ReadLimited(path);
            string text = Decode(old);
            ValueLocation location = FindValue(text, true);
            string directory = AppSettings.NormalizedFolder(desiredDirectory);
            string escaped = Quote(directory);
            string patched = text.Substring(0, location.Start) + escaped + text.Substring(location.End);
            byte[] next = Encode(patched, old.Length >= 3 && old[0] == 239 && old[1] == 187 && old[2] == 191);
            if (!ProductDbEditor.SameFolder(FindValue(Decode(next), true).Value, directory))
                throw new InvalidDataException("战网默认安装目录的编辑结果无法通过复核。");
            return new ClientConfigChange { Path = path, PreviousDirectory = location.Value, Original = old, Updated = next };
        }
        internal static string ReadDirectory(string path)
        {
            return FindValue(Decode(ReadLimited(path)), true).Value;
        }
        private static string Quote(string value)
        {
            StringBuilder b = new StringBuilder("\"");
            foreach (char c in value)
            {
                if (c == '"' || c == '\\') { b.Append('\\'); b.Append(c); }
                else if (c == '\b') b.Append("\\b");
                else if (c == '\f') b.Append("\\f");
                else if (c == '\n') b.Append("\\n");
                else if (c == '\r') b.Append("\\r");
                else if (c == '\t') b.Append("\\t");
                else if (c < 32) b.Append("\\u" + ((int)c).ToString("x4"));
                else b.Append(c);
            }
            return b.Append('"').ToString();
        }

        internal static string PatchForTest(string original, string newDirectory)
        {
            ValueLocation location = FindValue(original, true);
            return original.Substring(0, location.Start) + Quote(newDirectory) + original.Substring(location.End);
        }
        internal static string ReadForTest(string original) { return FindValue(original, true).Value; }

        private sealed class ValueLocation
        {
            public int Start;
            public int End;
            public string Value;
        }
        private static ValueLocation FindValue(string data, bool required)
        {
            JsonLocator reader = new JsonLocator(data);
            List<ValueLocation> found = reader.Scan();
            if (found.Count > 1)
                throw new InvalidDataException("战网配置中默认安装目录字段出现多次，拒绝不明确的修改。");
            if (found.Count == 0 && required)
                throw new InvalidDataException("配置中不存在 Client.Install.DefaultInstallPath（也不存在 User.Client.Install.DefaultInstallPath）。");
            return found.Count == 1 ? found[0] : null;
        }
        private sealed class JsonLocator
        {
            private readonly string text;
            private readonly List<ValueLocation> matches = new List<ValueLocation>();
            private int pos;
            private int depth;
            internal JsonLocator(string input) { text = input; }
            internal List<ValueLocation> Scan()
            {
                SkipSpace();
                ReadValue(new List<string>());
                SkipSpace();
                if (pos != text.Length) throw new InvalidDataException("战网配置存在 JSON 结尾垃圾数据。");
                return matches;
            }
            private void SkipSpace()
            {
                while (pos < text.Length && (text[pos] == ' ' || text[pos] == '\t' || text[pos] == '\r' || text[pos] == '\n')) pos++;
            }
            private void Require(char c)
            {
                SkipSpace();
                if (pos >= text.Length || text[pos] != c) throw new InvalidDataException("战网 JSON 配置结构无效（字符 " + c + "）。");
                pos++;
            }
            private bool Consume(char c)
            {
                SkipSpace();
                if (pos < text.Length && text[pos] == c) { pos++; return true; }
                return false;
            }
            private string ReadString()
            {
                Require('"');
                StringBuilder result = new StringBuilder();
                while (pos < text.Length)
                {
                    char ch = text[pos++];
                    if (ch == '"') return result.ToString();
                    if (ch < 32) throw new InvalidDataException("战网 JSON 字符串包含未转义控制字符。");
                    if (ch != '\\') { result.Append(ch); continue; }
                    if (pos == text.Length) throw new InvalidDataException("战网 JSON 转义不完整。");
                    char escape = text[pos++];
                    switch (escape)
                    {
                        case '"': result.Append('"'); break;
                        case '\\': result.Append('\\'); break;
                        case '/': result.Append('/'); break;
                        case 'b': result.Append('\b'); break;
                        case 'f': result.Append('\f'); break;
                        case 'n': result.Append('\n'); break;
                        case 'r': result.Append('\r'); break;
                        case 't': result.Append('\t'); break;
                        case 'u':
                            if (pos + 4 > text.Length) throw new InvalidDataException("战网 JSON Unicode 转义不完整。");
                            int code;
                            if (!int.TryParse(text.Substring(pos, 4), System.Globalization.NumberStyles.HexNumber,
                                System.Globalization.CultureInfo.InvariantCulture, out code))
                                throw new InvalidDataException("战网 JSON Unicode 转义不合法。");
                            result.Append((char)code);
                            pos += 4;
                            break;
                        default: throw new InvalidDataException("战网 JSON 含不合法的转义符。");
                    }
                }
                throw new InvalidDataException("战网 JSON 字符串没有正确终止。");
            }
            private static bool IsTarget(List<string> path)
            {
                return (path.Count == 3 && path[0] == "Client" && path[1] == "Install" && path[2] == "DefaultInstallPath") ||
                    (path.Count == 4 && path[0] == "User" && path[1] == "Client" && path[2] == "Install" && path[3] == "DefaultInstallPath");
            }
            private void ReadValue(List<string> path)
            {
                if (++depth > 64) throw new InvalidDataException("战网 JSON 嵌套层次超出安全限制。");
                try
                {
                    SkipSpace();
                    if (pos >= text.Length) throw new InvalidDataException("战网 JSON 记录不完整。");
                    char c = text[pos];
                    if (c == '{')
                    {
                        pos++;
                        if (Consume('}')) return;
                        while (true)
                        {
                            string key = ReadString();
                            Require(':');
                            path.Add(key);
                            ReadValue(path);
                            path.RemoveAt(path.Count - 1);
                            if (Consume('}')) break;
                            Require(',');
                        }
                    }
                    else if (c == '[')
                    {
                        pos++;
                        if (Consume(']')) return;
                        while (true)
                        {
                            path.Add("[]");
                            ReadValue(path);
                            path.RemoveAt(path.Count - 1);
                            if (Consume(']')) break;
                            Require(',');
                        }
                    }
                    else if (c == '"')
                    {
                        int start = pos;
                        string value = ReadString();
                        if (IsTarget(path)) matches.Add(new ValueLocation { Start = start, End = pos, Value = value });
                    }
                    else
                    {
                        if (IsTarget(path)) throw new InvalidDataException("战网默认安装目录字段不是字符串。");
                        int start = pos;
                        while (pos < text.Length && text[pos] != ',' && text[pos] != '}' && text[pos] != ']' &&
                               text[pos] != ' ' && text[pos] != '\r' && text[pos] != '\n' && text[pos] != '\t') pos++;
                        string raw = text.Substring(start, pos - start);
                        double number;
                        if (raw != "true" && raw != "false" && raw != "null" &&
                            (!double.TryParse(raw, System.Globalization.NumberStyles.Float,
                                System.Globalization.CultureInfo.InvariantCulture, out number) || double.IsNaN(number)))
                            throw new InvalidDataException("战网 JSON 包含不合法的标量数据。");
                    }
                }
                finally { depth--; }
            }
        }
    }
}
