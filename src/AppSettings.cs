// Two configurable launch profiles. Settings are stored locally, never embedded into releases.
// .NET Framework 4.x; no third-party dependencies.
using System;
using System.IO;
using System.Text;
using System.Xml;

namespace WowRegionSwitcher
{
    internal sealed class LaunchProfile
    {
        public string Id;
        public string Name;
        public string GameFolder;
        public string LoginRegion;

        public LaunchProfile Clone()
        {
            return new LaunchProfile { Id = Id, Name = Name, GameFolder = GameFolder, LoginRegion = LoginRegion };
        }
    }

    internal sealed class AppSettings
    {
        public LaunchProfile First;
        public LaunchProfile Second;

        public static string FilePath
        {
            get { return Path.Combine(Logger.DirectoryPath, "settings.xml"); }
        }

        public static AppSettings CreateDefaults()
        {
            return new AppSettings {
                First = new LaunchProfile {
                    Id = "CN", Name = "国服", LoginRegion = "CN",
                    GameFolder = @"D:\Games\Battle.netLibrary_cn\World of Warcraft"
                },
                Second = new LaunchProfile {
                    Id = "ASIA", Name = "亚服", LoginRegion = "TW",
                    GameFolder = @"D:\Games\Battle.netLibrary\World of Warcraft"
                }
            };
        }

        public AppSettings Clone()
        {
            return new AppSettings { First = First.Clone(), Second = Second.Clone() };
        }

        public LaunchProfile ForId(string id)
        {
            if (string.Equals(id, "CN", StringComparison.OrdinalIgnoreCase)) return First;
            if (string.Equals(id, "ASIA", StringComparison.OrdinalIgnoreCase)) return Second;
            throw new ArgumentException("Unknown launch profile identifier: " + id);
        }

        // Battle.net client launch-region option, NOT a guaranteed WoW game-realm selector.
        // Do not conflate this with product.db's play_region, which we never rewrite.
        public static bool IsSupportedRegion(string code)
        {
            string value = (code ?? "").Trim().ToUpperInvariant();
            return value == "CN" || value == "TW" || value == "KR" || value == "US" || value == "EU";
        }

        public static string NormalizedFolder(string value)
        {
            if (string.IsNullOrWhiteSpace(value)) throw new ArgumentException("游戏安装目录不能为空。");
            string entered = value.Trim().Trim('"');
            if (!Path.IsPathRooted(entered))
                throw new ArgumentException("游戏目录必须使用完整绝对路径。" );
            string full = Path.GetFullPath(entered);
            if (!Path.IsPathRooted(full) || full.Length < 4)
                throw new ArgumentException("游戏目录必须是完整的绝对路径。" );
            return full.TrimEnd(Path.DirectorySeparatorChar, Path.AltDirectorySeparatorChar);
        }

        private static void ValidateProfile(LaunchProfile profile)
        {
            if (profile == null) throw new InvalidDataException("Missing launch profile.");
            profile.Name = (profile.Name ?? "").Trim();
            if (profile.Name.Length == 0 || profile.Name.Length > 25 ||
                profile.Name.IndexOfAny(new char[] { '\r', '\n', '\t' }) >= 0)
                throw new InvalidDataException("入口名称需要为 1–25 个字符，不能包含换行。");
            profile.LoginRegion = (profile.LoginRegion ?? "").Trim().ToUpperInvariant();
            if (!IsSupportedRegion(profile.LoginRegion))
                throw new InvalidDataException("不支持的战网启动区域：" + profile.LoginRegion + "。允许 CN/TW/KR/US/EU。");
            profile.GameFolder = NormalizedFolder(profile.GameFolder);
        }

        public void Validate()
        {
            if (First == null || Second == null || First.Id != "CN" || Second.Id != "ASIA")
                throw new InvalidDataException("配置文件缺少两个预期的启动入口。" );
            ValidateProfile(First);
            ValidateProfile(Second);
            if (ProductDbEditor.SameFolder(First.GameFolder, Second.GameFolder))
                throw new InvalidDataException("两个入口不能指向同一个游戏安装目录。" );
        }

        private static LaunchProfile ParseProfile(XmlNode node, string id)
        {
            XmlAttributeCollection attributes = node.Attributes;
            if (attributes == null) throw new InvalidDataException("Invalid profile attributes.");
            XmlAttribute foundId = attributes["id"];
            if (foundId == null || !string.Equals(foundId.Value, id, StringComparison.Ordinal))
                throw new InvalidDataException("配置文件入口 ID 不正确。" );
            return new LaunchProfile {
                Id = id,
                Name = ReadAttr(attributes, "name"),
                GameFolder = ReadAttr(attributes, "gameFolder"),
                LoginRegion = ReadAttr(attributes, "loginRegion")
            };
        }
        private static string ReadAttr(XmlAttributeCollection attrs, string name)
        {
            XmlAttribute attribute = attrs[name];
            if (attribute == null) throw new InvalidDataException("配置缺少 " + name + "。" );
            return attribute.Value;
        }

        public static AppSettings Load()
        {
            if (!File.Exists(FilePath)) return CreateDefaults();
            XmlReaderSettings options = new XmlReaderSettings();
            options.DtdProcessing = DtdProcessing.Prohibit;
            options.XmlResolver = null;
            XmlDocument doc = new XmlDocument();
            doc.XmlResolver = null;
            using (XmlReader reader = XmlReader.Create(FilePath, options)) doc.Load(reader);
            XmlElement root = doc.DocumentElement;
            if (root == null || root.Name != "WoWRegionSwitcherSettings" || root.GetAttribute("version") != "1")
                throw new InvalidDataException("不认识的 settings.xml 格式/版本。不会覆盖已有设置。" );
            XmlNodeList profiles = root.SelectNodes("Profile");
            if (profiles == null || profiles.Count != 2)
                throw new InvalidDataException("配置文件必须恰好有两套启动配置。" );
            AppSettings result = new AppSettings {
                First = ParseProfile(profiles[0], "CN"),
                Second = ParseProfile(profiles[1], "ASIA")
            };
            result.Validate();
            return result;
        }

        private static void WriteProfile(XmlWriter writer, LaunchProfile profile)
        {
            writer.WriteStartElement("Profile");
            writer.WriteAttributeString("id", profile.Id);
            writer.WriteAttributeString("name", profile.Name);
            writer.WriteAttributeString("loginRegion", profile.LoginRegion);
            writer.WriteAttributeString("gameFolder", profile.GameFolder);
            writer.WriteEndElement();
        }

        public void Save()
        {
            Validate();
            Directory.CreateDirectory(Logger.DirectoryPath);
            string tmp = FilePath + ".tmp." + System.Diagnostics.Process.GetCurrentProcess().Id;
            try
            {
                XmlWriterSettings options = new XmlWriterSettings();
                options.Encoding = new UTF8Encoding(false);
                options.Indent = true;
                using (XmlWriter writer = XmlWriter.Create(tmp, options))
                {
                    writer.WriteStartDocument();
                    writer.WriteStartElement("WoWRegionSwitcherSettings");
                    writer.WriteAttributeString("version", "1");
                    WriteProfile(writer, First);
                    WriteProfile(writer, Second);
                    writer.WriteEndElement();
                    writer.WriteEndDocument();
                }
                if (File.Exists(FilePath)) File.Replace(tmp, FilePath, null);
                else File.Move(tmp, FilePath);
            }
            finally { try { if (File.Exists(tmp)) File.Delete(tmp); } catch { } }
        }
    }
}
