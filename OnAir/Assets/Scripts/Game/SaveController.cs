using System;
using System.IO;
using System.Threading.Tasks;
using UnityEngine;

namespace OnAir
{
    [DefaultExecutionOrder(1000)]
    public sealed class SaveController : MonoBehaviour
    {
        public string Status { get; private set; } = "PREPARING FLIGHT";
        public string SaveDirectory => store?.DirectoryPath;
        public bool WriteBlocked { get; private set; }
        public bool HasPreviousJourney => enabledSaving && previousAvailable;
        public bool Restored { get; private set; }
        GameEntry game;
        SaveFileStore store;
        string configuration, journeyId, lastGoodSnapshot;
        bool initialized, enabledSaving, dirty, firstSave, preserveJourney, preserveSettings, settingsBlocked,previousAvailable;
        float dirtyAt, lastSavedAt;
        Task<WriteResult> writing;
        JourneySaveData committedState;
        sealed class WriteResult { public string snapshot, settingsError; public JourneySaveData state; }

        public void Initialize(GameEntry owner, bool randomize, string[] args)
        {
            game = owner;
            string overrideDirectory = Argument(args, "-onairSaveDirectory");
            bool hasSeed = int.TryParse(Argument(args, "-onairSeed"), out int fixedSeed) && fixedSeed >= 0;
            enabledSaving = overrideDirectory != null || (!hasSeed && !Application.isBatchMode);
            string profile = Application.isEditor ? "Editor" : "Player";
            store = new SaveFileStore(overrideDirectory ?? Path.Combine(Application.persistentDataPath, "Saves", profile));
            configuration = SaveMigration.ConfigurationKey(game);
            journeyId = Guid.NewGuid().ToString("N");
            if (enabledSaving)
            {
                previousAvailable=File.Exists(store.PathFor("previousJourney.json"));
                RestoreSettings();
                var loaded = LoadCurrent();
                if (loaded != null) { ApplyState(loaded); Restored = true; }
            }
            if (!Restored)
            {
                if (hasSeed) game.journey.seed = fixedSeed;
                else if (randomize) game.journey.seed = JourneyController.FreshSeed(game.journey.seed);
                game.journey.Initialize();game.session.InitializeClimate(Environment.TickCount);
                if (WriteBlocked) game.session.paused = true;
            }
            if (!enabledSaving) Status = "TEST FLIGHT - SAVE OFF";
            game.session.PlayerChanged += RequestSave;
            game.journey.BeforeNewSeed += BeforeNewJourney;
            game.journey.JourneyChanged += RequestSave;
            initialized = true;firstSave = true;dirty = true;dirtyAt = Time.unscaledTime;
        }
        static string Argument(string[] args, string key)
        {
            int index = Array.IndexOf(args, key);
            return index >= 0 && index+1 < args.Length ? args[index+1] : null;
        }
        void RestoreSettings()
        {
            try
            {
                string text = store.Read("settings.json");
                if (text != null) game.panel.RestoreSettings(SaveMigration.ReadSettings(text));
            }
            catch (SaveCompatibilityException error) { settingsBlocked = true;Debug.LogWarning("Saved settings kept: " + error.Message); }
            catch (Exception error) { preserveSettings = true;Debug.LogWarning("Unreadable settings kept: " + error.Message); }
        }
        JourneySaveData ReadJourney(string text) => SaveMigration.ReadJourney(text, configuration, game.journey.terrains, game.world.buildings);
        JourneySaveData LoadCurrent()
        {
            bool unreadable = false;
            foreach (string name in new[] { "journey.json", "journey.bak" })
            {
                try
                {
                    string text = store.Read(name);
                    if (text == null) continue;
                    var data = ReadJourney(text);
                    lastGoodSnapshot = text;committedState = data;preserveJourney = name != "journey.json";
                    Status = preserveJourney ? "FLIGHT RECOVERED FROM BACKUP" : "FLIGHT RESTORED";
                    return data;
                }
                catch (SaveCompatibilityException error)
                {
                    // A valid newer/incompatible save must not be silently rolled back.
                    Block(error);return null;
                }
                catch (Exception error) { unreadable = true;Debug.LogWarning(name + " could not be read: " + error.Message); }
            }
            if (unreadable) Block(new FormatException("No readable journey or backup."));
            return null;
        }
        void Block(Exception error)
        {
            WriteBlocked = true;Status = "OLD SAVE KEPT - START A NEW FLIGHT";
            Debug.LogWarning("Automatic save disabled to preserve existing files: " + error.Message);
        }
        void ApplyState(JourneySaveData data)
        {
            journeyId = data.journeyId;
            game.journey.RestoreState(data.journey);game.session.RestoreState(data.climate);
            game.world.RestoreState(data.world);game.flight.RestoreState(data.flight);
            game.cameraFollow.Follow(0);
        }
        public JourneySaveData CaptureState() => CaptureState(game.world.CaptureState());
        JourneySaveData CaptureState(WorldState world)
        {
            return new JourneySaveData
            {
                configuration = configuration, journeyId = journeyId, savedAtUtc = DateTimeOffset.UtcNow.ToString("O"),
                journey = game.journey.CaptureState(), flight = game.flight.CaptureState(),
                climate = game.session.CaptureState(), world = world
            };
        }
        JourneySaveData CaptureForFlush()
        {
            if (game.world.CanCaptureSave) return CaptureState();
            // Keep the last committed layout while a new road or tile is preparing.
            // Progress and climate still come from the current frame. Never reuse
            // a layout from another seed, configuration or journey.
            if (committedState != null && committedState.journeyId == journeyId &&
                committedState.configuration == configuration && committedState.journey.seed == game.journey.seed)
                return CaptureState(committedState.world);
            return null;
        }
        public void RequestSave()
        {
            if (!initialized || !enabledSaving) return;
            dirty = true;dirtyAt = Time.unscaledTime;
        }
        void LateUpdate()
        {
            if (!initialized || !enabledSaving || WriteBlocked) return;
            CompleteWrite(false);
            if (writing != null || !game.world.CanCaptureSave) return;
            bool interval = Time.unscaledTime-lastSavedAt >= Mathf.Max(5, game.autoSaveIntervalSeconds);
            if (firstSave || interval || (dirty && Time.unscaledTime-dirtyAt >= .5f)) BeginWrite();
        }
        void BeginWrite(JourneySaveData captured = null)
        {
            try
            {
                var state = captured ?? CaptureState();var settings = game.panel.CaptureSettings();
                bool rejectedJourney = preserveJourney, rejectedSettings = preserveSettings, writeSettings = !settingsBlocked;
                dirty = false;firstSave = false;Status = "SAVING FLIGHT";
                writing = Task.Run(() =>
                {
                    var result = new WriteResult { snapshot = SaveFileStore.Pack(state), state = state };
                    store.Write("journey.json", result.snapshot, "journey.bak", rejectedJourney);
                    if (writeSettings)
                    {
                        try { store.Write("settings.json", SaveFileStore.Pack(settings), preserveRejected: rejectedSettings); }
                        catch (Exception error) { result.settingsError = error.Message; }
                    }
                    return result;
                });
            }
            catch (Exception error) { WriteFailed(error); }
        }
        void CompleteWrite(bool wait)
        {
            if (writing == null || (!wait && !writing.IsCompleted)) return;
            var task = writing;writing = null;
            try
            {
                var result = task.GetAwaiter().GetResult();lastGoodSnapshot = result.snapshot;committedState = result.state;preserveJourney = false;
                if (result.settingsError == null) preserveSettings = false;
                else Debug.LogWarning("Flight saved; settings could not be written: " + result.settingsError);
                lastSavedAt = Time.unscaledTime;
                Status = result.settingsError == null ? "FLIGHT SAVED" : "FLIGHT SAVED - SETTINGS NOT SAVED";
            }
            catch (Exception error) { WriteFailed(error); }
        }
        void WriteFailed(Exception error)
        {
            Status = "SAVE FAILED - WILL RETRY";dirty = true;firstSave = false;
            dirtyAt = Time.unscaledTime+4.5f;lastSavedAt = Time.unscaledTime;
            Debug.LogWarning("Flight save failed: " + error.Message);
        }
        public bool SaveNow()
        {
            if (!initialized || !enabledSaving || WriteBlocked) return false;
            CompleteWrite(true);
            var captured = CaptureForFlush();
            if (captured == null) return false;
            BeginWrite(captured);CompleteWrite(true);
            return Status == "FLIGHT SAVED" || Status == "FLIGHT SAVED - SETTINGS NOT SAVED";
        }
        void BeforeNewJourney()
        {
            if (!enabledSaving) return;
            CompleteWrite(true);
            string previous = lastGoodSnapshot;
            if (!WriteBlocked)
            {
                var captured = CaptureForFlush();
                if (captured != null) previous = SaveFileStore.Pack(captured);
            }
            // Failure propagates before JourneyController changes its seed/progress.
            store.ArchiveCurrent(previous, preserveJourney);
            previousAvailable=previous!=null||File.Exists(store.PathFor("previousJourney.json"));
            lastGoodSnapshot = null;committedState = null;preserveJourney = false;WriteBlocked = false;
            journeyId = Guid.NewGuid().ToString("N");firstSave = true;Status = "PREPARING NEW FLIGHT";
        }
        public bool StartNewJourney()
        {
            try { game.journey.NewSeed();return true; }
            catch (Exception error) { WriteFailed(error);return false; }
        }
        public bool ReturnToPreviousJourney()
        {
            if (!enabledSaving) return false;
            try
            {
                string text = store.Read("previousJourney.json");
                if (text == null) return false;
                var previous = ReadJourney(text);
                BeforeNewJourney();ApplyState(previous);Restored = true;
                lastGoodSnapshot = text;committedState = previous;
                game.world.Refresh();game.environment.Refresh(0,true);RequestSave();Status = "PREVIOUS FLIGHT RESTORED";
                return true;
            }
            catch (Exception error) { Status = "PREVIOUS FLIGHT UNAVAILABLE";Debug.LogWarning("Previous journey kept: " + error.Message);return false; }
        }
        public void BeforeContentChange() { if (enabledSaving&&configuration!=SaveMigration.ConfigurationKey(game)) BeforeNewJourney(); }
        public void AfterContentChange()
        {
            configuration = SaveMigration.ConfigurationKey(game);RequestSave();
        }
        void OnApplicationPause(bool paused) { if (paused) SaveNow(); }
        void OnApplicationFocus(bool focused) { if (!focused) SaveNow(); }
        void OnApplicationQuit() { SaveNow(); }
        void OnDestroy()
        {
            if (!initialized) return;
            CompleteWrite(true);
            game.session.PlayerChanged -= RequestSave;game.journey.BeforeNewSeed -= BeforeNewJourney;game.journey.JourneyChanged -= RequestSave;
        }
    }
}
