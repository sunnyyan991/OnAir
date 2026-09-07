using UnityEngine;
namespace OnAir.Prototype
{
    public sealed class EnvironmentView : MonoBehaviour
    {
        public WorldContext context;
        public Camera sceneCamera;
        public BuildingSpawner world;
        Material ground;
        void Start()
        {
            var weatherFx = GetComponent<WeatherParticles>();
            if (!weatherFx) weatherFx = gameObject.AddComponent<WeatherParticles>();
            weatherFx.context = context;
            weatherFx.sceneCamera = sceneCamera;
            ground = new Material(world.groundMaterial); world.groundMaterial = ground;
            foreach (var renderer in world.GetComponentsInChildren<MeshRenderer>()) renderer.sharedMaterial = ground;
        }
        void Update()
        {
            Color color = context.biome == Biome.City ? new Color(.32f,.46f,.41f) : context.biome == Biome.Coast ? new Color(.28f,.49f,.56f) : new Color(.69f,.51f,.32f);
            float light = context.period == DayPeriod.Night ? .35f : context.period == DayPeriod.Day ? 1f : .75f;
            if (context.weather != Weather.Clear) light *= .78f;
            if (ground) ground.SetColor("_BaseColor", Color.Lerp(ground.GetColor("_BaseColor"),color*light,Time.deltaTime*2));
            sceneCamera.backgroundColor = Color.Lerp(sceneCamera.backgroundColor,new Color(.12f,.2f,.28f)*light,Time.deltaTime*2);
        }
        void OnDestroy() { if (ground) Destroy(ground); }
    }
}
