using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // Values are persisted; keep the phase indices compatible with existing saves.
    public enum AirportPhase { Cruise, Approach, LandingRoll, TaxiIn, BackIntoHangar, Maintenance, TaxiOut, RunwayHold, TakeoffRoll, Climb, Complete }
    [Serializable] public sealed class AirportVisitState
    {
        public int siteId=-1, seed, phase, completedSite=-1;
        public int routeVersion=6;
        public float distance, elapsed, speed, groundHeight=.9f, cruiseSpeed=4.8f, simulationRemainder;
        public Vector3 position, heading=Vector3.forward, entry;
        public AirportGroundServiceState groundService;
    }
    // One simulation clock advances aircraft and service vehicles, including pause,
    // accelerated previews and restored visits. Heading samples the continuous path.
    public sealed class AirportVisitMotion
    {
        public AirportVisitState State { get; private set; }
        public readonly AirportVisitPlan.Site Site;
        public SmallAirportGroundService GroundService { get; private set; }
        public AirportPhase Phase => (AirportPhase)State.phase;
        public const float MaintenanceSeconds=900, HoldSeconds=10, TakeoffDistance=150, ClimbDistance=350;
        const float SimulationStep=1f/120, TaxiSpeed=3.1f, ReverseSpeed=1.1f;
        readonly List<Vector3> path=new List<Vector3>();
        float pathLength;
        public float RemainingDistance => Mathf.Max(0,pathLength-State.distance);
        public float ApproachSpeed=>Mathf.Min(8.2f,Mathf.Max(7.5f,State.cruiseSpeed*1.7f));
        public float LiftoffSpeed=>ApproachSpeed*1.15f;
        public float VisualPitch
        {
            get
            {
                switch(Phase)
                {
                    case AirportPhase.Approach:return Mathf.Lerp(3,-2,Mathf.SmoothStep(0,1,1-RemainingDistance/20));
                    case AirportPhase.LandingRoll:return Mathf.Lerp(-2,0,Mathf.SmoothStep(0,1,State.distance/18));
                    case AirportPhase.TakeoffRoll:return Mathf.Lerp(0,-5,Mathf.SmoothStep(0,1,Mathf.InverseLerp(TakeoffDistance*.6f,TakeoffDistance,State.distance)));
                    case AirportPhase.Climb:return Mathf.Lerp(-5,0,Mathf.SmoothStep(0,1,State.distance/ClimbDistance));
                    default:return 0;
                }
            }
        }
        public AirportVisitMotion(AirportVisitPlan.Site site,AirportVisitState state)
        {
            Site=site;State=state;
            if(state.routeVersion<6)
            {
                // The old field used different runways and hangars. Rebase ground
                // phases to the new bay while preserving the actual maintenance wait.
                if(Phase>=AirportPhase.LandingRoll&&Phase<=AirportPhase.Climb)
                {
                    float waited=Phase==AirportPhase.Maintenance?state.elapsed:0;
                    state.phase=(int)AirportPhase.Maintenance;state.distance=0;state.elapsed=waited;state.speed=0;
                    state.position=Site.Hangar+Vector3.up*state.groundHeight;state.heading=Site.HangarForward;
                }
                else if(Phase==AirportPhase.Approach)
                {
                    state.position=new Vector3(Site.runwayX,Mathf.Clamp(state.position.y,state.groundHeight+8,AircraftHeightPreview.AbsoluteHeight),Site.start);
                    state.entry=state.position;state.distance=state.elapsed=0;state.heading=Vector3.forward;
                }
                state.groundService=null;state.simulationRemainder=0;
            }
            state.routeVersion=6;MakePath();
            GroundService=new SmallAirportGroundService(site,state);
        }
        public static List<Vector3> TaxiRoute(AirportVisitPlan.Site s,bool outward)=>SmallAirportNetwork.Route(s,outward);
        void MakePath()
        {
            path.Clear();float h=State.groundHeight;
            switch(Phase)
            {
                case AirportPhase.Approach:path.Add(State.entry);path.Add(new Vector3(Site.runwayX,h,Site.LandingZ));break;
                case AirportPhase.LandingRoll:path.Add(new Vector3(Site.runwayX,h,Site.LandingZ));path.Add(new Vector3(Site.runwayX,h,Site.TurnoffZ));break;
                case AirportPhase.TaxiIn:case AirportPhase.TaxiOut:
                    foreach(var p in TaxiRoute(Site,Phase==AirportPhase.TaxiOut))path.Add(p+Vector3.up*h);break;
                case AirportPhase.BackIntoHangar:path.Add(Site.HangarFront+Vector3.up*h);path.Add(Site.Hangar+Vector3.up*h);break;
                case AirportPhase.TakeoffRoll:path.Add(Site.Departure+Vector3.up*h);path.Add(Site.Departure+Vector3.forward*TakeoffDistance+Vector3.up*h);break;
                case AirportPhase.Climb:path.Add(Site.Departure+Vector3.forward*TakeoffDistance+Vector3.up*h);path.Add(Site.Departure+Vector3.forward*(TakeoffDistance+ClimbDistance)+Vector3.up*AircraftHeightPreview.AbsoluteHeight);break;
            }
            pathLength=0;for(int i=1;i<path.Count;i++)pathLength+=HorizontalLength(path[i]-path[i-1]);
        }
        static float HorizontalLength(Vector3 delta)=>new Vector2(delta.x,delta.z).magnitude;
        public void SkipMaintenance(){if(Phase==AirportPhase.Maintenance)State.elapsed=MaintenanceSeconds;}
        public void Tick(float dt)
        {
            if(dt<=0||Phase==AirportPhase.Complete)return;
            State.simulationRemainder+=dt;
            while(State.simulationRemainder>=SimulationStep)
            {
                State.simulationRemainder-=SimulationStep;Step(SimulationStep);
                if(Phase==AirportPhase.Complete){State.simulationRemainder=0;break;}
            }
        }
        void Step(float dt)
        {
            GroundService.Tick(Phase,State.position,dt);
            State.elapsed+=dt;
            if(Phase==AirportPhase.Maintenance||Phase==AirportPhase.RunwayHold)
            {
                State.speed=0;
                if(State.elapsed>=(Phase==AirportPhase.Maintenance?MaintenanceSeconds:HoldSeconds))Next();
                return;
            }
            // The aircraft waits at the nose-coupling stand until the tractor has
            // really arrived. It also waits for service carts to clear its departure.
            if((Phase==AirportPhase.BackIntoHangar&&!GroundService.TugCoupled)||
                (Phase==AirportPhase.TaxiOut&&State.distance<=0&&!GroundService.DepartureClear))
            {
                State.speed=0;
                if(Phase==AirportPhase.BackIntoHangar)
                    State.heading=Vector3.RotateTowards(State.heading,Site.HangarForward,25*Mathf.Deg2Rad*dt,0).normalized;
                return;
            }
            bool taxi=Phase==AirportPhase.TaxiIn||Phase==AirportPhase.TaxiOut;
            float max=Phase==AirportPhase.BackIntoHangar?ReverseSpeed:taxi?TaxiSpeed:ApproachSpeed;
            if(taxi)
            {
                var a=AtDistance(State.distance+4)-AtDistance(State.distance);
                var b=AtDistance(State.distance+8)-AtDistance(State.distance+4);
                if(a.sqrMagnitude>.01f&&b.sqrMagnitude>.01f)
                    max=Mathf.Lerp(TaxiSpeed,2.35f,Mathf.Clamp01(Vector3.Angle(a,b)/22));
            }
            float remaining=Mathf.Max(0,pathLength-State.distance);
            bool stop=taxi||Phase==AirportPhase.BackIntoHangar;
            if(Phase==AirportPhase.LandingRoll)
            {
                float progress=Mathf.Clamp01(State.distance/Mathf.Max(.01f,pathLength));
                // Final approach keeps its speed through contact. Only the ground
                // roll reduces it, using the available runway before the exit.
                max=Mathf.Lerp(ApproachSpeed,TaxiSpeed,Mathf.SmoothStep(0,1,progress));
            }
            else if(Phase==AirportPhase.TakeoffRoll)max=LiftoffSpeed;
            else if(Phase==AirportPhase.Climb)
                max=Mathf.Lerp(LiftoffSpeed,State.cruiseSpeed,Mathf.SmoothStep(0,1,State.distance/Mathf.Max(.01f,pathLength)));
            float acceleration=stop?.85f:Phase==AirportPhase.TakeoffRoll?1.3f:.95f;
            float deceleration=stop?1.15f:1.2f;
            if(stop)max=Mathf.Min(max,Mathf.Sqrt(2*deceleration*remaining));
            float previousSpeed=State.speed;
            State.speed=Mathf.MoveTowards(previousSpeed,max,(max<previousSpeed?deceleration:acceleration)*dt);
            var previousPosition=State.position;
            State.distance=Mathf.Min(pathLength,State.distance+(previousSpeed+State.speed)*.5f*dt);
            Sample(dt);
            if(Phase==AirportPhase.BackIntoHangar)GroundService.FollowTowDelta(State.position-previousPosition,State.speed);
            if(pathLength-State.distance<.015f)
            {
                previousPosition=State.position;State.distance=pathLength;Sample(0);
                if(Phase==AirportPhase.BackIntoHangar)GroundService.FollowTowDelta(State.position-previousPosition,0);
                Next();
            }
        }
        void Sample(float dt)
        {
            State.position=AtDistance(State.distance);
            if(Phase==AirportPhase.Approach)State.position.y=ApproachHeight(State.position.z);
            else if(Phase==AirportPhase.Climb)
                State.position.y=Mathf.Lerp(path[0].y,path[path.Count-1].y,Mathf.SmoothStep(0,1,State.distance/Mathf.Max(.01f,pathLength)));
            Vector3 tangent;
            if(Phase==AirportPhase.BackIntoHangar)tangent=Site.HangarForward;
            else
            {
                // Central differences cover both sides of every sampled-arc join,
                // rather than resetting heading at the start of each segment.
                tangent=AtDistance(State.distance+1.25f)-AtDistance(State.distance-1.25f);tangent.y=0;
            }
            if(tangent.sqrMagnitude>.000001f&&dt>0)
                State.heading=Vector3.RotateTowards(State.heading,tangent.normalized,50*Mathf.Deg2Rad*dt,0).normalized;
        }
        float ApproachHeight(float z)
        {
            float h=State.groundHeight,thresholdHeight=Mathf.Min(24,State.entry.y);
            if(State.entry.z>=Site.threshold)
            {
                float distance=Mathf.Max(.01f,Site.LandingZ-State.entry.z);
                return HermiteHeight(State.entry.y,h,-.42f*distance,-.035f*distance,Mathf.InverseLerp(State.entry.z,Site.LandingZ,z));
            }
            float inboundDistance=Mathf.Max(.01f,Site.threshold-State.entry.z);
            float thresholdSlope=-Mathf.Min(.42f,2*Mathf.Max(0,State.entry.y-thresholdHeight)/inboundDistance);
            if(z<=Site.threshold)
            {
                float distance=Site.threshold-State.entry.z;
                return HermiteHeight(State.entry.y,thresholdHeight,0,thresholdSlope*distance,Mathf.InverseLerp(State.entry.z,Site.threshold,z));
            }
            float finalDistance=Site.LandingZ-Site.threshold;
            return HermiteHeight(thresholdHeight,h,thresholdSlope*finalDistance,-.035f*finalDistance,Mathf.InverseLerp(Site.threshold,Site.LandingZ,z));
        }
        static float HermiteHeight(float a,float b,float slopeA,float slopeB,float t)
        {
            float change=b-a;
            slopeA=Mathf.Clamp(slopeA,3*Mathf.Min(0,change),3*Mathf.Max(0,change));
            slopeB=Mathf.Clamp(slopeB,3*Mathf.Min(0,change),3*Mathf.Max(0,change));
            float t2=t*t,t3=t2*t;return (2*t3-3*t2+1)*a+(t3-2*t2+t)*slopeA+(-2*t3+3*t2)*b+(t3-t2)*slopeB;
        }
        Vector3 AtDistance(float distance)
        {
            distance=Mathf.Max(0,distance);
            for(int i=1;i<path.Count;i++)
            {float length=HorizontalLength(path[i]-path[i-1]);if(distance<=length)return Vector3.Lerp(path[i-1],path[i],length>.0001f?distance/length:1);distance-=length;}
            return path.Count>0?path[path.Count-1]:State.position;
        }
        void Next()
        {
            State.phase++;State.distance=State.elapsed=0;
            if(Phase==AirportPhase.BackIntoHangar||Phase==AirportPhase.Maintenance||Phase==AirportPhase.RunwayHold)State.speed=0;
            if(Phase==AirportPhase.Complete)State.completedSite=Site.id;
            MakePath();
        }
    }
}
