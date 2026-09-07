using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text;

namespace OnAir.Prototype
{
    public enum Biome { City, Coast, Desert }
    public enum Weather { Clear = 0, Rain = 1, Snow = 2, Cloudy = 3, HeavyRain = 4, Sandstorm = 5 }
    public enum DayPeriod { Dawn, Day, Dusk, Night }

    // Pure data and selection logic: no scene objects, rendering or global random state.
    public sealed class SpawnRule
    {
        public string Id, PrefabId;
        public HashSet<string> Biomes, Weathers, Periods;
        public float Weight, MinScale, MaxScale;
        public bool Matches(Biome biome, Weather weather, DayPeriod period) =>
            Accepts(Biomes, biome.ToString()) && Accepts(Weathers, weather.ToString()) && Accepts(Periods, period.ToString());
        static bool Accepts(HashSet<string> set, string value) => set.Contains("*") || set.Contains(value);
    }

    public static class SpawnRules
    {
        public static List<SpawnRule> Parse(string csv)
        {
            var rows = ReadCsv(csv.TrimStart('\uFEFF'));
            if (rows.Count == 0) throw new FormatException("Building table is empty.");
            string[] columns = { "id", "prefab_id", "biomes", "weather", "periods", "weight", "min_scale", "max_scale" };
            bool marked = rows[0].Length > 0 && rows[0][0] == "##var";
            if (!(marked ? rows[0].Skip(1) : rows[0]).SequenceEqual(columns)) throw new FormatException("Expected ##var followed by: " + string.Join(",", columns));
            string[] types = { "string", "string", "(list#sep=|),Biome", "(list#sep=|),Weather", "(list#sep=|),DayPeriod", "float", "float", "float" };
            bool typeRowSeen = false;
            var result = new List<SpawnRule>();
            var ids = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.All(string.IsNullOrWhiteSpace)) continue;
                if (r[0] == "##type")
                {
                    if (!marked || typeRowSeen || !r.Skip(1).SequenceEqual(types)) throw new FormatException($"Building table row {i + 1}: unexpected type declaration.");
                    typeRowSeen = true;
                    continue;
                }
                if (r[0].StartsWith("#", StringComparison.Ordinal)) continue;
                try
                {
                    if (marked)
                    {
                        if (r.Length != columns.Length + 1 || r[0] != "") throw new FormatException("Data rows require an empty marker column followed by 8 fields.");
                        r = r.Skip(1).ToArray();
                    }
                    if (r.Length != columns.Length) throw new FormatException("Expected 8 columns.");
                    if (string.IsNullOrEmpty(r[0]) || r[0].Any(c => c < '0' || c > '9') || !ids.Add(r[0])) throw new FormatException("id must be a unique string containing digits only.");
                    if (string.IsNullOrWhiteSpace(r[1])) throw new FormatException("prefab_id is required.");
                    var rule = new SpawnRule { Id = r[0], PrefabId = r[1], Biomes = Tags<Biome>(r[2]), Weathers = Tags<Weather>(r[3]), Periods = Tags<DayPeriod>(r[4]), Weight = Number(r[5]), MinScale = Number(r[6]), MaxScale = Number(r[7]) };
                    if (rule.Weight < 0 || rule.MinScale <= 0 || rule.MaxScale < rule.MinScale || rule.MaxScale > 1.5f)
                        throw new FormatException("weight >= 0; 0 < min_scale <= max_scale <= 1.5 required.");
                    result.Add(rule);
                }
                catch (FormatException e) { throw new FormatException($"Building table row {i + 1}: {e.Message}", e); }
            }
            if (marked && !typeRowSeen) throw new FormatException("Missing ##type row.");
            return result;
        }
        static float Number(string text)
        {
            if (!float.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out float value) || float.IsNaN(value) || float.IsInfinity(value))
                throw new FormatException("Invalid number: " + text);
            return value;
        }
        static HashSet<string> Tags<T>(string text) where T : Enum
        {
            var set = new HashSet<string>(text.Split('|').Select(x => x.Trim()), StringComparer.OrdinalIgnoreCase);
            var allowed = new HashSet<string>(Enum.GetNames(typeof(T)), StringComparer.OrdinalIgnoreCase);
            foreach (var tag in set) if (tag != "*" && !allowed.Contains(tag)) throw new FormatException("Unknown " + typeof(T).Name + ": " + tag);
            return set;
        }
        public static SpawnRule Pick(IReadOnlyList<SpawnRule> rules, Biome biome, Weather weather, DayPeriod period, Random random)
        {
            double total = 0;
            foreach (var r in rules) if (r.Matches(biome, weather, period)) total += r.Weight;
            if (total <= 0) return null; // No matching building means an empty lot, never a rule-breaking fallback.
            double ticket = random.NextDouble() * total;
            foreach (var r in rules)
                if (r.Weight > 0 && r.Matches(biome, weather, period)) { ticket -= r.Weight; if (ticket < 0) return r; }
            return null;
        }
        internal static List<string[]> ReadCsv(string text)
        {
            var rows = new List<string[]>(); var row = new List<string>(); var field = new StringBuilder(); bool quoted = false;
            for (int i = 0; i < text.Length; i++)
            {
                char c = text[i];
                if (c == '"')
                {
                    if (quoted && i + 1 < text.Length && text[i + 1] == '"') { field.Append('"'); i++; }
                    else quoted = !quoted;
                }
                else if (!quoted && (c == ',' || c == '\n' || c == '\r'))
                {
                    row.Add(field.ToString().Trim()); field.Clear();
                    if (c != ',') { rows.Add(row.ToArray()); row.Clear(); if (c == '\r' && i + 1 < text.Length && text[i + 1] == '\n') i++; }
                }
                else field.Append(c);
            }
            if (quoted) throw new FormatException("Unclosed CSV quote.");
            if (field.Length > 0 || row.Count > 0) { row.Add(field.ToString().Trim()); rows.Add(row.ToArray()); }
            return rows;
        }
    }
}
