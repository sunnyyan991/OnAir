using System;
using System.Collections.Generic;
using System.IO;

namespace OnAir
{
    public sealed class SaveSlot
    {
        public int index;
        public string name, writeDirectory, readDirectory, primaryName = "journey.json";
        public bool occupied, compatible;
        public string error, biome, savedAt;
        public double meters;
        public bool IsLegacy => index < 0;
    }

    // Slot discovery is read-only. Legacy files are imported only by an explicit load,
    // and subsequent writes use a separate directory, leaving the originals intact.
    public sealed class SaveSlots
    {
        readonly string root;
        readonly int count;
        public SaveSlots(string directory, int slots) { root = directory; count = Math.Max(1, Math.Min(8, slots)); }
        public List<SaveSlot> List(Func<string, JourneySaveData> validate, bool readFiles)
        {
            var result = new List<SaveSlot>();
            for (int i = 1; i <= 8; i++)
            {
                string directory = Path.Combine(root, "Slots", i.ToString("00"));
                if (i > count && (!readFiles || (!File.Exists(Path.Combine(directory,"journey.json")) && !File.Exists(Path.Combine(directory,"journey.bak"))))) continue;
                result.Add(Inspect(new SaveSlot { index = i, name = "FLIGHT " + i.ToString("00"), writeDirectory = directory, readDirectory = directory }, validate, readFiles));
            }
            if (readFiles)
            {
                AddLegacy(result, -1, "LEGACY FLIGHT", "LegacyCurrent", "journey.json", validate);
                AddLegacy(result, -2, "PREVIOUS FLIGHT", "LegacyPrevious", "previousJourney.json", validate);
            }
            return result;
        }
        void AddLegacy(List<SaveSlot> result, int index, string name, string folder, string source, Func<string, JourneySaveData> validate)
        {
            string target = Path.Combine(root, "Slots", folder);
            bool imported = File.Exists(Path.Combine(target,"journey.json")) || File.Exists(Path.Combine(target,"journey.bak"));
            if (!imported && !File.Exists(Path.Combine(root, source)) && !(index == -1 && File.Exists(Path.Combine(root,"journey.bak")))) return;
            result.Add(Inspect(new SaveSlot { index = index, name = name, writeDirectory = target, readDirectory = imported ? target : root, primaryName = imported ? "journey.json" : source }, validate, true));
        }
        static SaveSlot Inspect(SaveSlot slot, Func<string, JourneySaveData> validate, bool readFiles)
        {
            if (!readFiles) return slot;
            var store = new SaveFileStore(slot.readDirectory);
            slot.occupied = File.Exists(store.PathFor(slot.primaryName)) || (slot.primaryName == "journey.json" && File.Exists(store.PathFor("journey.bak")));
            if (!slot.occupied) return slot;
            try
            {
                var data = Read(slot, validate, out _);
                slot.compatible = true; slot.meters = data.journey.totalMeters; slot.savedAt = data.savedAtUtc;
                slot.biome = string.IsNullOrEmpty(data.biomeName) ? "SAVED WORLD" : data.biomeName;
            }
            catch (SaveCompatibilityException) { slot.error = "INCOMPATIBLE SAVE"; }
            catch (Exception) { slot.error = "COULD NOT READ THIS SAVE"; }
            return slot;
        }
        public static JourneySaveData Read(SaveSlot slot, Func<string, JourneySaveData> validate, out bool recovered)
        {
            var store = new SaveFileStore(slot.readDirectory);
            Exception failure = null; recovered = false;
            foreach (string name in slot.primaryName == "journey.json" ? new[] { "journey.json", "journey.bak" } : new[] { slot.primaryName })
            {
                try
                {
                    string text = store.Read(name);
                    if (text == null) continue;
                    var data = validate(text); recovered = name == "journey.bak"; return data;
                }
                catch (SaveCompatibilityException) { throw; }
                catch (Exception error) { failure = error; }
            }
            throw new FormatException("No readable saved journey.", failure);
        }
    }
}
