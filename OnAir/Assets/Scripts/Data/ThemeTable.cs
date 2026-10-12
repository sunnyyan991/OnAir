using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace OnAir
{
    // A country/region theme: which ecology zones its continuous world may generate, and how often.
    public sealed class ThemeRule
    {
        public string id,key,name;
        // Indexed by ContinuousWorldPlan.EcologyZone; zones the theme does not list have weight 0.
        public float[] zoneWeights;
        public bool Allows(ContinuousWorldPlan.EcologyZone zone)=>zoneWeights[(int)zone]>0;
    }
    public static class ThemeTable
    {
        public static List<ThemeRule> Parse(string text)
        {
            var rows=CsvReader.Read(text.TrimStart('\uFEFF'));
            const string columns="##var,id,key,name,zones,zone_weights";
            string[] types={"##type","string","string","string","(list#sep=|),EcologyZone","(list#sep=|),float"};
            if(rows.Count==0||string.Join(",",rows[0])!=columns)throw new FormatException("Theme table: unexpected columns.");
            var result=new List<ThemeRule>();var ids=new HashSet<string>();var keys=new HashSet<string>();bool typed=false;
            var zoneNames=Enum.GetNames(typeof(ContinuousWorldPlan.EcologyZone));
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i];if(row.All(string.IsNullOrWhiteSpace))continue;
                if(row[0]=="##type"){if(typed||!row.SequenceEqual(types))throw new FormatException("Theme table: invalid ##type.");typed=true;continue;}
                if(row[0].StartsWith("#",StringComparison.Ordinal))continue;
                try
                {
                    if(row.Length!=6||row[0]!="")throw new FormatException("Expected an empty marker and five fields.");
                    if(row[1].Length==0||row[1].Any(c=>c<'0'||c>'9')||!ids.Add(row[1]))throw new FormatException("id must be a unique digit string.");
                    if(string.IsNullOrWhiteSpace(row[2])||!keys.Add(row[2])||string.IsNullOrWhiteSpace(row[3]))throw new FormatException("key must be unique; name must not be empty.");
                    var names=row[4].Split('|');var weights=row[5].Split('|');
                    if(names.Length!=weights.Length)throw new FormatException("Zones and weights must have equal lengths.");
                    var rule=new ThemeRule{id=row[1],key=row[2],name=row[3],zoneWeights=new float[zoneNames.Length]};var seen=new HashSet<string>();
                    for(int n=0;n<names.Length;n++)
                    {
                        int zone=Array.IndexOf(zoneNames,names[n]);
                        if(zone<0||!seen.Add(names[n]))throw new FormatException("Unknown or duplicate zone: "+names[n]);
                        float weight=Number(weights[n]);if(weight<0)throw new FormatException("Negative weight.");
                        rule.zoneWeights[zone]=weight;
                    }
                    if(!rule.zoneWeights.Any(w=>w>0))throw new FormatException("At least one zone weight must be positive.");
                    result.Add(rule);
                }
                catch(FormatException e){throw new FormatException($"Theme row {i+1}: {e.Message}",e);}
            }
            if(!typed||result.Count==0)throw new FormatException("Theme table needs ##type and data.");
            return result;
        }
        static float Number(string value)
        {
            if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||float.IsNaN(number)||float.IsInfinity(number))throw new FormatException("Invalid number: "+value);
            return number;
        }
    }
}
