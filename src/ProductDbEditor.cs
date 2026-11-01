// .NET Framework 4.x-compatible, narrow protobuf editor for Blizzard Agent product.db.
// All fields except the requested ProductInstall.settings.install_path are retained byte-for-byte.
// No game or account files are modified.
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text;

namespace WowRegionSwitcher
{
    internal sealed class ProductRecord
    {
        public string Code;
        public string InstallPath;
        public string PlayRegion;
    }

    internal static class ProductDbEditor
    {
        private sealed class Field
        {
            public int Number;
            public int Wire;
            public byte[] Data;
            public byte[] Original;
        }

        private static readonly UTF8Encoding StrictUtf8 = new UTF8Encoding(false, true);
        private const int MaxDatabaseBytes = 64 * 1024 * 1024;

        private static byte[] CopyRange(byte[] bytes, int offset, int size)
        {
            if (offset < 0 || size < 0 || offset > bytes.Length || size > bytes.Length - offset)
                throw new InvalidDataException("Malformed product.db: field exceeds file length.");
            byte[] result = new byte[size];
            Buffer.BlockCopy(bytes, offset, result, 0, size);
            return result;
        }

        private static ulong ReadVarint(byte[] bytes, ref int pos)
        {
            ulong result = 0;
            for (int i = 0; i < 10; i++)
            {
                if (pos >= bytes.Length)
                    throw new InvalidDataException("Malformed product.db: truncated varint.");
                byte value = bytes[pos++];
                if (i == 9 && value > 1)
                    throw new InvalidDataException("Malformed product.db: invalid varint.");
                result |= ((ulong)(value & 0x7F)) << (i * 7);
                if ((value & 0x80) == 0)
                    return result;
            }
            throw new InvalidDataException("Malformed product.db: varint is too long.");
        }

        private static void WriteVarint(Stream s, ulong val)
        {
            while (val >= 128)
            {
                s.WriteByte((byte)((val & 127UL) | 128UL));
                val >>= 7;
            }
            s.WriteByte((byte)val);
        }

        private static List<Field> ReadFields(byte[] bytes)
        {
            List<Field> list = new List<Field>();
            int pos = 0;
            while (pos < bytes.Length)
            {
                int start = pos;
                ulong key = ReadVarint(bytes, ref pos);
                ulong number = key >> 3;
                int wire = (int)(key & 7);
                if (number == 0 || number > 536870911UL)
                    throw new InvalidDataException("Malformed product.db: invalid protobuf field number.");
                byte[] data = null;
                switch (wire)
                {
                    case 0:
                        ReadVarint(bytes, ref pos);
                        break;
                    case 1:
                        if (bytes.Length - pos < 8) throw new InvalidDataException("Malformed fixed64 field.");
                        pos += 8;
                        break;
                    case 2:
                        ulong length = ReadVarint(bytes, ref pos);
                        if (length > (ulong)(bytes.Length - pos))
                            throw new InvalidDataException("Malformed product.db: invalid field length.");
                        data = CopyRange(bytes, pos, (int)length);
                        pos += (int)length;
                        break;
                    case 5:
                        if (bytes.Length - pos < 4) throw new InvalidDataException("Malformed fixed32 field.");
                        pos += 4;
                        break;
                    default:
                        // Groups (wire 3/4) are not part of the documented ProductDb layout.
                        // If encountered, fail closed; never attempt a speculative edit.
                        throw new InvalidDataException("Unsupported protobuf wire type: " + wire);
                }
                list.Add(new Field {
                    Number = (int)number,
                    Wire = wire,
                    Data = data,
                    Original = CopyRange(bytes, start, pos - start)
                });
            }
            return list;
        }

        private static byte[] MakeLengthDelimited(int number, byte[] data)
        {
            using (MemoryStream stream = new MemoryStream())
            {
                WriteVarint(stream, ((ulong)number << 3) | 2UL);
                WriteVarint(stream, (ulong)data.Length);
                stream.Write(data, 0, data.Length);
                return stream.ToArray();
            }
        }

        private static string ReadString(byte[] bytes)
        {
            return StrictUtf8.GetString(bytes);
        }

        private static ProductRecord ReadProduct(byte[] bytes)
        {
            string code = string.Empty;
            string path = string.Empty;
            string playRegion = string.Empty;
            foreach (Field f in ReadFields(bytes))
            {
                if (f.Number == 2 && f.Wire == 2)
                    code = ReadString(f.Data);
                if (f.Number == 3 && f.Wire == 2)
                {
                    foreach (Field nested in ReadFields(f.Data))
                    {
                        if (nested.Number == 1 && nested.Wire == 2)
                            path = ReadString(nested.Data);
                        if (nested.Number == 2 && nested.Wire == 2)
                            playRegion = ReadString(nested.Data);
                    }
                }
            }
            return new ProductRecord { Code = code, InstallPath = path, PlayRegion = playRegion };
        }

        public static ProductRecord[] Inspect(byte[] db)
        {
            if (db == null || db.Length == 0 || db.Length > MaxDatabaseBytes)
                throw new InvalidDataException("Battle.net product.db is missing, empty, or unexpectedly large.");
            List<ProductRecord> result = new List<ProductRecord>();
            foreach (Field f in ReadFields(db))
                if (f.Number == 1 && f.Wire == 2)
                    result.Add(ReadProduct(f.Data));
            if (result.Count == 0)
                throw new InvalidDataException("No product records found in Battle.net product.db.");
            return result.ToArray();
        }

        /// <summary>
        /// Examines *other* protobuf fields that V4 never checked:
        /// top-level field 2 = active installation handshakes;
        /// ProductInstall field 5 = ProductOperations.
        /// We do not rewrite those fields: changing a running update/install session
        /// could corrupt the game's update transaction. Fail safely instead.
        /// </summary>
        public static string[] FindSwitchBlockers(byte[] db, params string[] targetCodes)
        {
            if (db == null || db.Length == 0 || db.Length > MaxDatabaseBytes)
                throw new InvalidDataException("Invalid product.db length.");
            List<string> result = new List<string>();
            foreach (Field top in ReadFields(db))
            {
                if (top.Number == 2 && top.Wire == 2)
                {
                    // InstallHandshake: string product=1, string uid=2, settings=3.
                    string product = "";
                    string installPath = "";
                    foreach (Field entry in ReadFields(top.Data))
                    {
                        if (entry.Number == 1 && entry.Wire == 2)
                            product = ReadString(entry.Data);
                        if (entry.Number == 3 && entry.Wire == 2)
                            foreach (Field setting in ReadFields(entry.Data))
                                if (setting.Number == 1 && setting.Wire == 2)
                                    installPath = ReadString(setting.Data);
                    }
                    if (IsInList(product, targetCodes))
                        result.Add("正在安装/定位：" + product +
                            (installPath == "" ? "" : "，目录：" + installPath));
                }
                if (top.Number == 1 && top.Wire == 2)
                {
                    ProductRecord product = ReadProduct(top.Data);
                    if (!IsInList(product.Code, targetCodes)) continue;
                    foreach (Field entry in ReadFields(top.Data))
                    {
                        if (entry.Number != 5 || entry.Wire != 2) continue;
                        // ProductOperations.active_operation = field 1, enum.
                        foreach (Field operation in ReadFields(entry.Data))
                        {
                            if (operation.Number != 1 || operation.Wire != 0) continue;
                            int position = 0;
                            ReadVarint(operation.Original, ref position); // field tag
                            ulong raw = ReadVarint(operation.Original, ref position);
                            int op = unchecked((int)raw);
                            if (op == 0 || op == 1 || op == 2)
                            {
                                string kind = op == 0 ? "更新中" : op == 1 ? "后台下载中" : "修复中";
                                result.Add(product.Code + "：" + kind + "（Agent 任务）");
                            }
                        }
                    }
                }
            }
            return result.Distinct().ToArray();
        }

        private static bool IsInList(string code, string[] codes)
        {
            return !string.IsNullOrEmpty(code) &&
                (codes == null || codes.Length == 0 || codes.Any(c =>
                    string.Equals(code, c, StringComparison.OrdinalIgnoreCase)));
        }

        private static byte[] RewriteSettings(byte[] settings, string target)
        {
            bool hasInstallPath = false;
            using (MemoryStream stream = new MemoryStream())
            {
                foreach (Field f in ReadFields(settings))
                {
                    byte[] encoded = f.Original;
                    if (f.Number == 1 && f.Wire == 2)
                    {
                        encoded = MakeLengthDelimited(1, StrictUtf8.GetBytes(target));
                        hasInstallPath = true;
                    }
                    stream.Write(encoded, 0, encoded.Length);
                }
                if (!hasInstallPath)
                    throw new InvalidDataException("Target product record has no install_path field. Refusing to fabricate one.");
                return stream.ToArray();
            }
        }

        private static byte[] RewriteProduct(byte[] product, string target)
        {
            bool hasSettings = false;
            using (MemoryStream stream = new MemoryStream())
            {
                foreach (Field f in ReadFields(product))
                {
                    byte[] encoded = f.Original;
                    if (f.Number == 3 && f.Wire == 2)
                    {
                        encoded = MakeLengthDelimited(3, RewriteSettings(f.Data, target));
                        hasSettings = true;
                    }
                    stream.Write(encoded, 0, encoded.Length);
                }
                if (!hasSettings)
                    throw new InvalidDataException("Target product record has no settings field.");
                return stream.ToArray();
            }
        }

        // CN and Asia may both be registered as the same product code (e.g. wowf).
        // The Battle.net UI can use another record even when the desired path exists.
        // Therefore every record of the two selected product codes MUST point to
        // the selected installation while that region is active. Other products,
        // non-install-path fields, product ordering, and duplicate IDs remain intact.
        public static bool SameFolder(string left, string right)
        {
            if (string.IsNullOrWhiteSpace(left) || string.IsNullOrWhiteSpace(right)) return false;
            string a = left.Trim().Replace('\\', '/').TrimEnd('/');
            string b = right.Trim().Replace('\\', '/').TrimEnd('/');
            return string.Equals(a, b, StringComparison.OrdinalIgnoreCase);
        }

        public static bool HasPath(ProductRecord[] records, string code, string target)
        {
            return records.Any(p =>
                string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase) &&
                SameFolder(p.InstallPath, target));
        }

        public static bool AllPathsAt(ProductRecord[] records, string code, string target)
        {
            ProductRecord[] matching = records.Where(p =>
                string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)).ToArray();
            return matching.Length > 0 && matching.All(p => SameFolder(p.InstallPath, target));
        }

        private static bool IsRequestedCode(string code, string normalCode, string foreverCode)
        {
            return string.Equals(code, normalCode, StringComparison.OrdinalIgnoreCase) ||
                string.Equals(code, foreverCode, StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExpectedRoot(string path, IEnumerable<string> trustedRoots)
        {
            return trustedRoots != null && trustedRoots.Any(root => SameFolder(path, root));
        }

        // Caller displays an explicit consent dialog for paths not covered by either
        // configured profile. Never silently expand the trusted-root allowlist.
        public static string[] FindUntrustedPaths(ProductRecord[] records, string codeA, string codeB,
            params string[] trustedRoots)
        {
            return records.Where(p => IsRequestedCode(p.Code, codeA, codeB) &&
                    !IsExpectedRoot(p.InstallPath, trustedRoots))
                .Select(p => p.InstallPath).Distinct(StringComparer.OrdinalIgnoreCase).ToArray();
        }

        private static string RespectPathSeparators(string oldPath, string target)
        {
            return oldPath.Contains("/") && !oldPath.Contains("\\")
                ? target.Replace('\\', '/') : target;
        }

        public static byte[] PatchBoth(byte[] source, string target, string normalCode, string foreverCode,
            params string[] trustedRoots)
        {
            if (string.IsNullOrWhiteSpace(target) ||
                string.IsNullOrWhiteSpace(normalCode) || string.IsNullOrWhiteSpace(foreverCode) ||
                string.Equals(normalCode, foreverCode, StringComparison.OrdinalIgnoreCase))
                throw new ArgumentException("Two distinct product codes and a target folder are required.");

            // Only explicitly configured/confirmed roots can be changed. No hard-coded D: restriction.
            if (trustedRoots == null || trustedRoots.Length < 2 ||
                !IsExpectedRoot(target, trustedRoots))
                throw new ArgumentException("Target directory is not one of the explicitly trusted game roots.");

            ProductRecord[] before = Inspect(source);
            foreach (string code in new string[] { normalCode, foreverCode })
            {
                ProductRecord[] matches = before.Where(p =>
                    string.Equals(p.Code, code, StringComparison.OrdinalIgnoreCase)).ToArray();
                if (matches.Length == 0)
                    throw new InvalidDataException("Missing product code " + code + ". Nothing changed.");
                foreach (ProductRecord record in matches)
                {
                    if (!IsExpectedRoot(record.InstallPath, trustedRoots))
                        throw new InvalidDataException("Product " + code +
                            " has an unexpected installation folder: " + record.InstallPath +
                            ". Refusing to overwrite an unknown installation. Nothing changed.");
                }
            }

            byte[] result;
            using (MemoryStream stream = new MemoryStream())
            {
                foreach (Field f in ReadFields(source))
                {
                    byte[] encoded = f.Original;
                    if (f.Number == 1 && f.Wire == 2)
                    {
                        ProductRecord product = ReadProduct(f.Data);
                        if (IsRequestedCode(product.Code, normalCode, foreverCode) &&
                            !SameFolder(product.InstallPath, target))
                        {
                            string formatted = RespectPathSeparators(product.InstallPath, target);
                            encoded = MakeLengthDelimited(1, RewriteProduct(f.Data, formatted));
                        }
                    }
                    stream.Write(encoded, 0, encoded.Length);
                }
                result = stream.ToArray();
            }

            ProductRecord[] after = Inspect(result);
            if (after.Length != before.Length ||
                !AllPathsAt(after, normalCode, target) ||
                !AllPathsAt(after, foreverCode, target))
                throw new InvalidDataException("Validation failed: not every requested record matches the selected region.");

            // Strictly validate changes: each serialized product must retain its code,
            // order and all unknown/other fields byte-for-byte. For modified products
            // only the nested settings.install_path field may change.
            List<Field> oldTop = ReadFields(source);
            List<Field> newTop = ReadFields(result);
            if (oldTop.Count != newTop.Count)
                throw new InvalidDataException("Validation failed: top-level field count changed.");
            for (int i = 0; i < oldTop.Count; i++)
            {
                Field oldField = oldTop[i];
                Field newField = newTop[i];
                if (oldField.Number != newField.Number || oldField.Wire != newField.Wire)
                    throw new InvalidDataException("Validation failed: top-level field metadata changed.");
                if (oldField.Number != 1 || oldField.Wire != 2)
                {
                    if (!oldField.Original.SequenceEqual(newField.Original))
                        throw new InvalidDataException("Validation failed: unrelated database content changed.");
                    continue;
                }
                ProductRecord oldProduct = ReadProduct(oldField.Data);
                ProductRecord newProduct = ReadProduct(newField.Data);
                if (oldProduct.Code != newProduct.Code)
                    throw new InvalidDataException("Validation failed: product code or ordering changed.");
                if (!IsRequestedCode(oldProduct.Code, normalCode, foreverCode) ||
                    SameFolder(oldProduct.InstallPath, target))
                {
                    if (!oldField.Original.SequenceEqual(newField.Original))
                        throw new InvalidDataException("Validation failed: unrelated product was modified.");
                }
                else
                {
                    // Rebuild from original record with only the allowed field changed;
                    // byte-wise equality also catches changes to unknown protobuf fields.
                    byte[] allowed = MakeLengthDelimited(1,
                        RewriteProduct(oldField.Data, RespectPathSeparators(oldProduct.InstallPath, target)));
                    if (!allowed.SequenceEqual(newField.Original))
                        throw new InvalidDataException("Validation failed: fields other than install_path were modified.");
                }
            }
            return result;
        }
    }
}
