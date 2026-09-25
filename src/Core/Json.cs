// Small JSON helpers over System.Web.Script.Serialization (part of .NET Framework; no extra NuGet package).
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using System.Web.Script.Serialization;

namespace Starpocket.Client.Core
{
    internal static class Json
    {
        static JavaScriptSerializer NewSerializer() => new JavaScriptSerializer { MaxJsonLength = 8 * 1024 * 1024, RecursionLimit = 64 };

        /// <summary>Parses JSON text; throws on malformed text.</summary>
        public static object Parse(string text) => NewSerializer().DeserializeObject(text);

        /// <summary>Parses JSON text into an object (dictionary); null for anything else or malformed text.</summary>
        public static Dictionary<string, object> TryParseObject(string text)
        {
            try { return Parse(text) as Dictionary<string, object>; }
            catch (Exception) { return null; }
        }

        public static string Serialize(object value) => NewSerializer().Serialize(value);

        /// <summary>A string member (null when missing or not a string).</summary>
        public static string Str(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj == null || !obj.TryGetValue(key, out v)) return null;
            return v as string;
        }

        /// <summary>A true member: JSON true, or the strings "true"/"1" (a page that puts its flags in strings).</summary>
        public static bool Bool(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj == null || !obj.TryGetValue(key, out v) || v == null) return false;
            if (v is bool b) return b;
            string s = v as string;
            return s == "true" || s == "1";
        }

        public static Dictionary<string, object> Obj(Dictionary<string, object> obj, string key)
        {
            object v;
            if (obj == null || !obj.TryGetValue(key, out v)) return null;
            return v as Dictionary<string, object>;
        }

        /// <summary>Settings files are small; anything bigger is not one of ours and is not read into memory.</summary>
        public const long MaxFileBytes = 4 * 1024 * 1024;

        /// <summary>Reads a UTF-8 text file with or without a BOM (PowerShell's Set-Content -Encoding UTF8 writes one).
        /// Throws when the file is larger than <see cref="MaxFileBytes"/> (EvidenceStore caps every file it reads the
        /// same way; this one used to read whatever was there).</summary>
        public static string ReadUtf8File(string path)
        {
            var fi = new FileInfo(path);
            if (fi.Exists && fi.Length > MaxFileBytes)
                throw new IOException(fi.Name + " is larger than " + (MaxFileBytes / (1024 * 1024)) + " MB");
            byte[] b = File.ReadAllBytes(path);
            int start = (b.Length >= 3 && b[0] == 0xEF && b[1] == 0xBB && b[2] == 0xBF) ? 3 : 0;
            return new UTF8Encoding(false, false).GetString(b, start, b.Length - start);
        }
    }
}
