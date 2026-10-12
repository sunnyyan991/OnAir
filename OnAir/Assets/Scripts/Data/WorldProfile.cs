using UnityEngine;
namespace OnAir
{
    // Global settings of the continuous world: route distance scale, building kit and day/night palette.
    [CreateAssetMenu(menuName="OnAir/World Profile")]
    public sealed class WorldProfile : ScriptableObject
    {
        [Tooltip("Route meters represented by one 64-unit world segment. Drives flight speed and streaming distance.")]
        [Min(1)] public float segmentMeters=320;
        public WorldKit kit;
        public Color daySky,nightSky,dayAmbient,nightAmbient,daySun,nightSun,nightWindows;
        public float dayIntensity=1.1f,nightIntensity=.12f;
        [Header("Directional sunlight by period (optional)")]
        public bool directionalPeriods;
        public Color dawnSun=new Color(1,.85f,.69f),duskSun=new Color(1,.79f,.59f);
        public Color dawnAmbient=new Color(.64f,.68f,.75f),duskAmbient=new Color(.60f,.65f,.73f);
        public float dawnIntensity=.85f,duskIntensity=.95f;
        public Vector3 dawnDirection=new Vector3(20,-95,0),dayDirection=new Vector3(58,-35,0),duskDirection=new Vector3(16,85,0),nightDirection=new Vector3(35,-20,0);
    }
}
