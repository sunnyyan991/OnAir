using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;

namespace OnAir
{
    // Pure data and selection logic: no scene objects, rendering or global random state.
    public sealed class SpawnRule
    {
        public string Id, PrefabId;
        public HashSet<string> Biomes, Weathers, Periods;
        public float Weight, MinScale, MaxScale;
        public string Family="Generic";
        public float Residential=1,Mixed=1,Commercial=1;
        public int MaxPerChunk;
        public float DistrictWeight(int district)=>district==0?Residential:district==1?Mixed:Commercial;
        public bool Matches(Biome biome, Weather weather, DayPeriod period) =>
            Accepts(Biomes, biome.ToString()) && Accepts(Weathers, weather.ToString()) && Accepts(Periods, period.ToString());
        static bool Accepts(HashSet<string> set, string value) => set.Contains("*") || set.Contains(value);
    }

    public static class BuildingRules
    {
        public static List<SpawnRule> Parse(string csv)
        {
            var rows = CsvReader.Read(csv.TrimStart('\uFEFF'));
            if (rows.Count == 0) throw new FormatException("Building table is empty.");
            string[] columns = { "id", "prefab_id", "biomes", "weather", "periods", "weight", "min_scale", "max_scale" };
            bool marked = rows[0].Length > 0 && rows[0][0] == "##var";
            bool districtColumns=(marked?rows[0].Length-1:rows[0].Length)>columns.Length;
            if(districtColumns)columns=columns.Concat(new[]{"family","residential","mixed","commercial","max_per_chunk"}).ToArray();
            if (!(marked ? rows[0].Skip(1) : rows[0]).SequenceEqual(columns)) throw new FormatException("Expected ##var followed by: " + string.Join(",", columns));
            string[] types = { "string", "string", "(list#sep=|),Biome", "(list#sep=|),Weather", "(list#sep=|),DayPeriod", "float", "float", "float" };
            if(districtColumns)types=types.Concat(new[]{"string","float","float","float","int"}).ToArray();
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
                        if (r.Length != columns.Length + 1 || r[0] != "") throw new FormatException("Data rows require an empty marker column followed by "+columns.Length+" fields.");
                        r = r.Skip(1).ToArray();
                    }
                    if (r.Length != columns.Length) throw new FormatException("Expected "+columns.Length+" columns.");
                    if (string.IsNullOrEmpty(r[0]) || r[0].Any(c => c < '0' || c > '9') || !ids.Add(r[0])) throw new FormatException("id must be a unique string containing digits only.");
                    if (string.IsNullOrWhiteSpace(r[1])) throw new FormatException("prefab_id is required.");
                    var rule = new SpawnRule { Id = r[0], PrefabId = r[1], Biomes = Tags<Biome>(r[2]), Weathers = Tags<Weather>(r[3]), Periods = Tags<DayPeriod>(r[4]), Weight = Number(r[5]), MinScale = Number(r[6]), MaxScale = Number(r[7]) };
                    if (rule.Weight < 0 || rule.MinScale <= 0 || rule.MaxScale < rule.MinScale || rule.MaxScale > 1.5f)
                        throw new FormatException("weight >= 0; 0 < min_scale <= max_scale <= 1.5 required.");
                    if(districtColumns)
                    {
                        rule.Family=r[8];rule.Residential=Number(r[9]);rule.Mixed=Number(r[10]);rule.Commercial=Number(r[11]);
                        if(!new[]{"Generic","House","Apartment","Shop","Office","Retail","Tower"}.Contains(rule.Family))throw new FormatException("Unknown building family.");
                        if(rule.Residential<0||rule.Mixed<0||rule.Commercial<0||!int.TryParse(r[12],out rule.MaxPerChunk)||rule.MaxPerChunk<0)throw new FormatException("Invalid district weight or chunk limit.");
                    }
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
    }
}
