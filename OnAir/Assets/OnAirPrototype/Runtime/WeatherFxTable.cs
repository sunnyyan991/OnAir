using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using UnityEngine;

namespace OnAir.Prototype
{
    public sealed class WeatherFxRule
    {
        public Weather Weather;
        public string Shape;
        public float Rate, Speed, Wind, Size, Length, Lifetime, NearRatio, Sway, Transition;
        public Color Color;
    }

    public static class WeatherFxTable
    {
        public const string Header = "weather,shape,rate,speed,wind,size,length,lifetime,color,near_ratio,sway,transition";
        public static Dictionary<Weather, WeatherFxRule> Parse(string csv)
        {
            var rows = SpawnRules.ReadCsv(csv.TrimStart('\uFEFF'));
            bool marked = rows.Count > 0 && rows[0][0] == "##var";
            if (rows.Count == 0 || string.Join(",", marked ? rows[0].Skip(1) : rows[0]) != Header)
                throw new FormatException("Weather table: expected " + Header);
            var result = new Dictionary<Weather, WeatherFxRule>();
            bool types = false;
            for (int i = 1; i < rows.Count; i++)
            {
                var r = rows[i];
                if (r.All(string.IsNullOrWhiteSpace)) continue;
                if (r[0] == "##type")
                {
                    const string expected = "Weather,string,float,float,float,float,float,float,string,float,float,float";
                    if (!marked || types || string.Join(",", r.Skip(1)) != expected) throw new FormatException("Weather table: invalid ##type.");
                    types = true; continue;
                }
                if (r[0].StartsWith("#", StringComparison.Ordinal)) continue;
                try
                {
                    if (marked) { if (r[0] != "") throw new FormatException("Expected empty marker."); r = r.Skip(1).ToArray(); }
                    if (r.Length != 12) throw new FormatException("Expected 12 fields.");
                    if (!Enum.GetNames(typeof(Weather)).Contains(r[0])) throw new FormatException("Unknown weather " + r[0]);
                    var weather = (Weather)Enum.Parse(typeof(Weather), r[0]);
                    if (result.ContainsKey(weather)) throw new FormatException("Duplicate weather.");
                    if (!new[] { "none", "rain", "snow", "dust" }.Contains(r[1])) throw new FormatException("Unknown shape.");
                    if (!ColorUtility.TryParseHtmlString(r[8], out var color)) throw new FormatException("Invalid HTML color.");
                    var rule = new WeatherFxRule { Weather = weather, Shape = r[1], Rate = Number(r[2],0,800), Speed = Number(r[3],0,40), Wind = Number(r[4],-20,20), Size = Number(r[5],.01f,1), Length = Number(r[6],.01f,3), Lifetime = Number(r[7],.1f,12), Color = color, NearRatio = Number(r[9],0,1), Sway = Number(r[10],0,2), Transition = Number(r[11],.05f,10) };
                    if (rule.Shape == "none" && rule.Rate != 0) throw new FormatException("none requires zero rate.");
                    if (rule.Rate * rule.Lifetime > 4000) throw new FormatException("rate * lifetime exceeds 4000 particle budget.");
                    result.Add(weather, rule);
                }
                catch (FormatException e) { throw new FormatException($"Weather row {i + 1}: {e.Message}", e); }
            }
            if (marked && !types) throw new FormatException("Missing ##type.");
            foreach (Weather w in Enum.GetValues(typeof(Weather))) if (!result.ContainsKey(w)) throw new FormatException("Missing weather " + w);
            return result;
        }
        static float Number(string s, float min, float max)
        {
            if (!float.TryParse(s, NumberStyles.Float, CultureInfo.InvariantCulture, out var v) || float.IsNaN(v) || float.IsInfinity(v) || v < min || v > max)
                throw new FormatException($"Number {s} outside [{min}, {max}].");
            return v;
        }
    }
}
