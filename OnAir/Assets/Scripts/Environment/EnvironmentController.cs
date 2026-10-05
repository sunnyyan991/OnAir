using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    [DefaultExecutionOrder(300)]
    public sealed class EnvironmentController : MonoBehaviour
    {
        public JourneyController journey;
        public TerrainStreamer world;
        public Camera sceneCamera;
        public Light sun;
        public Light moon{get;private set;}
        static readonly float[] knots={0,.06f,.20f,.36f,.46f,.54f,.65f,.90f,1};
        static Color AmbientAt(TerrainProfile t,int k)=>k==1?t.dawnAmbient:k==2||k==3?t.dayAmbient:k==4?t.duskAmbient:k==6||k==7?t.nightAmbient:new Color(.39f,.48f,.67f);
        static Color SunAt(TerrainProfile t,int k)=>k<=1||k==8?t.dawnSun:k==2||k==3?t.daySun:k==4?t.duskSun:t.nightSun;
        int generation=-1;float lastNight=-1;TerrainProfile lastProfile;
        readonly Dictionary<Material,Material> windowMaterials=new Dictionary<Material,Material>();
        readonly HashSet<Material> ownedMaterials=new HashSet<Material>();
        Quaternion originalSunRotation;bool sunCaptured;
        public int PreparedWindowChunks{get;private set;}
        float cloudCover;
        public float CloudCover=>cloudCover;
        void LateUpdate()=>Refresh(Time.deltaTime);
        public void Refresh(float seconds,bool immediate=false)
        {
            journey.Initialize();var t=journey.Current.terrain.profile;
            if(!sunCaptured){originalSunRotation=sun.transform.rotation;sunCaptured=true;}
            var session=journey.context;
            float phase=session.automaticDaylight?session.solarPhase:session.period==DayPeriod.Day?.25f:session.period==DayPeriod.Dawn?.055f:session.period==DayPeriod.Dusk?.455f:session.period==DayPeriod.BlueHour?.535f:.75f;
            float night=1-Mathf.SmoothStep(0,1,Mathf.Sin(phase*Mathf.PI*2)*3);
            CityParkGeometry.RefreshLights(t.nightWindows*(night*1.4f));
            float targetCloud=session.weather==Weather.Clear?0:session.weather==Weather.Cloudy?.5f:session.weather==Weather.HeavyRain?1:.85f;
            cloudCover=immediate?targetCloud:Mathf.Lerp(cloudCover,targetCloud,1-Mathf.Exp(-seconds*.35f));
            float directTransmission=Mathf.Lerp(1,.08f,cloudCover),ambientTransmission=Mathf.Lerp(1,.94f,cloudCover);
            float mix=immediate?1:1-Mathf.Exp(-seconds*1.8f);
            Color ambient=Color.Lerp(t.dayAmbient,t.nightAmbient,night),light=Color.Lerp(t.daySun,t.nightSun,night);
            float intensity=Mathf.Lerp(t.dayIntensity,t.nightIntensity,night);var rotation=originalSunRotation;
            // Continuous palette knots: dawn, daylight, sunset, blue hour and moonlit night.
            int k=0;while(k<knots.Length-2&&phase>knots[k+1])k++;
            float blend=Mathf.SmoothStep(0,1,Mathf.InverseLerp(knots[k],knots[k+1],phase));ambient=Color.Lerp(AmbientAt(t,k),AmbientAt(t,k+1),blend);light=Color.Lerp(SunAt(t,k),SunAt(t,k+1),blend);
            float solarAltitude=Mathf.Sin(phase*Mathf.PI*2),lunarAltitude=-solarAltitude;
            intensity=t.dayIntensity*Mathf.SmoothStep(0,1,Mathf.Max(0,solarAltitude)*2.5f);
            rotation=Quaternion.Euler(solarAltitude*58,-100+180*Mathf.Clamp01(phase*2),0);
            if(!moon){var obj=new GameObject("Moon light");obj.transform.SetParent(transform,false);moon=obj.AddComponent<Light>();moon.type=LightType.Directional;moon.shadows=sun.shadows;moon.shadowBias=sun.shadowBias;moon.shadowNormalBias=sun.shadowNormalBias;}
            moon.transform.rotation=Quaternion.Euler(lunarAltitude*48,-100+180*Mathf.Clamp01((phase-.5f)*2),0);
            moon.color=t.nightSun;moon.intensity=0;moon.enabled=false;
            if(solarAltitude<0){rotation=moon.transform.rotation;light=t.nightSun;intensity=t.nightIntensity*Mathf.SmoothStep(0,1,lunarAltitude*3);}
            RenderSettings.sun=sun; // Keep URP's main light identity stable across sunrise and sunset.
            sceneCamera.backgroundColor=Color.Lerp(sceneCamera.backgroundColor,Color.Lerp(t.daySky,t.nightSky,night),mix);
            if(t.directionalPeriods)
            {
                RenderSettings.ambientMode=AmbientMode.Trilight;
                RenderSettings.ambientSkyColor=Color.Lerp(RenderSettings.ambientSkyColor,ambient*ambientTransmission,mix);
                RenderSettings.ambientEquatorColor=Color.Lerp(RenderSettings.ambientEquatorColor,ambient*ambientTransmission*.67f,mix);
                RenderSettings.ambientGroundColor=Color.Lerp(RenderSettings.ambientGroundColor,new Color(ambient.r*.34f,ambient.g*.32f,ambient.b*.29f)*ambientTransmission,mix);
            }
            else {RenderSettings.ambientMode=AmbientMode.Flat;RenderSettings.ambientLight=Color.Lerp(RenderSettings.ambientLight,ambient*ambientTransmission,mix);}
            sun.color=Color.Lerp(sun.color,light,mix);sun.intensity=Mathf.Lerp(sun.intensity,intensity*directTransmission,mix);
            // Explicit ambient colours for object-pixel surfaces. URP does not
            // populate the legacy unity_Ambient* shader globals consistently.
            Shader.SetGlobalColor("_PixelSky",RenderSettings.ambientSkyColor);
            Shader.SetGlobalColor("_PixelEquator",RenderSettings.ambientEquatorColor);
            Shader.SetGlobalColor("_PixelGround",RenderSettings.ambientGroundColor);
            // Reconstruct immutable map coordinates after the floating origin moves.
            // Shared materials can then choose different windows for each building.
            Shader.SetGlobalFloat("_PixelOriginZ",journey.Current.index*TerrainStreamer.SegmentLength);
            Shader.SetGlobalFloat("_PixelWorldSeed",journey.seed%1048573);
            Shader.SetGlobalFloat("_WindowSleepProgress",WindowNightSchedule.Progress(session.solarPhase,session.nightSeconds,session.automaticDaylight));
            sun.shadowStrength=Mathf.Lerp(1,.12f,cloudCover);
            sun.transform.rotation=Quaternion.Slerp(sun.transform.rotation,rotation,mix);
            // Continuous tiles prepare their windows once, before becoming visible.
            // Only the compatibility world needs discovery here.
            if(!journey.continuousWorld&&generation!=world.GenerationCount)
            {
                foreach(var chunk in world.Chunks)PrepareWindows(chunk);
                generation=world.GenerationCount;lastNight=-1;
            }
            if(night!=lastNight||t!=lastProfile)
            {
                foreach(var material in ownedMaterials)material.SetColor("_EmissionColor",t.nightWindows*(night*1.4f));
                lastNight=night;lastProfile=t;
            }
        }
        public void PrepareWindows(GameObject chunk)
        {
            PreparedWindowChunks++;
            var t=journey.Current.terrain.profile;var session=journey.context;
            float phase=session.automaticDaylight?session.solarPhase:session.period==DayPeriod.Day?.25f:session.period==DayPeriod.Dawn?.055f:session.period==DayPeriod.Dusk?.455f:session.period==DayPeriod.BlueHour?.535f:.75f;
            float amount=1-Mathf.SmoothStep(0,1,Mathf.Sin(phase*Mathf.PI*2)*3);
            foreach(var building in chunk.GetComponentsInChildren<BuildingView>(true))foreach(var r in building.windows)
            {
                if(!r||!r.sharedMaterial)continue;var source=r.sharedMaterial;
                if(!ownedMaterials.Contains(source))
                {
                    if(!windowMaterials.TryGetValue(source,out var material)){material=new Material(source);material.EnableKeyword("_EMISSION");windowMaterials.Add(source,material);ownedMaterials.Add(material);}
                    r.sharedMaterial=material;
                }
                r.sharedMaterial.SetColor("_EmissionColor",t.nightWindows*(amount*1.4f));
            }
        }
        void OnDestroy(){foreach(var material in ownedMaterials)Destroy(material);}
    }
}
