using System;
using UnityEngine;

namespace OnAir
{
    public sealed class DisplayController : MonoBehaviour
    {
        public DisplaySettings Settings { get; private set; }
        public bool ModePending { get; private set; }
        public float SecondsRemaining => Mathf.Max(0, modeDeadline - Time.unscaledTime);
        public string Status { get; private set; } = "";
        public float UiScale => Mathf.Min(Mathf.Min(Screen.width / 960f, Screen.height / 800f) * Settings.uiScale,
            Mathf.Min(Screen.width / 640f, Screen.height / 720f));
        SaveFileStore store;
        FullScreenMode defaultMode, priorMode;
        int defaultWidth, defaultHeight, defaultRate, defaultVsync, priorWidth, priorHeight, priorPreference;
        bool preserveRejected, writeBlocked;
        float modeDeadline;

        public void Initialize(string directory, bool persist)
        {
            defaultMode = Screen.fullScreenMode;defaultWidth = Screen.width;defaultHeight = Screen.height;
            defaultRate = Application.targetFrameRate;defaultVsync = QualitySettings.vSyncCount;
            Settings = new DisplaySettings { windowMode = defaultMode == FullScreenMode.Windowed ? 0 : 1 };
            if (!persist) return;
            store = new SaveFileStore(directory);
            try
            {
                string json = store.Read("display.json");if (json == null) return;
                var saved = JsonUtility.FromJson<DisplaySettings>(SaveFileStore.Unpack(json));
                if (saved.schemaVersion > 1) { writeBlocked = true;Status = "SETTINGS FROM A NEWER VERSION";return; }
                if (saved.schemaVersion != 1 || saved.windowMode < 0 || saved.windowMode > 1 || !SaveMigration.Finite(saved.uiScale) || saved.uiScale < .75f || saved.uiScale > 1.5f ||
                    (saved.frameLimit != 0 && saved.frameLimit != -1 && saved.frameLimit != 30 && saved.frameLimit != 60)) throw new FormatException("Invalid display settings.");
                Settings = saved;ApplyRate();ApplyMode(saved.windowMode);
            }
            catch (Exception error) { preserveRejected = true;Status = "OLD DISPLAY SETTINGS KEPT";Debug.LogWarning(error.Message); }
        }
        public void CycleScale()
        {
            float[] values = { .75f, 1, 1.25f, 1.5f };int i = Array.FindIndex(values, v => Mathf.Approximately(v, Settings.uiScale));
            Settings.uiScale = values[(i + 1) % values.Length];Save();
        }
        public void CycleRate()
        {
            Settings.frameLimit = Settings.frameLimit == 30 ? 60 : Settings.frameLimit == 60 ? -1 : 30;ApplyRate();Save();
        }
        void ApplyRate()
        {
            if (Settings.frameLimit == 0) { QualitySettings.vSyncCount = defaultVsync;Application.targetFrameRate = defaultRate; }
            else { QualitySettings.vSyncCount = 0;Application.targetFrameRate = Settings.frameLimit; }
        }
        void ApplyMode(int mode)
        {
#if !UNITY_EDITOR
            int width = mode == 1 ? Screen.currentResolution.width : Mathf.Max(640, defaultWidth);
            int height = mode == 1 ? Screen.currentResolution.height : Mathf.Max(480, defaultHeight);
            Screen.SetResolution(width, height, mode == 1 ? FullScreenMode.FullScreenWindow : FullScreenMode.Windowed);
#endif
        }
        public void PreviewMode()
        {
            if (ModePending) return;
            priorMode = Screen.fullScreenMode;priorWidth = Screen.width;priorHeight = Screen.height;priorPreference = Settings.windowMode;
            Settings.windowMode = 1 - Settings.windowMode;ApplyMode(Settings.windowMode);ModePending = true;modeDeadline = Time.unscaledTime + 15;
        }
        public void ConfirmMode() { if (!ModePending) return;ModePending = false;Save(); }
        public void RevertMode()
        {
            if (!ModePending) return;
            Settings.windowMode = priorPreference;ModePending = false;
#if !UNITY_EDITOR
            Screen.SetResolution(priorWidth, priorHeight, priorMode);
#endif
            Status = "DISPLAY MODE REVERTED";
        }
        public void ResetDefaults()
        {
            RevertMode();Settings = new DisplaySettings { windowMode = defaultMode == FullScreenMode.Windowed ? 0 : 1 };
            ApplyRate();
#if !UNITY_EDITOR
            Screen.SetResolution(defaultWidth, defaultHeight, defaultMode);
#endif
            Save();
        }
        void Save()
        {
            if (store == null || writeBlocked) return;
            try
            {
                var committed = new DisplaySettings { windowMode = ModePending ? priorPreference : Settings.windowMode, frameLimit = Settings.frameLimit, uiScale = Settings.uiScale };
                store.Write("display.json",SaveFileStore.Pack(committed),preserveRejected:preserveRejected);preserveRejected = false;Status = "DISPLAY SAVED";
            }
            catch (Exception error) { Status = "DISPLAY SAVE FAILED";Debug.LogWarning(error.Message); }
        }
        void Update() { if (ModePending && Time.unscaledTime >= modeDeadline) RevertMode(); }
        void OnApplicationQuit() { RevertMode(); }
    }
}
