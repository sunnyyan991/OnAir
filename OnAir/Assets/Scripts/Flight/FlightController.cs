using UnityEngine;
namespace OnAir
{
    [DefaultExecutionOrder(-100)]
    public sealed class FlightController:MonoBehaviour
    {
        public JourneyController journey;
        [Min(0)]public float speedMetersPerSecond=24;
        public float routeSlope;
        public float RouteX(float z)=>routeSlope*z;
        public void SetScreenHeading(Camera camera,float degrees)
        {
            var r=camera.transform.right;var u=camera.transform.up;
            // Keep the existing screen quadrant; the unsigned angle also has a near-sideways solution.
            float t=Mathf.Sign(u.z/r.z)*Mathf.Tan(degrees*Mathf.Deg2Rad);
            routeSlope=(t*r.z-u.z)/(u.x-t*r.x);
        }
        [Min(1)]public float altitude=19;
        public float ObstacleCeiling{get;private set;}
        public void ConfigureClearance(BuildingData buildings)=>ConfigureClearance(buildings.Prefabs.Values);
        public void ConfigureClearance(System.Collections.Generic.IEnumerable<GameObject> prefabs)
        {
            float top=0;
            foreach(var prefab in prefabs)
            {
                var footprint=prefab.GetComponent<BuildingFootprint>();if(footprint)top=Mathf.Max(top,footprint.height);
                foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))top=Mathf.Max(top,renderer.bounds.max.y-prefab.transform.position.y);
            }
            ObstacleCeiling=top+1;altitude=Mathf.Max(65,ObstacleCeiling+30);
        }
        [Min(0)]public float swayAmplitude=1.2f;
        [Min(.01f)]public float swayFrequency=.12f;
        [Min(0)]public float bobAmplitude=.15f;
        // Serialized compatibility only; orbit behavior has been removed.
        [HideInInspector]public bool autoCircle=false;
        [HideInInspector]public float firstCircleAfter=10,circleInterval=22,circleRadius=4.5f;
        public bool IsCircling=>false;
        public float CircleProgress=>0;
        public Vector3 Heading{get;private set;}=Vector3.forward;
        public float Bank{get;private set;}
        float flightTime;int teleport=-1;
        ContinuousWorldPlan landscape;
        float nextCacheTrim;
        float groundClearance,clearanceVelocity;
        float aircraftRadius=8;
        public void ConfigureAircraft(AircraftRig rig){if(rig)aircraftRadius=.5f*Mathf.Sqrt(rig.length*rig.length+rig.wingspan*rig.wingspan)+2;}
        public float CruiseHeight=>altitude+groundClearance;
        float WorldSpeed=>Mathf.Max(0,speedMetersPerSecond)*TerrainStreamer.SegmentLength/journey.Current.terrain.distanceMeters;
        void Start()=>Tick(0);
        void Update()=>Tick(Mathf.Min(Time.deltaTime,.05f));
        public void Tick(float seconds)
        {
            if(!journey)return;journey.Initialize();
            if(landscape==null||landscape.seed!=journey.seed||landscape.routeSlope!=routeSlope)landscape=new ContinuousWorldPlan(journey.seed,routeSlope);
            if(Time.unscaledTime>=nextCacheTrim){landscape.TrimCaches();nextCacheTrim=Time.unscaledTime+2;}
            if(teleport!=journey.TeleportRevision){teleport=journey.TeleportRevision;flightTime=0;Bank=0;clearanceVelocity=0;}
            if(!journey.context.paused&&seconds>0&&!float.IsNaN(seconds)&&!float.IsInfinity(seconds)&&WorldSpeed>0){flightTime+=seconds;journey.Advance(seconds*speedMetersPerSecond);}
            float phase=flightTime*swayFrequency,seedPhase=(journey.seed%997)*.013f;
            float x=swayAmplitude*(.78f*Mathf.Sin(phase+seedPhase)+.22f*Mathf.Sin(phase*.43f+seedPhase*2));
            // Visual sway must not yaw the cruise heading.
            Heading=new Vector3(routeSlope,0,1).normalized;
            Bank=-Mathf.Sin(phase+seedPhase)*7f;
            float globalZ=(journey.Current.index+journey.Progress)*TerrainStreamer.SegmentLength;
            x+=RouteX(globalZ);
            if(AircraftHeightPreview.Enabled)
            {
                // Fixed flight altitude with a separate camera/streaming anchor,
                // preserving the approved framing without terrain-driven climbs.
                groundClearance=0;clearanceVelocity=0;
                transform.SetPositionAndRotation(new Vector3(x,AircraftHeightPreview.CameraAnchorHeight,journey.Progress*TerrainStreamer.SegmentLength),Quaternion.LookRotation(Heading,Vector3.up));
                return;
            }
            // Preview the whole aircraft corridor, including hills beside its centre line.
            // A centre-only preview could miss a wingtip hill until the hard floor
            // snapped the aircraft (and its following camera) upwards.
            float terrain=0;
            if(journey.continuousWorld)for(float ahead=-aircraftRadius;ahead<=80+aircraftRadius;ahead+=4)
                for(float side=-aircraftRadius;side<=aircraftRadius+4;side+=4)
                    terrain=Mathf.Max(terrain,landscape.Height(x+routeSlope*ahead+Mathf.Min(side,aircraftRadius),globalZ+ahead));
            // Hard clearance floor beneath the whole wingspan, independent of the climb smoothing.
            float localGround=0;
            // Terrain is a shared 4 m cell height field. Visit every intersected cell,
            // including the full jet footprint regardless of its heading or bank.
            if(journey.continuousWorld)for(float gz=Mathf.Floor((globalZ-aircraftRadius)/4)*4;gz<=globalZ+aircraftRadius;gz+=4)
                for(float gx=Mathf.Floor((x-aircraftRadius)/4)*4;gx<=x+aircraftRadius;gx+=4)localGround=Mathf.Max(localGround,landscape.Height(gx+2,gz+2));
            if(seconds==0){groundClearance=terrain;clearanceVelocity=0;}
            else if(!journey.context.paused)groundClearance=Mathf.SmoothDamp(groundClearance,terrain,ref clearanceVelocity,terrain>groundClearance?2.5f:6f,8,seconds);
            // Clearance is measured against the actual flight altitude. The cruise
            // altitude already provides a reserve; do not demand the full terrain
            // height again as an instantaneous extra climb.
            groundClearance=Mathf.Max(groundClearance,localGround+30-altitude);
            transform.SetPositionAndRotation(new Vector3(x,altitude+groundClearance+bobAmplitude*(.7f*Mathf.Sin(flightTime*.42f+seedPhase)+.3f*Mathf.Sin(flightTime*.19f+seedPhase*2)),journey.Progress*TerrainStreamer.SegmentLength),Quaternion.LookRotation(Heading,Vector3.up));
        }
        public bool BeginCircle()=>false;
    }
}
