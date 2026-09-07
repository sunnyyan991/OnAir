using UnityEngine;
namespace OnAir.Prototype
{
    public sealed class WorldContext : MonoBehaviour
    {
        public Biome biome = Biome.City;
        public Weather weather = Weather.Clear;
        public DayPeriod period = DayPeriod.Day;
        public bool paused;
    }
}
