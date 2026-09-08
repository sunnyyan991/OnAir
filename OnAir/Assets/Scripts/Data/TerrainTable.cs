using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
namespace OnAir
{
    public sealed class TerrainRule
    {
        public string id;
        public Biome biome;
        public float distanceMeters,selectionWeight,buildingDensity;
        public WeatherWeight[] weather;
    }
    public struct WeatherWeight { public Weather weather; public float weight; }
    public static class TerrainTable
    {
        public static List<TerrainRule> Parse(string text)
        {
            var rows=CsvReader.Read(text.TrimStart('\uFEFF'));
            const string columns="##var,id,biome,distance_meters,selection_weight,building_density,weather,weather_weights";
            string[] types={"##type","string","Biome","float","float","float","(list#sep=|),Weather","(list#sep=|),float"};
            if(rows.Count==0||string.Join(",",rows[0])!=columns)throw new FormatException("Terrain table: unexpected columns.");
            var result=new List<TerrainRule>();var ids=new HashSet<string>();bool typed=false;
            for(int i=1;i<rows.Count;i++)
            {
                var row=rows[i];if(row.All(string.IsNullOrWhiteSpace))continue;
                if(row[0]=="##type"){if(typed||!row.SequenceEqual(types))throw new FormatException("Terrain table: invalid ##type.");typed=true;continue;}
                if(row[0].StartsWith("#",StringComparison.Ordinal))continue;
                try
                {
                    if(row.Length!=8||row[0]!="")throw new FormatException("Expected an empty marker and seven fields.");
                    if(row[1].Length==0||row[1].Any(c=>c<'0'||c>'9')||!ids.Add(row[1]))throw new FormatException("id must be a unique digit string.");
                    if(!Enum.GetNames(typeof(Biome)).Contains(row[2]))throw new FormatException("Unknown biome.");
                    var rule=new TerrainRule{id=row[1],biome=(Biome)Enum.Parse(typeof(Biome),row[2]),distanceMeters=Number(row[3]),selectionWeight=Number(row[4]),buildingDensity=Number(row[5])};
                    if(rule.distanceMeters<=0||rule.selectionWeight<=0||rule.buildingDensity<0||rule.buildingDensity>1)throw new FormatException("Distance/selection weight must be positive; density must be 0..1.");
                    var names=row[6].Split('|');var weights=row[7].Split('|');
                    if(names.Length!=weights.Length)throw new FormatException("Weather and weights must have equal lengths.");
                    var seen=new HashSet<Weather>();rule.weather=new WeatherWeight[names.Length];
                    for(int n=0;n<names.Length;n++)
                    {
                        if(!Enum.GetNames(typeof(Weather)).Contains(names[n]))throw new FormatException("Unknown weather: "+names[n]);
                        var weather=(Weather)Enum.Parse(typeof(Weather),names[n]);float weight=Number(weights[n]);
                        if(weight<0||!seen.Add(weather))throw new FormatException("Negative weight or duplicate weather.");
                        rule.weather[n]=new WeatherWeight{weather=weather,weight=weight};
                    }
                    if(!rule.weather.Any(w=>w.weight>0))throw new FormatException("At least one weather weight must be positive.");
                    result.Add(rule);
                }
                catch(FormatException e){throw new FormatException($"Terrain row {i+1}: {e.Message}",e);}
            }
            if(!typed||result.Count==0)throw new FormatException("Terrain table needs ##type and data.");
            return result;
        }
        static float Number(string value)
        {
            if(!float.TryParse(value,NumberStyles.Float,CultureInfo.InvariantCulture,out var number)||float.IsNaN(number)||float.IsInfinity(number))throw new FormatException("Invalid number: "+value);
            return number;
        }
    }
}
