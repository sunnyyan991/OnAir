using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Steady navigation lenses, independent white strobes and an engine warning
    // beacon. No shadow-casting point lights are added to the aircraft.
    public sealed class AircraftLights:MonoBehaviour
    {
        FlightView view;
        AirportVisitController airport;
        AirportVisitPlan lightPlan;
        int planSeed=int.MinValue;
        float planSlope;
        Material red,green,white;
        Renderer leftPosition,rightPosition,tailPosition,leftFlash,rightFlash,beacon;
        float clock,beaconClock;
        public bool FlashOn{get;private set;}
        public bool BeaconOn{get;private set;}
        public bool StrobesEnabled{get;private set;}
        public bool BeaconEnabled{get;private set;}
        public bool PositionLightsEnabled{get;private set;}
        public float PulsePhase=>clock;
        public const float RunwayClearanceRadius=6.5f;
        void Start()
        {
            view=GetComponent<FlightView>();ResolveAirport();
            if(!view||!view.visual)return;
            red=LensMaterial(new Color(1,.04f,.015f));green=LensMaterial(new Color(.02f,1,.18f));white=LensMaterial(Color.white);
            view.AircraftChanged+=Build;Build();
        }
        // Lenses live under the current visual and are replaced with it.
        void Build()
        {
            if(!view||!view.visual||!red)return;
            var rig=view.Rig;
            if(rig)
            {
                var v=view.visual;
                leftPosition=Lens("Left red position",v,rig.leftNavigation.localPosition,.22f,red);
                rightPosition=Lens("Right green position",v,rig.rightNavigation.localPosition,.22f,green);
                tailPosition=Lens("Tail white position",v,rig.tailNavigation.localPosition,.20f,white);
                leftFlash=Lens("Left white strobe",v,rig.leftStrobe.localPosition,.38f,white);
                rightFlash=Lens("Right white strobe",v,rig.rightStrobe.localPosition,.38f,white);
                beacon=Lens("Red engine beacon",v,rig.beacon.localPosition,.28f,red);Tick(0);return;
            }
            // Metric small-aircraft prefabs carry named FlightAnchors (metres, model space).
            var anchors=Find(view.visual,"FlightAnchors");
            var left=anchors?anchors.Find("LeftNavigation"):null;var right=anchors?anchors.Find("RightNavigation"):null;var tail=anchors?anchors.Find("TailNavigation"):null;
            if(left&&right&&tail)
            {
                var back=new Vector3(0,0,-.3f);
                leftPosition=Lens("Left red position",anchors,left.localPosition,.2f,red);
                rightPosition=Lens("Right green position",anchors,right.localPosition,.2f,green);
                tailPosition=Lens("Tail white position",anchors,tail.localPosition,.18f,white);
                leftFlash=Lens("Left white strobe",anchors,left.localPosition+back,.3f,white);
                rightFlash=Lens("Right white strobe",anchors,right.localPosition+back,.3f,white);
                // Beacon on top of the tail: the T-tail fairing when present, else the fin.
                var fin=Find(view.visual,"TailplaneFairing");if(!fin)fin=Find(view.visual,"VerticalFin");
                var mesh=fin?fin.GetComponent<MeshFilter>():null;
                var bounds=mesh&&mesh.sharedMesh?mesh.sharedMesh.bounds:new Bounds(tail.localPosition,Vector3.zero);
                beacon=Lens("Red engine beacon",anchors,new Vector3(0,bounds.max.y+.05f,bounds.center.z),.22f,red);Tick(0);return;
            }
            var w=view.visual;
            leftPosition=Lens("Left red position",w,new Vector3(-1.79f,.035f,-.05f),.065f,red);
            rightPosition=Lens("Right green position",w,new Vector3(1.79f,.035f,-.05f),.065f,green);
            tailPosition=Lens("Tail white position",w,new Vector3(0,.09f,-1.39f),.045f,white);
            leftFlash=Lens("Left white strobe",w,new Vector3(-1.82f,.045f,-.17f),.11f,white);
            rightFlash=Lens("Right white strobe",w,new Vector3(1.82f,.045f,-.17f),.11f,white);
            beacon=Lens("Red engine beacon",w,new Vector3(0,.39f,-.35f),.075f,red);Tick(0);
        }
        static Transform Find(Transform root,string name)
        {
            if(root.name==name)return root;
            foreach(Transform child in root){var hit=Find(child,name);if(hit)return hit;}
            return null;
        }
        Material LensMaterial(Color color)
        {
            // Keep the visual's referenced shader; it remains present in player builds.
            var m=new Material(view.visual.GetComponentInChildren<MeshRenderer>().sharedMaterial){name="Aircraft navigation lens"};
            m.SetColor("_BaseColor",color);m.EnableKeyword("_EMISSION");m.SetColor("_EmissionColor",color*2);return m;
        }
        Renderer Lens(string label,Transform parent,Vector3 position,float size,Material material)
        {
            var obj=GameObject.CreatePrimitive(PrimitiveType.Sphere);obj.name=label;obj.transform.SetParent(parent,false);obj.transform.localPosition=position;obj.transform.localScale=Vector3.one*size;
            var collider=obj.GetComponent<Collider>();if(Application.isPlaying)Destroy(collider);else DestroyImmediate(collider);
            var r=obj.GetComponent<Renderer>();r.sharedMaterial=material;r.shadowCastingMode=ShadowCastingMode.Off;r.receiveShadows=false;return r;
        }
        void Update()
        {
            if(!view||!view.flight||!view.flight.journey)return;
            bool paused=view.flight.journey.context&&view.flight.journey.context.SimulationPaused;
            Tick(paused?0:Time.deltaTime);
        }
        public static bool ShouldUseStrobes(AirportPhase phase,bool occupyingRunway,bool airportActive=true)
        {
            if(!airportActive)return true;
            switch(phase)
            {
                case AirportPhase.Cruise:case AirportPhase.Approach:case AirportPhase.LandingRoll:
                case AirportPhase.TakeoffRoll:case AirportPhase.Climb:case AirportPhase.Complete:return true;
                case AirportPhase.TaxiIn:case AirportPhase.TaxiOut:case AirportPhase.RunwayHold:return occupyingRunway;
                default:return false;
            }
        }
        public static bool ShouldUseBeacon(AirportPhase phase,bool engineRunning)
            =>engineRunning&&phase!=AirportPhase.BackIntoHangar&&phase!=AirportPhase.Maintenance;
        public static bool OccupiesRunway(AirportVisitPlan.Site site,Vector3 worldPosition)
            =>site!=null&&SmallAirportNetwork.RunwayOccupied(SmallAirportNetwork.Source(site,worldPosition),RunwayClearanceRadius);
        bool RunwayOccupied()
        {
            if(!airport||!airport.Active||!view||!view.flight||!view.flight.journey)return false;
            int seed=view.flight.journey.seed;float slope=view.flight.routeSlope;
            if(lightPlan==null||seed!=planSeed||slope!=planSlope)
            {lightPlan=new AirportVisitPlan(seed,slope);planSeed=seed;planSlope=slope;}
            return OccupiesRunway(lightPlan.Near(airport.Position.z),airport.Position);
        }
        void ResolveAirport()
        {
            // GameEntry owns the airport controller on the Game object. The
            // aircraft's FlightController holds its authoritative connection.
            var flight=view&&view.flight?view.flight:GetComponent<FlightController>();
            airport=flight?flight.airport:null;
        }
        public void Tick(float seconds)
        {
            if(!view)view=GetComponent<FlightView>();ResolveAirport();
            seconds=Mathf.Max(0,seconds);clock=Mathf.Repeat(clock+seconds,1.35f);beaconClock=Mathf.Repeat(beaconClock+seconds,1.2f);
            bool active=airport&&airport.Active;
            var phase=active?airport.Phase:AirportPhase.Cruise;
            bool running=active?airport.EngineActivity>.01f:!view||!view.flight||view.flight.EngineActivity>.01f;
            StrobesEnabled=ShouldUseStrobes(phase,RunwayOccupied(),active);
            BeaconEnabled=ShouldUseBeacon(phase,running);
            PositionLightsEnabled=phase!=AirportPhase.Maintenance;
            FlashOn=StrobesEnabled&&(clock<.07f||(clock>.17f&&clock<.24f));
            BeaconOn=BeaconEnabled&&beaconClock>.58f&&beaconClock<.74f;
            if(leftFlash)leftFlash.enabled=FlashOn;if(rightFlash)rightFlash.enabled=FlashOn;if(beacon)beacon.enabled=BeaconOn;
            if(leftPosition)leftPosition.enabled=PositionLightsEnabled;if(rightPosition)rightPosition.enabled=PositionLightsEnabled;if(tailPosition)tailPosition.enabled=PositionLightsEnabled;
        }
        void Release(Material material)
        {if(!material)return;if(Application.isPlaying)Destroy(material);else DestroyImmediate(material);}
        void OnDestroy(){if(view)view.AircraftChanged-=Build;Release(red);Release(green);Release(white);}
    }
}
