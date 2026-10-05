using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace OnAir
{
    [DefaultExecutionOrder(350)]
    public sealed class AircraftCabinView:MonoBehaviour
    {
        public FlightView view; public Camera sceneCamera;
        public bool IsCabin{get;private set;}
        public Camera CabinCamera{get;private set;}
        public Camera ActiveCamera=>IsCabin&&CabinCamera?CabinCamera:sceneCamera;
        public string LiveryName=>view&&view.Rig&&view.Rig.Livery?view.Rig.Livery.displayName:"Original";
        public const float HazeEnd=240;
        Material sky; Vector3 eye; Color horizon; float night;
        bool savedFog; FogMode savedMode; Color savedColor; float savedStart,savedEnd;
        public void SetCabin(bool value){IsCabin=value;ApplyState();}
        public void NextLivery(){if(view&&view.Rig)view.Rig.ApplyLivery(view.Rig.LiveryIndex+1);}
        void Start()
        {
            if(!view||!view.visual||!sceneCamera){enabled=false;return;}
            var obj=new GameObject("Borderless right wing camera",typeof(Camera));
            obj.transform.SetParent(view.visual,false);CabinCamera=obj.GetComponent<Camera>();CabinCamera.CopyFrom(sceneCamera);
            CabinCamera.orthographic=false;CabinCamera.fieldOfView=58;CabinCamera.nearClipPlane=.06f;CabinCamera.farClipPlane=HazeEnd+40;
            CabinCamera.targetTexture=null;CabinCamera.rect=new Rect(0,0,1,1);CabinCamera.depth=sceneCamera.depth+1;
            CabinCamera.GetUniversalAdditionalCameraData().renderPostProcessing=false;
            eye=view.Rig?view.Rig.cabinEye:new Vector3(.29f,.60f,-.65f);
            obj.transform.localPosition=eye;obj.transform.localRotation=Quaternion.Euler(18,82,0);
            sky=new Material(Shader.Find("OnAir/WingSky"));obj.AddComponent<Skybox>().material=sky;
            CabinCamera.clearFlags=CameraClearFlags.Skybox;
            RenderPipelineManager.beginCameraRendering+=Begin;RenderPipelineManager.endCameraRendering+=End;ApplyState();
        }
        void ApplyState(){if(!CabinCamera)return;sceneCamera.enabled=!IsCabin;CabinCamera.enabled=IsCabin;}
        void Update(){if(Input.GetKeyDown(KeyCode.V))SetCabin(!IsCabin);if(Input.GetKeyDown(KeyCode.L))NextLivery();}
        void LateUpdate()
        {
            if(!CabinCamera)return;
            var session=view.flight.journey.context;
            float phase=session.automaticDaylight?session.solarPhase:session.period==DayPeriod.Day?.25f:session.period==DayPeriod.Dawn?.055f:session.period==DayPeriod.Dusk?.455f:session.period==DayPeriod.BlueHour?.535f:.75f;
            float altitude=Mathf.Sin(phase*Mathf.PI*2);
            night=1-Mathf.SmoothStep(0,1,Mathf.Clamp01((altitude+.12f)/.38f));
            float warm=(1-Mathf.SmoothStep(0,1,Mathf.Abs(altitude)*2.5f))*(1-night);
            Color zenith=Color.Lerp(new Color(.26f,.53f,.79f),new Color(.025f,.045f,.10f),night);
            horizon=Color.Lerp(new Color(.68f,.79f,.86f),new Color(.07f,.10f,.17f),night);
            horizon=Color.Lerp(horizon,new Color(.80f,.58f,.45f),warm*.7f);
            sky.SetColor("_SkyColor",zenith);sky.SetColor("_HorizonColor",horizon);sky.SetFloat("_Night",night);
        }
        Color Horizon()=>horizon;
        void Begin(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=CabinCamera)return;
            savedFog=RenderSettings.fog;savedMode=RenderSettings.fogMode;savedColor=RenderSettings.fogColor;
            savedStart=RenderSettings.fogStartDistance;savedEnd=RenderSettings.fogEndDistance;
            RenderSettings.fog=true;RenderSettings.fogMode=FogMode.Linear;RenderSettings.fogColor=Horizon();
            RenderSettings.fogStartDistance=205;RenderSettings.fogEndDistance=HazeEnd;
        }
        void End(ScriptableRenderContext context,Camera camera)
        {
            if(camera!=CabinCamera)return;
            RenderSettings.fog=savedFog;RenderSettings.fogMode=savedMode;RenderSettings.fogColor=savedColor;
            RenderSettings.fogStartDistance=savedStart;RenderSettings.fogEndDistance=savedEnd;
        }
        void OnDestroy(){RenderPipelineManager.beginCameraRendering-=Begin;RenderPipelineManager.endCameraRendering-=End;if(sceneCamera)sceneCamera.enabled=true;if(CabinCamera)Destroy(CabinCamera.gameObject);if(sky)Destroy(sky);}
    }
}
