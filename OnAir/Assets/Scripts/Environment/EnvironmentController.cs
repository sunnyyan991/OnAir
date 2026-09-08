using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    public sealed class EnvironmentController : MonoBehaviour
    {
        public JourneyController journey;
        public TerrainStreamer world;
        public Camera sceneCamera;
        public Light sun;
        int generation=-1;float lastNight=-1;TerrainProfile lastProfile;
        readonly List<Renderer> windows=new List<Renderer>();
        readonly Dictionary<Material,Material> windowMaterials=new Dictionary<Material,Material>();
        readonly HashSet<Material> ownedMaterials=new HashSet<Material>();
        MaterialPropertyBlock properties;
        void Start(){properties=new MaterialPropertyBlock();RenderSettings.ambientMode=AmbientMode.Flat;}
        void Update()=>Refresh(Time.deltaTime);
        public void Refresh(float seconds)
        {
            journey.Initialize();var t=journey.Current.terrain.profile;
            float night=journey.context.period==DayPeriod.Night?1:journey.context.period==DayPeriod.Day?0:.45f;
            float weather=journey.context.weather==Weather.Clear?1:journey.context.weather==Weather.Cloudy?.82f:.65f;
            float mix=1-Mathf.Exp(-seconds*1.8f);
            sceneCamera.backgroundColor=Color.Lerp(sceneCamera.backgroundColor,Color.Lerp(t.daySky,t.nightSky,night),mix);
            RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,Color.Lerp(t.dayAmbient,t.nightAmbient,night)*weather,mix);
            sun.color=Color.Lerp(sun.color,Color.Lerp(t.daySun,t.nightSun,night),mix);
            sun.intensity=Mathf.Lerp(sun.intensity,Mathf.Lerp(t.dayIntensity,t.nightIntensity,night)*weather,mix);
            if(generation!=world.GenerationCount)
            {
                windows.Clear();foreach(var chunk in world.Chunks)foreach(var building in chunk.GetComponentsInChildren<BuildingView>())foreach(var r in building.windows)
                {
                    if(!r)continue;
                    var source=r.sharedMaterial;
                    if(!ownedMaterials.Contains(source))
                    {
                        if(!windowMaterials.TryGetValue(source,out var material))
                        {
                            material=new Material(source);material.EnableKeyword("_EMISSION");windowMaterials.Add(source,material);ownedMaterials.Add(material);
                        }
                        r.sharedMaterial=material;
                    }
                    windows.Add(r);
                }
                generation=world.GenerationCount;lastNight=-1;
            }
            if(night!=lastNight||t!=lastProfile)
            {
                properties.SetColor("_EmissionColor",t.nightWindows*(night*1.4f));foreach(var window in windows)if(window)window.SetPropertyBlock(properties);
                lastNight=night;lastProfile=t;
            }
        }
        void OnDestroy(){foreach(var material in ownedMaterials)Destroy(material);}
    }
}
