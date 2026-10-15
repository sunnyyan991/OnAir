using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace OnAir
{
    public struct ClimateRule { public Weather weather; public float weight,minSeconds,maxSeconds; }
    // Automatic weather per ecology zone of the continuous world.
    public static class ClimateTable
    {
        public static IEnumerable<string> Zones=>Enum.GetNames(typeof(ContinuousWorldPlan.EcologyZone)).Where(zone=>zone!=nameof(ContinuousWorldPlan.EcologyZone.Airport));
        public static Dictionary<string,ClimateRule[]> Parse(string text)
        {
            var rows=CsvReader.Read(text.TrimStart('﻿'));
            const string columns="##var,zone,weather,weight,min_seconds,max_seconds";
            string[] types={"##type","string","Weather","float","float","float"};
            if(rows.Count==0||string.Join(",",rows[0])!=columns)throw new FormatException("Climate table: unexpected columns.");
            var zones=new HashSet<string>(Zones);var lists=new Dictionary<string,List<ClimateRule>>();bool typed=false;
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i];if(row.All(string.IsNullOrWhiteSpace))continue;
                if(row[0]=="##type"){if(typed||!row.SequenceEqual(types))throw new FormatException("Climate table: invalid ##type.");typed=true;continue;}
                if(row[0].StartsWith("#",StringComparison.Ordinal))continue;
                try
                {
                    if(row.Length!=6||row[0]!="")throw new FormatException("Expected an empty marker and five fields.");
                    if(!zones.Contains(row[1]))throw new FormatException("Unknown zone: "+row[1]);
                    if(!Enum.GetNames(typeof(Weather)).Contains(row[2]))throw new FormatException("Unknown weather: "+row[2]);
                    var rule=new ClimateRule{weather=(Weather)Enum.Parse(typeof(Weather),row[2]),weight=Number(row[3]),minSeconds=Number(row[4]),maxSeconds=Number(row[5])};
                    if(rule.weight<0||rule.minSeconds<5||rule.maxSeconds<rule.minSeconds)throw new FormatException("Weight must be >= 0; seconds must satisfy 5 <= min <= max.");
                    if(!lists.TryGetValue(row[1],out var list))lists.Add(row[1],list=new List<ClimateRule>());
                    if(list.Any(r=>r.weather==rule.weather))throw new FormatException("Duplicate weather in zone.");
                    list.Add(rule);
                }
                catch(FormatException e){throw new FormatException($"Climate row {i+1}: {e.Message}",e);}
            }
            if(!typed)throw new FormatException("Climate table needs ##type.");
            foreach(var zone in zones)if(!lists.TryGetValue(zone,out var list)||!list.Any(r=>r.weight>0))throw new FormatException("Climate zone needs a positive weight: "+zone);
            return lists.ToDictionary(p=>p.Key,p=>p.Value.ToArray());
        }
        static float Number(string value)
        {
            if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||float.IsNaN(number)||float.IsInfinity(number))throw new FormatException("Invalid number: "+value);
            return number;
        }
    }
}
