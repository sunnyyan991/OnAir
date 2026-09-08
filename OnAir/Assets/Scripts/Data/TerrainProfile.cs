using UnityEngine;
namespace OnAir
{
    [CreateAssetMenu(menuName="OnAir/Terrain Profile")]
    public sealed class TerrainProfile : ScriptableObject
    {
        public string id;
        public WorldKit kit;
        public Material ground;
        public GameObject scenery;
        public Color daySky,nightSky,dayAmbient,nightAmbient,daySun,nightSun,nightWindows;
        public float dayIntensity=1.1f,nightIntensity=.12f;
    }
}
