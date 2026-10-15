using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    public sealed class AirportVisitController : MonoBehaviour
    {
        public GameEntry game;
        public bool TestMode { get; private set; }
        public float TestSpeed { get; private set; }=1;
        public bool Active => motion!=null&&motion.Phase!=AirportPhase.Complete;
        public Vector3 Position => motion?.State.position??Vector3.zero;
        public Vector3 Velocity { get; private set; }
        public float EngineActivity => Phase==AirportPhase.Maintenance||Phase==AirportPhase.BackIntoHangar?0:Phase==AirportPhase.TaxiIn||Phase==AirportPhase.TaxiOut||Phase==AirportPhase.RunwayHold?.3f:1;
        public AirportPhase Phase => motion?.Phase??AirportPhase.Cruise;
        public string Status => Phase.ToString()+((Phase==AirportPhase.TaxiIn||Phase==AirportPhase.TaxiOut||Phase==AirportPhase.Maintenance)?" / "+SmallAirportNetwork.Bays[motion.Site.bay].name:"");
        public float TaxiRemaining => motion?.RemainingDistance??0;
        public float VisualPitch => motion?.VisualPitch??0;
        public float Remaining => Phase==AirportPhase.Maintenance?Mathf.Max(0,900-motion.State.elapsed):Phase==AirportPhase.RunwayHold?Mathf.Max(0,10-motion.State.elapsed):0;
        public int Runway => motion?.Site.runway??loadedSite?.runway??-1;
        AirportVisitPlan plan;
        AirportFlightLayout layout;
        AirportVisitMotion motion;
        AirportVisitState saved=new AirportVisitState();
        int seed=int.MinValue,teleport=-1;
        GameObject sceneRoot;
        AirportVisitPlan.Site loadedSite;
        AirportServiceScene service;
        AirportHangarRoof hangarRoof;
        AirportGroundLights groundLights;
        float lightingClock;
        readonly List<Light> staticLights=new List<Light>();
        readonly List<float> staticLightLevels=new List<float>();
        readonly List<Material> nightMaterials=new List<Material>();
        readonly List<Color> nightColors=new List<Color>();
        float lastNight=-1;
        public AirportVisitState Capture()
        {
            var state=motion?.State??saved;state.seed=game.journey.seed;
            return JsonUtility.FromJson<AirportVisitState>(JsonUtility.ToJson(state));
        }
        public void Restore(AirportVisitState state)
        {
            bool test=TestMode;float rate=TestSpeed;Reset(test);TestSpeed=rate;Prepare();saved=state??new AirportVisitState();
            if(saved.siteId>=0&&saved.seed==seed&&saved.phase>0&&saved.phase<(int)AirportPhase.Complete)
            {var site=plan.Get(saved.siteId);if(site!=null){motion=new AirportVisitMotion(site,saved);Load(site);ApplyPose();}}
        }
        public void Reset(bool test=false)
        {
            motion=null;saved=new AirportVisitState();Velocity=Vector3.zero;ClearScene();seed=int.MinValue;teleport=-1;TestMode=test;TestSpeed=1;
        }
        void Prepare()
        {
            if(seed==game.journey.seed&&plan!=null)return;
            seed=game.journey.seed;plan=new AirportVisitPlan(seed,game.flight.routeSlope);layout=Resources.Load<AirportFlightLayout>("Airport/FlightLayout");
            teleport=game.journey.TeleportRevision;
        }
        public bool JumpToTest()
        {
            Reset(true);Prepare();var next=plan.Next((game.journey.Current.index+game.journey.Progress)*64);
            if(next==null)return false;
            // Align with the physical aircraft, whose cruise visual is offset along the camera ray.
            game.journey.JumpForwardWorld(next.start-layout.cruiseVisualOffset.y+1);
            teleport=game.journey.TeleportRevision;
            game.flight.Tick(0);game.cameraFollow.Follow(0);return true;
        }
        public bool TryTick(float seconds)
        {
            if(!game||!game.journey)return false;
            if(seed!=int.MinValue&&seed!=game.journey.seed)Reset(TestMode);
            Prepare();if(!plan.Available)return false;
            if(teleport!=game.journey.TeleportRevision)
            {motion=null;Velocity=Vector3.zero;teleport=game.journey.TeleportRevision;}
            float z=(game.journey.Current.index+game.journey.Progress)*64;
            if(!Active)
            {
                var site=plan.Near(z,600);
                if(site!=null)Load(site);
                else if(sceneRoot)ClearScene();
                float physicalZ=z+layout.cruiseVisualOffset.y;
                if(site==null||site.id==saved.completedSite||physicalZ<site.start||physicalZ>site.LandingZ-30)return false;
                var view=game.flight.GetComponent<FlightView>();
                float ground=.9f;
                if(view&&view.visual)
                {
                    float bottom=0;foreach(var mf in view.visual.GetComponentsInChildren<MeshFilter>())
                    {if(!mf.sharedMesh)continue;var b=mf.sharedMesh.bounds;for(int i=0;i<8;i++){var p=b.center+Vector3.Scale(b.extents,new Vector3((i&1)==0?-1:1,(i&2)==0?-1:1,(i&4)==0?-1:1));bottom=Mathf.Min(bottom,mf.transform.TransformPoint(p).y-view.visual.position.y);}}
                    ground=-bottom+.24f;
                }
                var physical=new Vector3(game.flight.RouteX(z)+layout.cruiseVisualOffset.x,130,physicalZ);
                // Last rendered position includes sway. Keep it at the start of the blend.
                if(seconds>0&&view&&view.visual)physical=view.visual.position+Vector3.forward*(game.journey.Current.index*64);
                var state=new AirportVisitState{siteId=site.id,seed=seed,phase=(int)AirportPhase.Approach,entry=physical,position=physical,heading=Vector3.forward,groundHeight=ground,cruiseSpeed=Mathf.Max(.25f,game.flight.CruiseWorldSpeed),speed=Mathf.Max(.25f,game.flight.CruiseWorldSpeed),completedSite=saved.completedSite};
                motion=new AirportVisitMotion(site,state);
            }
            // Waiting follows the same game-time clock as daylight, even at low FPS.
            float elapsed=(Phase==AirportPhase.Maintenance||Phase==AirportPhase.RunwayHold)?Mathf.Max(0,seconds):Mathf.Clamp(seconds,0,.05f);
            float dt=game.session.SimulationPaused?0:elapsed*(TestMode?TestSpeed:1);
            var previous=Position;var oldPhase=Phase;
            // Small integration steps keep accelerated tests equivalent to real-time motion.
            for(float left=dt;left>0;){float step=Mathf.Min(.02f,left);motion.Tick(step);left-=step;}
            Velocity=seconds>0?(Position-previous)/seconds:Vector3.zero;
            if(oldPhase==AirportPhase.Approach&&Phase!=oldPhase)service?.Touchdown(Position);
            if(Phase==AirportPhase.Complete)
            {
                saved=motion.State;float exit=Position.z-layout.cruiseVisualOffset.y;
                motion=null;game.flight.FinishAirportVisit();game.journey.JumpForwardWorld(exit);teleport=game.journey.TeleportRevision;Velocity=Vector3.zero;return false;
            }
            ApplyPose();return true;
        }
        void ApplyPose()=>game.flight.ApplyAirportPose(Position,motion.State.heading,game.sceneCamera);
        void Load(AirportVisitPlan.Site site)
        {
            if(sceneRoot&&loadedSite.id==site.id)return;
            ClearScene();loadedSite=site;sceneRoot=new GameObject("City Airport "+site.id+" / runway "+site.RunwayName);
            var airport=Instantiate(layout.airport,sceneRoot.transform,false);airport.name="SmallField reusable airport";airport.transform.SetLocalPositionAndRotation(site.assetPosition,site.assetRotation);
            var serviceObject=new GameObject("General aviation service and approach city");serviceObject.transform.SetParent(sceneRoot.transform,false);service=serviceObject.AddComponent<AirportServiceScene>();service.Build(site,layout);
            staticLights.AddRange(airport.GetComponentsInChildren<Light>());
            foreach(var light in staticLights)staticLightLevels.Add(light.intensity);
            var replacements=new Dictionary<Material,Material>();
            foreach(var r in airport.GetComponentsInChildren<Renderer>())
            {
                var m=r.sharedMaterial;if(!m)continue;
                float strength=NightEmissionScale(r);if(strength<=0)continue;
                if(!replacements.TryGetValue(m,out var replacement))
                {replacement=new Material(m);replacement.EnableKeyword("_EMISSION");replacements[m]=replacement;nightMaterials.Add(replacement);nightColors.Add(m.GetColor("_BaseColor")*strength);}
                r.sharedMaterial=replacement;
            }
            hangarRoof=sceneRoot.AddComponent<AirportHangarRoof>();hangarRoof.Build(airport.transform,site,Phase,Position);
            groundLights=sceneRoot.AddComponent<AirportGroundLights>();groundLights.Build(airport.transform);
            lightingClock=0;
            lastNight=-1;UpdateOrigin();
        }
        // Classify actual fixture/window materials. "GrassLight", "WarmWood"
        // and the parked aircraft's "Warm ivory" are ordinary lit surfaces.
        public static float NightEmissionScale(Renderer renderer)
        {
            if(!renderer||!renderer.sharedMaterial)return 0;
            for(var ancestor=renderer.transform.parent;ancestor;ancestor=ancestor.parent)
                if(ancestor.name=="AirfieldLights")return 0;
            switch(renderer.sharedMaterial.name)
            {
                case "SmallField WarmWindow":return 1.4f;
                case "SmallField RedLamp":return 1.6f;
                case "SmallField Glass":
                    for(var parent=renderer.transform.parent;parent;parent=parent.parent)
                        if(parent.name.StartsWith("SmallField-terminal-",System.StringComparison.Ordinal))return .35f;
                    return 0;
                default:return 0;
            }
        }
        void UpdateOrigin(){if(sceneRoot)sceneRoot.transform.position=Vector3.back*(game.journey.Current.index*64);}
        void LateUpdate()=>UpdatePresentation(Mathf.Max(0,Time.deltaTime));
        void UpdatePresentation(float seconds)
        {
            if(!game||game.journey.Current==null)return;
            if(sceneRoot)
            {
                bool visible=game.flight.gameObject.activeInHierarchy;sceneRoot.SetActive(visible);if(!visible)return;UpdateOrigin();
                bool paused=game.session.SimulationPaused;service.Refresh(Phase,Position,paused?0:Mathf.Min(seconds,.05f),paused,motion?.GroundService);
                float night=1-Mathf.SmoothStep(0,1,Mathf.Sin(game.session.solarPhase*Mathf.PI*2)*3);
                if(Mathf.Abs(night-lastNight)>.01f){for(int i=0;i<nightMaterials.Count;i++)nightMaterials[i].SetColor("_EmissionColor",nightColors[i]*night);service.RefreshNight(night);for(int i=0;i<staticLights.Count;i++)if(staticLights[i])staticLights[i].intensity=night*staticLightLevels[i];lastNight=night;}
                if(!paused)lightingClock+=seconds;
                groundLights.Refresh(night,lightingClock,loadedSite,Position,Phase);
                hangarRoof.Refresh(Phase,Position,seconds,paused);
            }
        }
        public void CycleTestSpeed(){if(TestMode)TestSpeed=TestSpeed==1?4:TestSpeed==4?16:1;}
        public void RefreshPresentation()=>UpdatePresentation(0);
        public void SkipService(){if(TestMode)motion?.SkipMaintenance();}
        void ClearScene()
        {
            if(sceneRoot){sceneRoot.SetActive(false);Release(sceneRoot);}sceneRoot=null;service=null;hangarRoof=null;groundLights=null;loadedSite=null;
            foreach(var m in nightMaterials)Release(m);nightMaterials.Clear();nightColors.Clear();staticLights.Clear();staticLightLevels.Clear();
        }
        void Release(Object value){if(!value)return;if(Application.isPlaying)Destroy(value);else DestroyImmediate(value);}
        void OnDestroy()=>ClearScene();
    }
}
