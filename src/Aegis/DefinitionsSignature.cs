// The detached signature of aegis\definitions.txt (aegis\Aegis.ps1 class Sig, v0.5.5 lines 270-374; the same rules as
// the mod's src\Net\DefinitionsSignature.cs). definitions.txt.sig next to definitions.txt holds the base64 RSA 3072 /
// SHA-256 / PKCS#1 v1.5 signature over the file's bytes with a leading UTF-8 BOM removed and CRLF turned into LF, and an
// optional "keyid=" line. Only a file that verifies with a trusted public key (never a revoked one) is used or saved.
// Ported line for line: the same parsing, the same size limits, the same RSACryptoServiceProvider calls.
// SPDX-License-Identifier: GPL-3.0-or-later
using System;
using System.Security.Cryptography;

namespace Starpocket.Client.Aegis
{
    /// <summary>A trusted signing key: its id (the first 16 hex digits of the SHA-256 of its SubjectPublicKeyInfo, as the
    /// .sig's keyid= line names it) and its public XML.</summary>
    internal sealed class DefinitionKey
    {
        public readonly string Id, Xml;
        public DefinitionKey(string id, string xml) { Id = id ?? ""; Xml = xml ?? ""; }
    }

    /// <summary>
    /// The keys a definitions file may be signed with, and the ids of revoked keys: copied character for character from
    /// Aegis.ps1 (Sig.TrustedKeys / Sig.RevokedKeyIds, v0.5.5 lines 292-298), which are the same lists as TrustedKeys /
    /// RevokedKeyIds in the mod's src\Net\AegisRules.cs and aegis\AegisBan.ps1. Changed only by a release: a new admin's
    /// key is added; a lost or leaked key is removed and its id added to RevokedIds. (tools\sign-definitions.ps1 -Verify
    /// and build-release.ps1 of the mod's repository read the list from Aegis.ps1 today; they should read this file too
    /// once the Client ships - PORT-MAP 3.10.)
    /// </summary>
    internal static class TrustedKeys
    {
        public static readonly DefinitionKey[] Keys =
        {
            // 2026-09-22, the owner's key, made in the owner's own Windows session (the first key, cedca02cae60f103, was made
            // inside the Claude app, whose AppData Windows keeps private to that app, and is revoked below). SHA-256 of its
            // SubjectPublicKeyInfo: 91400fdf0f5af4ca9d4772e63e7840c2c88cee099ef70147a5583cc99d45fbaf
            new DefinitionKey("91400fdf0f5af4ca", "<RSAKeyValue><Modulus>vy6G7MqjS1pVrs2kPhhIgm8kiHeYpLSegBOTWq2XnpxwJXspVovRsHZqtn9vFwd3Vb/zzJQqoa9uzhKjj5befeJArZXgn5gSwgbSKY2J3MC2gXVgHY/ELfwkgO2qCD6uXDEym4zGTe262sDKOJoD3xT2QHfNAEaxXlRFRnU0WPTL1Gca31TqVdo1Pxj9/ofCvpNsuTS834hMyeTSE/8qV+t6nKGmtCI93/0f4qhpXusWBDOyM04uM0pUTH3eKtku5hOU9H1TKkXbMvCavP2529JMu3lk8T1Y5gVdOllC33sSwL0ehqz3rdy1/t8mlQISwqL4EuS1ph+21oTbY6tjXQCRv7GvkRuxl91ZyyZkgJV4kwWXMCGYo5sIxuUN/RVtf9UB+qX/qYPHA94NKaV2Ee6AnUaEsiYmMykK9YYpJIWEwNNt8XIbYM/rlNlkckTUp3ND1O7XB7D6WP9TeAYDiqt5asXcNTLsciq27xndQSJRPybYTaZOcZ/RmCKKQ7Xp</Modulus><Exponent>AQAB</Exponent></RSAKeyValue>"),
        };

        public static readonly string[] RevokedIds = { "cedca02cae60f103" };

        /// <summary>The full SHA-256 of the SubjectPublicKeyInfo of Keys[0] (the self-test checks the id against it).</summary>
        public const string OwnerKeySpkiSha256 = "91400fdf0f5af4ca9d4772e63e7840c2c88cee099ef70147a5583cc99d45fbaf";
    }

    internal static class DefinitionsSignature
    {
        public const int Ok = 0, Missing = 1, Invalid = 2;
        public const int MaxSigChars = 4096;

        /// <summary>The bytes that are signed: a leading UTF-8 BOM removed, CRLF → LF (a lone CR is kept).</summary>
        public static byte[] Canonical(byte[] data)
        {
            if (data == null) return new byte[0];
            int start = data.Length >= 3 && data[0] == 0xEF && data[1] == 0xBB && data[2] == 0xBF ? 3 : 0;
            var o = new byte[data.Length - start];
            int n = 0;
            for (int i = start; i < data.Length; i++)
            {
                if (data[i] == 0x0D && i + 1 < data.Length && data[i + 1] == 0x0A) continue;
                o[n++] = data[i];
            }
            if (n != o.Length) Array.Resize(ref o, n);
            return o;
        }

        public static int Verify(byte[] canonical, string sigText) => Verify(canonical, sigText, TrustedKeys.Keys, TrustedKeys.RevokedIds);

        /// <summary>True when <paramref name="id"/> is in <paramref name="revoked"/> (any case).</summary>
        public static bool IsRevoked(string id, string[] revoked)
        {
            if (string.IsNullOrEmpty(id) || revoked == null) return false;
            foreach (var r in revoked) if (string.Equals(r, id, StringComparison.OrdinalIgnoreCase)) return true;
            return false;
        }

        /// <summary>
        /// Ok, Missing (no .sig text) or Invalid (malformed, a revoked or untrusted key id, or no usable key matches). A .sig
        /// naming a key checks with that key only; one without a keyid= line checks with each trusted key; revoked keys are
        /// never used, even when listed. Never throws.
        /// </summary>
        public static int Verify(byte[] canonical, string sigText, DefinitionKey[] trusted, string[] revoked)
        {
            if (sigText == null || sigText.Trim().Length == 0) return Missing;
            if (canonical == null || sigText.Length > MaxSigChars) return Invalid;
            byte[] sig = null;
            string id = null;
            foreach (var raw in sigText.Split('\n'))
            {
                string line = raw.Trim().TrimStart((char)0xFEFF);
                if (line.Length == 0 || line[0] == '#') continue;
                if (line.StartsWith("keyid=", StringComparison.OrdinalIgnoreCase)) { id = line.Substring(6).Trim(); continue; }
                if (sig != null) return Invalid;
                try { sig = Convert.FromBase64String(line); }
                catch (FormatException) { return Invalid; }
            }
            if (sig == null || sig.Length == 0) return Invalid;
            if (IsRevoked(id, revoked) || trusted == null) return Invalid;
            foreach (var k in trusted)
            {
                if (k == null || IsRevoked(k.Id, revoked)) continue;
                if (!string.IsNullOrEmpty(id) && !string.Equals(id, k.Id, StringComparison.OrdinalIgnoreCase)) continue;
                if (VerifyWith(canonical, sig, k.Xml)) return Ok;
            }
            return Invalid;
        }

        static bool VerifyWith(byte[] canonical, byte[] sig, string keyXml)
        {
            try
            {
                using (var rsa = new RSACryptoServiceProvider())
                {
                    rsa.FromXmlString(keyXml);
                    if (sig.Length != (rsa.KeySize + 7) / 8) return false;
                    return rsa.VerifyData(canonical, "SHA256", sig);
                }
            }
            catch (CryptographicException) { return false; }
            catch (FormatException) { return false; }
            catch (ArgumentException) { return false; }
        }
    }
}
