using System;
using System.Collections.Generic;
using Titan.Core;
using UnityEngine;

namespace Titan.Game
{
    /// <summary>
    /// The Unity side of the profile store (RET-1): JsonUtility for the text, PlayerPrefs for the mirror,
    /// Application.persistentDataPath for the file. The load order and the crash-safe save live in
    /// <see cref="Titan.Core.ProfileStore"/> (unit tested outside Unity).
    /// </summary>
    public static class UnityProfileStore
    {
        public static Titan.Core.ProfileStore Open() => new Titan.Core.ProfileStore(Application.persistentDataPath, new JsonUtilitySerializer(), new PlayerPrefsStore());

        /// <summary>The log ring as JSON (debug export: F9 in the editor, or a 3-finger tap on the score).</summary>
        public static string LogJson(Profile p) => JsonUtility.ToJson(new LogExport { version = p.version, log = p.log }, true);

        [Serializable]
        sealed class LogExport
        {
            public int version;
            public List<LogEntry> log;
        }
    }

    public sealed class JsonUtilitySerializer : ISerializer
    {
        public string Write(Profile p) => JsonUtility.ToJson(p);
        public Profile Read(string text) => JsonUtility.FromJson<Profile>(text);
    }

    public sealed class PlayerPrefsStore : IPrefs
    {
        public bool HasKey(string key) => PlayerPrefs.HasKey(key);
        public string GetString(string key) => PlayerPrefs.GetString(key, null);
        public int GetInt(string key, int fallback) => PlayerPrefs.GetInt(key, fallback);
        public void SetString(string key, string value) => PlayerPrefs.SetString(key, value);
        public void SetInt(string key, int value) => PlayerPrefs.SetInt(key, value);
        public void Flush() => PlayerPrefs.Save();
    }
}
