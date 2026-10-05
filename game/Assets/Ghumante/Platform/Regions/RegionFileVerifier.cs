using System;
using System.IO;
using System.Security.Cryptography;
using System.Text;
using System.Threading;

namespace Ghumante.Platform.Regions
{
    /// <summary>
    /// Size and SHA-256 checks of region files against their manifest entry (ARCHITECTURE.md section 9: packs are
    /// verified after every copy or download). Thread-safe and engine-free, so hashing runs on a worker thread.
    /// </summary>
    public static class RegionFileVerifier
    {
        private const int BufferSize = 1 << 16;

        /// <summary>Lowercase hexadecimal SHA-256 of the rest of <paramref name="stream"/>.</summary>
        public static string Sha256Hex(Stream stream, CancellationToken cancellationToken = default(CancellationToken))
        {
            if (stream == null) throw new ArgumentNullException(nameof(stream));
            using (SHA256 sha = SHA256.Create())
            {
                var buffer = new byte[BufferSize];
                int n;
                while ((n = stream.Read(buffer, 0, buffer.Length)) > 0)
                {
                    cancellationToken.ThrowIfCancellationRequested();
                    sha.TransformBlock(buffer, 0, n, null, 0);
                }
                sha.TransformFinalBlock(buffer, 0, 0);
                return Hex(sha.Hash);
            }
        }

        /// <summary>Lowercase hexadecimal SHA-256 of a file.</summary>
        public static string Sha256Hex(string path, CancellationToken cancellationToken = default(CancellationToken))
        {
            using (var fs = new FileStream(path, FileMode.Open, FileAccess.Read, FileShare.Read, BufferSize, FileOptions.SequentialScan))
            {
                return Sha256Hex(fs, cancellationToken);
            }
        }

        /// <summary>
        /// True when the file at <paramref name="path"/> exists with the entry's size and, when
        /// <paramref name="checkHash"/>, its SHA-256. <paramref name="problem"/> says what is wrong otherwise.
        /// </summary>
        public static bool Check(string path, RegionFileEntry entry, bool checkHash, out string problem,
                                 CancellationToken cancellationToken = default(CancellationToken))
        {
            if (entry == null) throw new ArgumentNullException(nameof(entry));
            var info = new FileInfo(path);
            if (!info.Exists)
            {
                problem = entry.Path + " is missing";
                return false;
            }
            if (info.Length != entry.Bytes)
            {
                problem = entry.Path + " has " + info.Length + " bytes, the manifest says " + entry.Bytes;
                return false;
            }
            if (checkHash)
            {
                string sha = Sha256Hex(path, cancellationToken);
                if (!string.Equals(sha, entry.Sha256, StringComparison.Ordinal))
                {
                    problem = entry.Path + " SHA-256 " + sha + " does not match the manifest (" + entry.Sha256 + ")";
                    return false;
                }
            }
            problem = null;
            return true;
        }

        private static string Hex(byte[] bytes)
        {
            var sb = new StringBuilder(bytes.Length * 2);
            for (int i = 0; i < bytes.Length; i++) sb.Append(bytes[i].ToString("x2", System.Globalization.CultureInfo.InvariantCulture));
            return sb.ToString();
        }
    }
}
