// All regression tests are purely in-memory; NEVER touch real Battle.net/game files.
using System;
using System.IO;
using System.Linq;
using System.Text;

namespace WowRegionSwitcher
{
    internal static class OfflineSelfTests
    {
        private const string CN = @"D:\Games\Battle.netLibrary_cn\World of Warcraft";
        private const string ASIA = @"D:\Games\Battle.netLibrary\World of Warcraft";

        private static byte[] Varint(uint n)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                while (n > 127u)
                {
                    stream.WriteByte((byte)((n & 127u) | 128u));
                    n >>= 7;
                }
                stream.WriteByte((byte)n);
                return stream.ToArray();
            }
        }
        private static byte[] Concat(params byte[][] segments)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                foreach (byte[] part in segments) stream.Write(part, 0, part.Length);
                return stream.ToArray();
            }
        }
        private static byte[] TextField(int id, string data) { return Field(id, Encoding.UTF8.GetBytes(data)); }
        private static byte[] Field(int id, byte[] bytes)
        {
            return Concat(Varint((uint)((id << 3) | 2)), Varint((uint)bytes.Length), bytes);
        }
        private static byte[] NumberField(int id, uint value)
        {
            return Concat(Varint((uint)(id << 3)), Varint(value));
        }
        private static byte[] Product(string code, string folder)
        {
            return Concat(TextField(1, "test-" + code), TextField(2, code),
                Field(3, Concat(TextField(1, folder), TextField(2, "KR"))),
                TextField(9, "untouched extra protobuf field"));
        }
        private static byte[] Database(params byte[][] products)
        {
            return Concat(products.Concat(new byte[][] {
                TextField(12, "unknown top level field must remain unmodified")
            }).ToArray());
        }
        private static byte[] Entry(string code, string path) { return Field(1, Product(code, path)); }
        private static void Ensure(bool value, string reason)
        {
            if (!value) throw new InvalidOperationException("Internal offline regression test failed: " + reason);
        }
        public static void Run()
        {
            // V4.1 regression: launcher was mistaken for a WoW game simply
            // because its process name begins with 'Wow'. The reported log
            // contained ONLY WowRegionSwitcher_Managed (PID 11688).
            Ensure(!ProcessClassifier.IsGameProcess("WowRegionSwitcher_Managed", @"C:\Users\Example\AppData\Local\WoWRegionSwitcherWinForms\managed_v0_5\WowRegionSwitcher_Managed.exe", CN, ASIA),
                "self managed WinForms launcher is never a running WoW game");
            Ensure(!ProcessClassifier.IsGameProcess("WowRegionSwitcher", "", CN, ASIA),
                "self bootstrap launcher is not a WoW game");
            Ensure(!ProcessClassifier.IsGameProcess("WowRegionSwitcher_Managed", ASIA + @"\_retail_\WowRegionSwitcher_Managed.exe", CN, ASIA),
                "even inside game directory launcher name is excluded");
            Ensure(!ProcessClassifier.IsGameProcess("WowUp", @"C:\Apps\WowUp.exe", CN, ASIA),
                "unrelated Wow-prefixed applications are not blocked");
            Ensure(ProcessClassifier.IsGameProcess("Wow", "", CN, ASIA),
                "retail game executable still blocks switching");
            Ensure(ProcessClassifier.IsGameProcess("WowClassic", "", CN, ASIA),
                "classic game executable still blocks switching");
            Ensure(ProcessClassifier.IsGameProcess("WowF", "", CN, ASIA),
                "forever game executable still blocks switching");
            Ensure(ProcessClassifier.IsGameProcess("WowFutureEdition", ASIA + @"\_forever_\WowFutureEdition.exe", CN, ASIA),
                "new Wow game executable inside configured installation still blocks switching");
            Ensure(!ProcessClassifier.IsGameProcess("WowFutureEdition", ASIA + @"_other\WowFutureEdition.exe", CN, ASIA),
                "path prefix of another folder must not be mistaken for a game install");
            Ensure(ProcessClassifier.IsBattleNetName("Battle.net"), "battle.net itself is detected");
            Ensure(!ProcessClassifier.IsBattleNetName("Battle.net Notes"),
                "unrelated similarly named processes are not blocked");
            Ensure(ProcessClassifier.IsAgentName("Agent"), "agent candidates still verified");

            Ensure(ProductDbEditor.SameFolder(@"D:/Games/Battle.netLibrary/World of Warcraft/", ASIA),
                "Windows path separator and trailing slash normalization");
            Ensure(!ProductDbEditor.SameFolder(CN, ASIA), "distinguish country paths");

            // Basic: one record for each selected game, unrelated game must be unchanged.
            byte[] standard = Database(Entry("wow", CN), Entry("wowf", CN), Entry("s2", @"F:\Other Game"));
            byte[] standardUpdated = ProductDbEditor.PatchBoth(standard, ASIA, "wow", "wowf", CN, ASIA);
            ProductRecord[] standardRecords = ProductDbEditor.Inspect(standardUpdated);
            Ensure(standardRecords.Length == 3 && ProductDbEditor.HasPath(standardRecords, "wow", ASIA) &&
                ProductDbEditor.HasPath(standardRecords, "wowf", ASIA), "single records switched");
            Ensure(standardRecords[2].InstallPath == @"F:\Other Game", "unrelated game unchanged");
            Ensure(ProductDbEditor.PatchBoth(standardUpdated, ASIA, "wow", "wowf", CN, ASIA).SequenceEqual(standardUpdated),
                "idempotent repeated switch");

            // User's concrete scenario: wow=CN; two wowf entries point to CN and Asia.
            // V3.2 was incorrect because it only required ANY matching wowf entry.
            // V4 sets ALL registrations of BOTH chosen products to the target root.
            byte[] dual = Database(
                Entry("wow", "D:/Games/Battle.netLibrary_cn/World of Warcraft"),
                Entry("wowf", "D:/Games/Battle.netLibrary_cn/World of Warcraft"),
                Entry("wowf", "D:/Games/Battle.netLibrary/World of Warcraft"),
                Entry("s2", @"F:\Other Game"));
            ProductRecord[] original = ProductDbEditor.Inspect(dual);
            Ensure(original.Length == 4 && !ProductDbEditor.AllPathsAt(original, "wowf", CN),
                "mixed wowf paths must not be considered CN-ready");

            byte[] switchedCn = ProductDbEditor.PatchBoth(dual, CN, "wow", "wowf", CN, ASIA);
            ProductRecord[] cnRecords = ProductDbEditor.Inspect(switchedCn);
            Ensure(ProductDbEditor.AllPathsAt(cnRecords, "wow", CN) &&
                ProductDbEditor.AllPathsAt(cnRecords, "wowf", CN),
                "CN must change Asia wowf registration too");
            Ensure(cnRecords.Length == 4 && cnRecords[3].InstallPath == @"F:\Other Game",
                "preserve records and unrelated game");
            Ensure(!switchedCn.SequenceEqual(dual),
                "CN switch must no longer incorrectly produce a no-op");

            byte[] switchedAsia = ProductDbEditor.PatchBoth(switchedCn, ASIA, "wow", "wowf", CN, ASIA);
            ProductRecord[] asiaRecords = ProductDbEditor.Inspect(switchedAsia);
            Ensure(ProductDbEditor.AllPathsAt(asiaRecords, "wow", ASIA) &&
                ProductDbEditor.AllPathsAt(asiaRecords, "wowf", ASIA),
                "Asia must switch all three active records");
            Ensure(ProductDbEditor.PatchBoth(switchedAsia, ASIA, "wow", "wowf", CN, ASIA).SequenceEqual(switchedAsia),
                "repeat same region is idempotent");
            byte[] returnedCn = ProductDbEditor.PatchBoth(switchedAsia, CN, "wow", "wowf", CN, ASIA);
            Ensure(ProductDbEditor.AllPathsAt(ProductDbEditor.Inspect(returnedCn), "wowf", CN),
                "repeated CN/Asia switching fully redirects duplicate installations");

            // Multiple wow records are not silently skipped either.
            byte[] dualNormal = Database(Entry("wow", CN), Entry("wow", ASIA), Entry("wowf", ASIA));
            Ensure(ProductDbEditor.AllPathsAt(ProductDbEditor.Inspect(
                ProductDbEditor.PatchBoth(dualNormal, CN, "wow", "wowf", CN, ASIA)), "wow", CN),
                "multiple normal-wow records must all match selected root");

            // Unknown installation roots are protected: do not overwrite third-party games.
            byte[] unknown = Database(Entry("wow", CN), Entry("wowf", CN), Entry("wowf", @"E:\Other Install"));
            bool rejectedUnexpected = false;
            try { ProductDbEditor.PatchBoth(unknown, ASIA, "wow", "wowf", CN, ASIA); }
            catch (InvalidDataException) { rejectedUnexpected = true; }
            Ensure(rejectedUnexpected, "unknown third installation root rejected");

            // Custom installation directories are supported after selecting them in UI.
            const string CustomA = @"E:\Games\WoW Custom A";
            const string CustomB = @"F:\Games\WoW Custom B";
            byte[] custom = Database(Entry("wow", CustomA), Entry("wowf", CustomA));
            ProductRecord[] customResults = ProductDbEditor.Inspect(
                ProductDbEditor.PatchBoth(custom, CustomB, "wow", "wowf", CustomA, CustomB));
            Ensure(ProductDbEditor.AllPathsAt(customResults, "wow", CustomB) &&
                ProductDbEditor.AllPathsAt(customResults, "wowf", CustomB),
                "user-configured game folders must be accepted");
            Ensure(ProductDbEditor.FindUntrustedPaths(ProductDbEditor.Inspect(custom),
                "wow", "wowf", CN, ASIA).Length == 1,
                "untrusted history requires explicit consent");
            Ensure(AppSettings.IsSupportedRegion("EU") && AppSettings.IsSupportedRegion("TW") &&
                !AppSettings.IsSupportedRegion("US --wrong"),
                "launch-region argument must be whitelisted");
            AppSettings preferences = AppSettings.CreateDefaults();
            preferences.First.LoginRegion = "EU";
            preferences.Second.LoginRegion = "US";
            preferences.Validate();
            Ensure(preferences.First.LoginRegion == "EU" && preferences.Second.LoginRegion == "US",
                "both server region choices are independent");

            // New V4.1 safety regression: detect live install tasks, but do not
            // confuse unrelated products with active WoW operations.
            byte[] activeHandshake = Database(Entry("wow", CN), Entry("wowf", CN));
            activeHandshake = Concat(activeHandshake,
                Field(2, Concat(TextField(1, "wowf"), Field(3, TextField(1, CN)))));
            Ensure(ProductDbEditor.FindSwitchBlockers(activeHandshake, "wow", "wowf").Length == 1,
                "detect wowf active-install handshake");
            byte[] unrelatedHandshake = Concat(standard,
                Field(2, Concat(TextField(1, "s2"), Field(3, TextField(1, @"F:\Other Game")))));
            Ensure(ProductDbEditor.FindSwitchBlockers(unrelatedHandshake, "wow", "wowf").Length == 0,
                "unrelated active-install handshake must not block WoW");
            Ensure(ProductDbEditor.FindSwitchBlockers(unrelatedHandshake).Length == 1,
                "an active update for any other game must prevent Agent shutdown");
            byte[] runningUpdate = Database(
                Field(1, Concat(Product("wow", CN), Field(5, NumberField(1, 0u)))),
                Entry("wowf", CN));
            Ensure(ProductDbEditor.FindSwitchBlockers(runningUpdate, "wow", "wowf").Length == 1,
                "detect active wow update operation");
            byte[] idleUpdate = Database(
                Field(1, Concat(Product("wow", CN), Field(5, NumberField(1, uint.MaxValue)))),
                Entry("wowf", CN));
            Ensure(ProductDbEditor.FindSwitchBlockers(idleUpdate, "wow", "wowf").Length == 0,
                "OP_NONE must not be treated as an update");

            // Regression (2026-11-01): Asia -> CN was blocked by persisted
            // "agent: updating" despite zero Agent processes and no .new file.
            // The overload is only valid AFTER the caller confirms Agent stopped.
            byte[] staleAgent = Database(
                Entry("wow", ASIA), Entry("wowf", ASIA),
                Field(1, Concat(Product("agent", @"C:\ProgramData\Battle.net\Agent"),
                    Field(5, NumberField(1, 0u)))));
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgent).Length == 1,
                "unconfirmed Agent update remains protected");
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgent, true).Length == 0,
                "dormant Agent self-operation can be ignored when confirmed stopped");
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgent, false).Length == 1,
                "Agent update must still block when Agent is running");
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgent, true, "agent").Length == 0,
                "filtered dormant Agent operation is absent");
            byte[] staleAgentAndWoW = Concat(staleAgent,
                Field(1, Concat(Product("wow", ASIA), Field(5, NumberField(1, 0u)))));
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgentAndWoW, true).Length == 1,
                "real WoW update remains a blocker even if Agent is stopped");
            byte[] staleAgentAndOtherGame = Concat(staleAgent,
                Field(1, Concat(Product("w3", @"F:\Other Game"), Field(5, NumberField(1, 0u)))));
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgentAndOtherGame, true).Length == 1,
                "another game's running update remains a blocker");
            byte[] staleAgentAndHandshake = Concat(staleAgent,
                Field(2, Concat(TextField(1, "agent"), Field(3, TextField(1, CN)))));
            Ensure(ProductDbEditor.FindSwitchBlockers(staleAgentAndHandshake, true).Length == 1,
                "active Agent install handshake is not suppressed by operation exception");
            byte[] mixedAgentOps = Database(
                Field(1, Concat(Product("Agent", @"C:\ProgramData\Battle.net\Agent"),
                    Field(5, NumberField(1, 0u)))),
                Entry("wow", ASIA), Entry("wowf", ASIA));
            Ensure(ProductDbEditor.FindSwitchBlockers(mixedAgentOps, true).Length == 0,
                "agent code match is case insensitive");

            // Regression: never mistake our GUI or bootstrap as a game / Battle.net.
            Ensure(ProcessClassifier.IsOwnApplicationName("WowRegionSwitcher_Managed") &&
                !ProcessClassifier.IsGameProcess("WowRegionSwitcher_Managed", "", CN, ASIA),
                "our own WinForms process must never block a switch");
            Ensure(ProcessClassifier.IsBattleNetName("Battle.net") &&
                ProcessClassifier.IsAgentName("Blizzard Update Agent") &&
                !ProcessClassifier.IsBattleNetName("Wow"),
                "exact process names for graceful client shutdown");

            bool rejectedMissing = false;
            try { ProductDbEditor.PatchBoth(standard, ASIA, "wow", "missing-code", CN, ASIA); }
            catch (InvalidDataException) { rejectedMissing = true; }
            Ensure(rejectedMissing, "missing game rejected");
            bool rejectedMalformed = false;
            try { ProductDbEditor.Inspect(new byte[] { 0x0a, 0xff }); }
            catch (InvalidDataException) { rejectedMalformed = true; }
            Ensure(rejectedMalformed, "malformed database rejected");
        }
    }
}
