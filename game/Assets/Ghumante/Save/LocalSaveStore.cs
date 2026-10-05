using System;
using System.IO;
using System.Text;
using Ghumante.Core.Save;
using UnityEngine;

namespace Ghumante.Save
{
    /// <summary>Where <see cref="LocalSaveStore.Load"/> found the save.</summary>
    public enum SaveSource
    {
        /// <summary>No save yet (first launch): defaults.</summary>
        None = 0,

        /// <summary>The save file.</summary>
        Primary = 1,

        /// <summary>The save file was unreadable; the newer backup was used.</summary>
        Backup = 2,

        /// <summary>The save file and the newer backup were unreadable; the older backup was used.</summary>
        OlderBackup = 3,

        /// <summary>Nothing readable: defaults (the unreadable files are left in place).</summary>
        Unreadable = 4,

        /// <summary>Written by a newer build: defaults, and <see cref="LocalSaveStore.Save"/> refuses to overwrite it.</summary>
        NewerVersion = 5,
    }

    /// <summary>
    /// The local save (ARCHITECTURE.md 7.10): <see cref="SaveData"/> as UTF-8 JSON under
    /// <c>Application.persistentDataPath</c>. Writes are atomic: the document goes to a temp file, which is
    /// flushed to disk and then renamed over the save (<see cref="File.Replace(string,string,string,bool)"/>),
    /// so a crash or a full disk mid-write never leaves a half-written save. The previous two saves are kept as
    /// backups and are read when the save itself is unreadable. Compression arrives with the larger M1 saves.
    /// Main thread; a save is a few KB, written only when something changed.
    /// </summary>
    public sealed class LocalSaveStore
    {
        public const string DefaultFileName = "ghumante-save.json";

        private static readonly UTF8Encoding Utf8NoBom = new UTF8Encoding(false);

        public LocalSaveStore(string directory, string fileName = DefaultFileName)
        {
            if (string.IsNullOrEmpty(directory)) throw new ArgumentException("directory is required", nameof(directory));
            if (string.IsNullOrEmpty(fileName)) throw new ArgumentException("file name is required", nameof(fileName));
            Directory = directory;
            FilePath = Path.Combine(directory, fileName);
            BackupPath = FilePath + ".bak";
            OlderBackupPath = FilePath + ".bak2";
            TempPath = FilePath + ".tmp";
        }

        /// <summary>The store the game uses: <c>Application.persistentDataPath/ghumante-save.json</c>.</summary>
        public static LocalSaveStore CreateDefault()
        {
            return new LocalSaveStore(Application.persistentDataPath);
        }

        public string Directory { get; private set; }
        public string FilePath { get; private set; }
        public string BackupPath { get; private set; }
        public string OlderBackupPath { get; private set; }
        public string TempPath { get; private set; }

        /// <summary>Where the last <see cref="Load"/> came from.</summary>
        public SaveSource LastSource { get; private set; }

        /// <summary>
        /// True after loading a save written by a newer build: <see cref="Save"/> then refuses, so playing an
        /// older build (a rollback, a second device) never destroys the newer progress.
        /// </summary>
        public bool ReadOnly { get; private set; }

        /// <summary>
        /// Loads the save, falling back to the backups, then to defaults. Never throws for a missing or
        /// corrupt file; see <see cref="LastSource"/>.
        /// </summary>
        public SaveData Load()
        {
            ReadOnly = false;
            bool any = false;
            SaveData data;
            string[] paths = { FilePath, BackupPath, OlderBackupPath };
            SaveSource[] sources = { SaveSource.Primary, SaveSource.Backup, SaveSource.OlderBackup };
            for (int i = 0; i < paths.Length; i++)
            {
                if (!File.Exists(paths[i])) continue;
                any = true;
                try
                {
                    data = SaveSerializer.FromJson(File.ReadAllText(paths[i], Encoding.UTF8));
                    LastSource = sources[i];
                    if (i > 0) Debug.LogWarning("LocalSaveStore: " + FilePath + " was unreadable; loaded " + paths[i] + ".");
                    return data;
                }
                catch (SaveVersionException e)
                {
                    Debug.LogWarning("LocalSaveStore: " + e.Message + "; using defaults and leaving the save untouched.");
                    ReadOnly = true;
                    LastSource = SaveSource.NewerVersion;
                    return new SaveData();
                }
                catch (Exception e) when (e is IOException || e is FormatException || e is UnauthorizedAccessException ||
                                          e is InvalidOperationException)
                {
                    Debug.LogWarning("LocalSaveStore: cannot read " + paths[i] + ": " + e.Message);
                }
            }
            LastSource = any ? SaveSource.Unreadable : SaveSource.None;
            return new SaveData();
        }

        /// <summary>
        /// Writes <paramref name="data"/> atomically and rotates the backups. Returns false (and logs) when
        /// the write failed or the store is <see cref="ReadOnly"/>; the previous save is then intact.
        /// </summary>
        public bool Save(SaveData data)
        {
            if (data == null) throw new ArgumentNullException(nameof(data));
            if (ReadOnly)
            {
                Debug.LogWarning("LocalSaveStore: not overwriting a save from a newer build.");
                return false;
            }
            try
            {
                System.IO.Directory.CreateDirectory(Directory);
                byte[] bytes = Utf8NoBom.GetBytes(SaveSerializer.ToJson(data));
                using (var stream = new FileStream(TempPath, FileMode.Create, FileAccess.Write, FileShare.None))
                {
                    stream.Write(bytes, 0, bytes.Length);
                    stream.Flush(true);  // on disk before the rename makes it the save
                }
                if (File.Exists(FilePath))
                {
                    if (File.Exists(BackupPath)) File.Copy(BackupPath, OlderBackupPath, true);
                    try
                    {
                        File.Replace(TempPath, FilePath, BackupPath, true);
                    }
                    catch (PlatformNotSupportedException)
                    {
                        ReplaceByCopy();
                    }
                }
                else
                {
                    File.Move(TempPath, FilePath);
                }
                return true;
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                Debug.LogWarning("LocalSaveStore: could not write " + FilePath + ": " + e.Message);
                TryDelete(TempPath);
                return false;
            }
        }

        /// <summary>Fallback where File.Replace is unavailable: keep the old save as the backup, then move.</summary>
        private void ReplaceByCopy()
        {
            File.Copy(FilePath, BackupPath, true);
            File.Delete(FilePath);
            File.Move(TempPath, FilePath);
        }

        private static void TryDelete(string path)
        {
            try
            {
                if (File.Exists(path)) File.Delete(path);
            }
            catch (Exception e) when (e is IOException || e is UnauthorizedAccessException)
            {
                // Leave it; the next save overwrites it.
            }
        }
    }
}
