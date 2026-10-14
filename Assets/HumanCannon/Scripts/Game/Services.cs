using System;
using System.Collections;
using System.Text;
using HumanCannon.Core;
using UnityEngine;

namespace HumanCannon.Game
{
    /// <summary>Saves the player profile locally (record, run history) with PlayerPrefs.</summary>
    public static class ProfileStore
    {
        const string Key = "hc_profile_v1";

        public static PlayerProfile Load()
        {
            PlayerProfile profile = null;
            var json = PlayerPrefs.GetString(Key, "");
            if (!string.IsNullOrEmpty(json))
            {
                try { profile = JsonUtility.FromJson<PlayerProfile>(json); }
                catch (ArgumentException) { profile = null; }
            }
            if (profile == null) profile = new PlayerProfile();
            if (profile.History == null) profile.History = new System.Collections.Generic.List<RunRecord>();
            if (profile.FirstLaunchUnixSeconds == 0)
            {
                profile.FirstLaunchUnixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
                Save(profile);
            }
            return profile;
        }

        public static void Save(PlayerProfile profile)
        {
            PlayerPrefs.SetString(Key, JsonUtility.ToJson(profile));
            PlayerPrefs.Save();
        }

        public static double AccountAgeHours(PlayerProfile profile) =>
            (DateTimeOffset.UtcNow.ToUnixTimeSeconds() - profile.FirstLaunchUnixSeconds) / 3600.0;
    }

    public static class Settings
    {
        public static bool Vibration
        {
            get => PlayerPrefs.GetInt("hc_vibration", 1) == 1;
            set { PlayerPrefs.SetInt("hc_vibration", value ? 1 : 0); PlayerPrefs.Save(); }
        }
    }

    /// <summary>
    /// Light / medium / heavy haptics. Unity only exposes one strong vibration, so light and medium are no-ops
    /// until a native haptics plugin is added (MVP step 2).
    /// </summary>
    public static class Haptics
    {
        public static void Light() { }
        public static void Medium() { }

        public static void Heavy()
        {
#if UNITY_ANDROID || UNITY_IOS
            if (Settings.Vibration) Handheld.Vibrate();
#endif
        }
    }

    /// <summary>Analytics events from the design doc. Logs to the console until an analytics SDK is wired in.</summary>
    public static class Analytics
    {
        public static void Log(string name, params object[] keyValues)
        {
            var sb = new StringBuilder("[analytics] ").Append(name);
            for (int i = 0; i + 1 < keyValues.Length; i += 2)
                sb.Append(' ').Append(keyValues[i]).Append('=').Append(keyValues[i + 1]);
            Debug.Log(sb.ToString());
        }
    }

    /// <summary>The three ad formats. LevelPlay plugs in behind this interface at MVP step 4.</summary>
    public interface IAdService
    {
        bool RewardedReady { get; }
        bool BannerVisible { get; }
        /// <summary>Shows the rewarded ad; the callback says whether the reward was earned.</summary>
        void ShowRewarded(Action<bool> done);
        /// <summary>Shows an interstitial if the rules allow it, then calls done.</summary>
        void ShowInterstitialIfAllowed(int runNumber, Action done);
    }

    /// <summary>Prototype stand-in: a full-screen "AD" card for a moment, always succeeds.</summary>
    public sealed class SimulatedAdService : MonoBehaviour, IAdService
    {
        public float MinInterstitialIntervalSeconds = 240f;
        public int InterstitialFirstRun = 3;
        public float BannerDelayHours = 24f;
        public PlayerProfile Profile;

        float lastInterstitialTime = float.NegativeInfinity;
        string showing;

        public bool RewardedReady => Application.internetReachability != NetworkReachability.NotReachable || Application.isEditor;
        public bool BannerVisible => Profile != null && ProfileStore.AccountAgeHours(Profile) >= BannerDelayHours;
        public bool IsShowing => showing != null;

        public void ShowRewarded(Action<bool> done)
        {
            StartCoroutine(Show("Reclamă cu recompensă (simulată)", 1.5f, () => done(true)));
        }

        public void ShowInterstitialIfAllowed(int runNumber, Action done)
        {
            bool allowed = runNumber >= InterstitialFirstRun && Time.realtimeSinceStartup - lastInterstitialTime >= MinInterstitialIntervalSeconds;
            if (!allowed)
            {
                done();
                return;
            }
            lastInterstitialTime = Time.realtimeSinceStartup;
            StartCoroutine(Show("Interstițială (simulată)", 1f, done));
        }

        IEnumerator Show(string label, float seconds, Action done)
        {
            showing = label;
            yield return new WaitForSecondsRealtime(seconds);
            showing = null;
            done();
        }

        void OnGUI()
        {
            if (showing == null) return;
            GUI.depth = -100;
            var style = new GUIStyle(GUI.skin.box) { fontSize = Mathf.RoundToInt(Screen.height / 30f), alignment = TextAnchor.MiddleCenter };
            GUI.Box(new Rect(0, 0, Screen.width, Screen.height), "AD\n" + showing, style);
        }
    }
}
