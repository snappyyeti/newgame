using System;
using System.IO;

namespace Titan.Core
{
    /// <summary>Turns a profile into text and back (Unity: JsonUtility; tests: any JSON library).</summary>
    public interface ISerializer
    {
        string Write(Profile p);
        /// <summary>Returns null or throws when the text is not a profile.</summary>
        Profile Read(string text);
    }

    /// <summary>The small key-value store next to the file (Unity: PlayerPrefs).</summary>
    public interface IPrefs
    {
        bool HasKey(string key);
        string GetString(string key);
        int GetInt(string key, int fallback);
        void SetString(string key, string value);
        void SetInt(string key, int value);
        /// <summary>Flushes to disk (PlayerPrefs.Save).</summary>
        void Flush();
    }

    /// <summary>
    /// Loads and saves the profile safely (RET-1, DESIGN.md 9.1). Pure C# with System.IO, so the crash-safety rules
    /// are unit tested outside Unity.
    ///
    /// Load order: the file, then the PlayerPrefs mirror, then a new profile. A source that does not parse is skipped
    /// and logged as "save_corrupt". A new profile migrates the old best ("titan_best", shown only, never compared)
    /// and the old "Efecte reduse" PlayerPrefs setting.
    ///
    /// Save: write the whole profile to file.tmp, File.Copy it over the file, delete the .tmp, mirror the same text to
    /// PlayerPrefs and flush. A crash while writing the .tmp leaves the file as it was; a crash during the copy can
    /// leave a torn file, and then the PlayerPrefs mirror of the previous save loads instead.
    /// </summary>
    public sealed class ProfileStore
    {
        public const string FileName = "titan_profile.json";
        public const string PrefsKey = "titan_profile_v1";
        /// <summary>The best score of the old scoring (before CORE-5). Read once for display, never written.</summary>
        public const string LegacyBestKey = "titan_best";
        /// <summary>The v2 best, also mirrored as a plain int (CORE-5 names this key).</summary>
        public const string BestKey = "titan_best_v2";
        /// <summary>The CORE-4 "Efecte reduse" PlayerPrefs setting, migrated into the profile once.</summary>
        public const string LegacyReducedFxKey = "set_reducedFx";

        readonly string path;
        readonly ISerializer serializer;
        readonly IPrefs prefs;

        public ProfileStore(string directory, ISerializer serializer, IPrefs prefs)
        {
            path = Path.Combine(directory, FileName);
            this.serializer = serializer;
            this.prefs = prefs;
        }

        public string FilePath => path;
        public string TempPath => path + ".tmp";

        /// <summary>Where the last <see cref="Load"/> found the profile: 0 file, 1 PlayerPrefs, 2 new.</summary>
        public int LoadedFrom { get; private set; } = -1;

        /// <summary>Loads the profile. <paramref name="corruptSources"/> gets "save_corrupt" sources (0 file, 1 PlayerPrefs) to log.</summary>
        public Profile Load(Action<int> corruptSources = null)
        {
            if (File.Exists(path))
            {
                var p = TryRead(SafeReadFile(path));
                if (p != null)
                {
                    LoadedFrom = 0;
                    return p;
                }
                corruptSources?.Invoke(0);
            }
            if (prefs.HasKey(PrefsKey))
            {
                var p = TryRead(prefs.GetString(PrefsKey));
                if (p != null)
                {
                    LoadedFrom = 1;
                    return p;
                }
                corruptSources?.Invoke(1);
            }
            LoadedFrom = 2;
            return NewProfile();
        }

        /// <summary>A fresh profile that keeps what the old PlayerPrefs knew (legacy best, reduced effects).</summary>
        public Profile NewProfile()
        {
            var p = new Profile();
            p.legacyBest = Math.Max(0, prefs.GetInt(LegacyBestKey, 0));
            p.settings.reducedFx = prefs.GetInt(LegacyReducedFxKey, 0) == 1;
            return p;
        }

        Profile TryRead(string text)
        {
            if (string.IsNullOrWhiteSpace(text)) return null;
            try
            {
                var p = serializer.Read(text);
                if (p == null || p.version < 1) return null;
                p.Normalize();
                return p;
            }
            catch (Exception)
            {
                return null;
            }
        }

        static string SafeReadFile(string file)
        {
            try
            {
                return File.ReadAllText(file);
            }
            catch (IOException)
            {
                return null;
            }
            catch (UnauthorizedAccessException)
            {
                return null;
            }
        }

        /// <summary>Saves through the temp file, then mirrors to PlayerPrefs. Returns false if the file part failed (the mirror is still written).</summary>
        public bool Save(Profile p)
        {
            string text = serializer.Write(p);
            bool fileOk = true;
            try
            {
                Directory.CreateDirectory(Path.GetDirectoryName(path));
                File.WriteAllText(TempPath, text);
                File.Copy(TempPath, path, true);
                File.Delete(TempPath);
            }
            catch (IOException)
            {
                fileOk = false;
            }
            catch (UnauthorizedAccessException)
            {
                fileOk = false;
            }
            prefs.SetString(PrefsKey, text);
            prefs.SetInt(BestKey, p.bestScore);
            prefs.Flush();
            return fileOk;
        }
    }
}
