using System;
using System.IO;
using System.Collections.Generic;
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
        public bool ActiveJourney { get; private set; }
        public bool SuspendAutomatic { get; set; }
        public string RootDirectory => settingsStore?.DirectoryPath;
        public SaveSlot CurrentSlot { get; private set; }
        public bool SavingEnabled => enabledSaving;
        GameEntry game;
        SaveFileStore store;
        SaveFileStore settingsStore;
        SaveSlots slots;
        ClimateState initialClimate;
        FlightState initialFlight;
        int? testSeed;
        string configuration, journeyId, lastGoodSnapshot;
        bool initialized, enabledSaving, dirty, firstSave, preserveJourney, preserveSettings, settingsBlocked,previousAvailable;
        float dirtyAt, lastSavedAt;
        Task<WriteResult> writing;
        JourneySaveData committedState;
        SaveSettings committedSettings;
        public bool HasUnsavedChanges
        {
            get
            {
                if (!ActiveJourney) return false;
                if (committedState == null || dirty || committedState.journeyId != journeyId || committedState.configuration != configuration) return true;
                return JsonUtility.ToJson(game.journey.CaptureState()) != JsonUtility.ToJson(committedState.journey) ||
                    JsonUtility.ToJson(game.flight.CaptureState()) != JsonUtility.ToJson(committedState.flight) ||
                    JsonUtility.ToJson(game.session.CaptureState()) != JsonUtility.ToJson(committedState.climate) ||
                    (committedSettings != null && JsonUtility.ToJson(game.panel.CaptureSettings()) != JsonUtility.ToJson(committedSettings));
            }
        }
        sealed class WriteResult { public string snapshot, settingsError; public JourneySaveData state; public SaveSettings settings; }

        public void Initialize(GameEntry owner, bool randomize, string[] args, bool titleMode = false)
        {
            game = owner;
            string overrideDirectory = Argument(args, "-onairSaveDirectory");
            bool hasSeed = int.TryParse(Argument(args, "-onairSeed"), out int fixedSeed) && fixedSeed >= 0;
            enabledSaving = overrideDirectory != null || (!hasSeed && !Application.isBatchMode);
            string profile = Application.isEditor ? "Editor" : "Player";
            store = new SaveFileStore(overrideDirectory ?? Path.Combine(Application.persistentDataPath, "Saves", profile));
            settingsStore = store; slots = new SaveSlots(store.DirectoryPath, game.saveSlotCount);
            testSeed = hasSeed ? fixedSeed : (int?)null;
            ActiveJourney = !titleMode;
            configuration = SaveMigration.ConfigurationKey(game);
            journeyId = Guid.NewGuid().ToString("N");
            if (enabledSaving)
            {
                RestoreSettings();
                if (!titleMode)
                {
                    previousAvailable=File.Exists(store.PathFor("previousJourney.json"));
                    var loaded = LoadCurrent();
                    if (loaded != null) { ApplyState(loaded); Restored = true; }
                }
            }
            if (!Restored)
            {
                if (hasSeed) game.journey.seed = fixedSeed;
                else if (randomize) game.journey.seed = JourneyController.FreshSeed(game.journey.seed);
                game.journey.Initialize();game.session.InitializeClimate(Environment.TickCount);
                if (WriteBlocked) game.session.paused = true;
            }
            if (!enabledSaving) Status = "TEST FLIGHT - SAVE OFF";
            initialClimate = game.session.CaptureState();
            initialFlight = game.flight.CaptureState();
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
        public List<SaveSlot> GetSlots() => slots.List(ReadJourney, enabledSaving);
        public bool StageSlot(SaveSlot slot, bool create, bool overwriteConfirmed = false)
        {
            if (ActiveJourney || slot == null || (create && slot.IsLegacy)) return false;
            try
            {
                CompleteWrite(true);
                var current = GetSlots().Find(s => s.index == slot.index);
                if (current == null) return false;
                if (create && current.occupied && !overwriteConfirmed) { Status = "OVERWRITE CONFIRMATION REQUIRED"; return false; }
                JourneySaveData loaded = null; bool recovered = false;
                if (!create) loaded = SaveSlots.Read(current, ReadJourney, out recovered);
                store = new SaveFileStore(current.writeDirectory); CurrentSlot = current;
                lastGoodSnapshot = null; committedState = null; WriteBlocked = false;
                preserveJourney = recovered && current.readDirectory == current.writeDirectory;
                previousAvailable = File.Exists(store.PathFor("previousJourney.json"));
                Restored = !create; firstSave = false; dirty = false;
                if (create)
                {
                    game.world.RestoreState(null);
                    journeyId = Guid.NewGuid().ToString("N"); game.session.RestoreState(initialClimate);
                    game.journey.Restart(testSeed ?? JourneyController.FreshSeed(game.journey.seed));
                    game.flight.RestoreState(initialFlight);
                }
                else
                {
                    ApplyState(loaded); committedState = loaded; lastGoodSnapshot = SaveFileStore.Pack(loaded);
                    if (current.index == -1 && current.readDirectory == settingsStore.DirectoryPath)
                        previousAvailable |= File.Exists(settingsStore.PathFor("previousJourney.json"));
                }
                Status = "PREPARING FLIGHT";return true;
            }
            catch (SaveCompatibilityException) { Status = "INCOMPATIBLE SAVE"; return false; }
            catch (Exception error) { Status = "COULD NOT LOAD THIS SAVE";Debug.LogWarning("Slot unchanged: "+error.Message);return false; }
        }
        public bool CommitStagedSlot(bool create)
        {
            ActiveJourney = true; SuspendAutomatic = false;
            if (!enabledSaving) { Status = "TEST FLIGHT - SAVE OFF"; return true; }
            if (create && !SaveNow()) { ActiveJourney = false; SuspendAutomatic = true; return false; }
            firstSave = !create;lastSavedAt = Time.unscaledTime;return true;
        }
        public void EndJourneyWithoutSaving()
        {
            CompleteWrite(true);ActiveJourney = false;SuspendAutomatic = true;dirty = false;firstSave = false;
        }
        public void FinishPendingWrite() => CompleteWrite(true);
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
                biomeName = game.biomeNavigator ? game.biomeNavigator.VisibleName : game.session.biome.ToString().ToUpperInvariant(),
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
            if (!initialized || !enabledSaving || !ActiveJourney) return;
            dirty = true;dirtyAt = Time.unscaledTime;
        }
        void LateUpdate()
        {
            if (!initialized || !enabledSaving || !ActiveJourney || SuspendAutomatic || WriteBlocked) return;
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
                    var result = new WriteResult { snapshot = SaveFileStore.Pack(state), state = state, settings = settings };
                    store.Write("journey.json", result.snapshot, "journey.bak", rejectedJourney);
                    if (writeSettings)
                    {
                        try { settingsStore.Write("settings.json", SaveFileStore.Pack(settings), preserveRejected: rejectedSettings); }
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
                if (result.settingsError == null) { preserveSettings = false; committedSettings = result.settings; }
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
            if (!initialized || !enabledSaving || !ActiveJourney || WriteBlocked) return false;
            CompleteWrite(true);
            var captured = CaptureForFlush();
            if (captured == null) return false;
            BeginWrite(captured);CompleteWrite(true);
            return Status == "FLIGHT SAVED" || Status == "FLIGHT SAVED - SETTINGS NOT SAVED";
        }
        void BeforeNewJourney()
        {
            if (!enabledSaving || !ActiveJourney) return;
            CompleteWrite(true);
            string previous = lastGoodSnapshot;
            if (!WriteBlocked)
            {
                var captured = CaptureForFlush();
                if (captured != null) previous = SaveFileStore.Pack(captured);
            }
            // Failure propagates before JourneyController changes its seed/progress.
            if (previous != null) store.Write("previousJourney.json", previous);
            previousAvailable=previous!=null||File.Exists(store.PathFor("previousJourney.json"));
            lastGoodSnapshot = null;committedState = null;WriteBlocked = false;
            journeyId = Guid.NewGuid().ToString("N");firstSave = true;Status = "PREPARING NEW FLIGHT";
        }
        public bool StartNewJourney()
        {
            try { game.journey.NewSeed();return true; }
            catch (Exception error) { WriteFailed(error);return false; }
        }
        public bool ReturnToPreviousJourney()
        {
            if (!enabledSaving || !ActiveJourney) return false;
            try
            {
                string text = store.Read("previousJourney.json");
                if (text == null && CurrentSlot?.index == -1) text = settingsStore.Read("previousJourney.json");
                if (text == null) return false;
                var previous = ReadJourney(text);
                BeforeNewJourney();ApplyState(previous);Restored = true;
                lastGoodSnapshot = text;committedState = previous;
                firstSave = true;
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
        void AutoFlush() { if (!SuspendAutomatic) SaveNow(); }
        void OnApplicationPause(bool paused) { if (paused) AutoFlush(); }
        void OnApplicationFocus(bool focused) { if (!focused) AutoFlush(); }
        void OnApplicationQuit() { AutoFlush(); }
        void OnDestroy()
        {
            if (!initialized) return;
            CompleteWrite(true);
            game.session.PlayerChanged -= RequestSave;game.journey.BeforeNewSeed -= BeforeNewJourney;game.journey.JourneyChanged -= RequestSave;
        }
    }
}
