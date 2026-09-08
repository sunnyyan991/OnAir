using System;
namespace OnAir
{
    // Runtime association of one table row with its authored visual/resources profile.
    public sealed class TerrainDefinition
    {
        public readonly TerrainRule rule;
        public readonly TerrainProfile profile;
        public TerrainDefinition(TerrainRule rule,TerrainProfile profile){this.rule=rule;this.profile=profile;}
        public string id=>rule.id;
        public Biome biome=>rule.biome;
        public float distanceMeters=>rule.distanceMeters;
        public float selectionWeight=>rule.selectionWeight;
        public float buildingDensity=>rule.buildingDensity;
        public WeatherWeight[] weather=>rule.weather;
        public bool Allows(Weather value)=>Array.Exists(weather,w=>w.weather==value&&w.weight>0);
        public Weather PickWeather(Random random)
        {
            double sum=0;foreach(var option in weather)sum+=option.weight;
            double ticket=random.NextDouble()*sum;Weather fallback=default;
            foreach(var option in weather)
            {
                if(option.weight<=0)continue;fallback=option.weather;ticket-=option.weight;
                if(ticket<0)return option.weather;
            }
            return fallback;
        }
    }
}
