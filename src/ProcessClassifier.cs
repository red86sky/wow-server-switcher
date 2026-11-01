// Conservative, testable process classifier.  No broad StartsWith("Wow") blocking.
// Blizzard can add executable variants; unknown Wow* variants are considered
// game processes ONLY when Windows resolves their image under a configured WoW root.
using System;
using System.Collections.Generic;

namespace WowRegionSwitcher
{
    internal static class ProcessClassifier
    {
        private static readonly HashSet<string> GameExecutableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Wow", "WowClassic", "WowClassicT", "WowClassicB", "WowClassicBeta",
            "WowB", "WowT", "WowF", "WowForever", "Wow-64", "WowClassic-64",
            "WowRetail", "WowBeta"
        };

        private static readonly HashSet<string> BattleNetExecutableNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "Battle.net", "Battle.net Launcher", "Battle.net Helper", "Battle.net Desktop App"
        };

        public static bool IsOwnApplicationName(string name)
        {
            if (string.IsNullOrEmpty(name)) return false;
            // Includes cached WinForms child process and the single-EXE bootstrapper.
            return name.StartsWith("WowRegionSwitcher", StringComparison.OrdinalIgnoreCase) ||
                   name.StartsWith("魔兽双区服助手", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsKnownGameName(string name)
        {
            return !string.IsNullOrEmpty(name) && GameExecutableNames.Contains(name);
        }

        public static bool IsBattleNetName(string name)
        {
            return !string.IsNullOrEmpty(name) && BattleNetExecutableNames.Contains(name);
        }

        public static bool IsAgentName(string name)
        {
            return string.Equals(name, "Agent", StringComparison.OrdinalIgnoreCase) ||
                   string.Equals(name, "Blizzard Update Agent", StringComparison.OrdinalIgnoreCase);
        }

        public static bool IsPossibleAdditionalGameName(string name)
        {
            return !string.IsNullOrEmpty(name) && !IsOwnApplicationName(name) &&
                name.StartsWith("Wow", StringComparison.OrdinalIgnoreCase) && !IsKnownGameName(name);
        }

        private static string Normalize(string path)
        {
            if (string.IsNullOrWhiteSpace(path)) return "";
            return path.Trim().Replace('/', '\\').TrimEnd('\\');
        }

        public static bool IsWithinGameFolder(string image, string cnRoot, string asiaRoot)
        {
            string fullPath = Normalize(image);
            if (fullPath == "" || !fullPath.EndsWith(".exe", StringComparison.OrdinalIgnoreCase)) return false;
            string[] roots = {cnRoot, asiaRoot};
            foreach (string root in roots)
            {
                string folder = Normalize(root);
                // Delimiter makes 'World of Warcraft2' not match 'World of Warcraft'.
                if (folder != "" && fullPath.StartsWith(folder + "\\", StringComparison.OrdinalIgnoreCase))
                    return true;
            }
            return false;
        }

        public static bool IsGameProcess(string name, string image, string cnRoot, string asiaRoot)
        {
            if (IsOwnApplicationName(name)) return false;
            if (IsKnownGameName(name)) return true;
            return IsPossibleAdditionalGameName(name) && IsWithinGameFolder(image, cnRoot, asiaRoot);
        }
    }
}
