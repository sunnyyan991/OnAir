using UnityEngine;
namespace OnAir
{
    public sealed class GameSession : MonoBehaviour
    {
        public Biome biome = Biome.City;
        public Weather weather = Weather.Clear;
        public DayPeriod period = DayPeriod.Day;
        public bool paused;
        public event System.Action EnvironmentChanged;
        public void EnterTerrain(Biome terrain,Weather entryWeather)
        {
            biome=terrain;weather=entryWeather;EnvironmentChanged?.Invoke();
        }
        public void SetWeather(Weather value){weather=value;EnvironmentChanged?.Invoke();}
        public void CyclePeriod(){period=(DayPeriod)(((int)period+1)%System.Enum.GetValues(typeof(DayPeriod)).Length);EnvironmentChanged?.Invoke();}
        public void TogglePause()=>paused=!paused;
    }
}
