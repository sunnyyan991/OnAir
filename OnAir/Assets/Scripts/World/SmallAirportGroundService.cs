using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // Source metres, persisted with the visit. Rendering never advances this state.
    [Serializable] public sealed class AirportServiceVehicleState
    {
        public Vector3 position, heading=Vector3.left;
        public float speed, distance, routeLength;
        public bool reversing;
        public List<Vector3> route=new List<Vector3>();
    }
    [Serializable] public sealed class AirportGroundServiceState
    {
        public AirportServiceVehicleState fuel, tug, mechanic;
        public int phase=-1;
        public bool tugCoupled;
        public float couplingSeconds;
    }

    // Real drive paths through the one apron. Tow coupling is a physical condition,
    // rather than a timer that places a tractor beside an already-moving aircraft.
    public sealed class SmallAirportGroundService
    {
        public static readonly Vector3 FuelParking=new Vector3(111,0,10), TugParking=new Vector3(119,0,10), MechanicParking=new Vector3(127,0,10);
        public static readonly Vector3 FuelWork=new Vector3(113,0,45), MechanicWork=new Vector3(113,0,27), TugStaging=new Vector3(68,0,24);
        public const float CouplingOffset=6.8f, CouplingSeconds=.8f, VehicleMaxSpeed=3.2f, SteeringDegrees=85f;
        const float Acceleration=1.6f;
        readonly AirportVisitPlan.Site site;
        readonly AirportGroundServiceState state;
        public AirportServiceVehicleState Fuel=>state.fuel;
        public AirportServiceVehicleState Tug=>state.tug;
        public AirportServiceVehicleState Mechanic=>state.mechanic;
        public bool TugCoupled=>state.tugCoupled;
        public bool FuelConnected=>state.phase==(int)AirportPhase.Maintenance&&At(Fuel,FuelWork);
        public bool DepartureClear=>At(Fuel,FuelParking)&&At(Mechanic,MechanicParking)&&At(Tug,TugParking);
        public Vector3 WorldPosition(AirportServiceVehicleState vehicle)=>SmallAirportNetwork.World(site,vehicle.position);
        public Vector3 WorldHeading(AirportServiceVehicleState vehicle)=>site.assetRotation*vehicle.heading;

        public SmallAirportGroundService(AirportVisitPlan.Site site,AirportVisitState visit)
        {
            this.site=site;
            if(visit.groundService==null||visit.groundService.fuel==null||visit.groundService.tug==null||visit.groundService.mechanic==null)
                visit.groundService=new AirportGroundServiceState{fuel=Parked(FuelParking),tug=Parked(TugParking),mechanic=Parked(MechanicParking)};
            state=visit.groundService;
        }
        static AirportServiceVehicleState Parked(Vector3 position)=>new AirportServiceVehicleState{position=position};
        static bool At(AirportServiceVehicleState vehicle,Vector3 point)=>Vector3.Distance(vehicle.position,point)<.08f&&vehicle.speed<.08f;
        internal void Tick(AirportPhase phase,Vector3 aircraft,float dt)
        {
            if(dt<=0)return;
            if(state.phase!=(int)phase)
            {
                state.phase=(int)phase;
                switch(phase)
                {
                    case AirportPhase.TaxiIn:
                        // Leave the parking row first; the parked fuel bowser is
                        // immediately west of the tractor's own stand.
                        SetRoute(Tug,false,new Vector3(TugParking.x,0,3),new Vector3(100,0,3),new Vector3(68,0,3),TugStaging);
                        break;
                    case AirportPhase.BackIntoHangar:
                        state.tugCoupled=false;state.couplingSeconds=0;
                        var nose=SmallAirportNetwork.Source(site,aircraft+site.HangarForward*CouplingOffset);nose.y=0;
                        // The nose tractor approaches facing the aircraft, then pushes
                        // it backwards into the bay with its front coupling bar.
                        if(Vector3.Distance(Tug.position,TugStaging)>1)
                            SetRoute(Tug,false,new Vector3(TugParking.x,0,3),new Vector3(100,0,3),new Vector3(68,0,3),TugStaging,new Vector3(68,0,36),nose);
                        else SetRoute(Tug,false,new Vector3(68,0,36),nose);
                        break;
                    case AirportPhase.Maintenance:
                        state.tugCoupled=false;
                        SetRoute(Tug,true,new Vector3(88,0,36),new Vector3(88,0,3),new Vector3(TugParking.x,0,3),TugParking);
                        SetRoute(Fuel,false,new Vector3(94,0,10),new Vector3(94,0,45),FuelWork);
                        SetRoute(Mechanic,false,new Vector3(98,0,10),new Vector3(98,0,27),MechanicWork);
                        break;
                    case AirportPhase.TaxiOut:
                        state.tugCoupled=false;
                        ReturnToParking(Fuel,FuelParking,94);
                        ReturnToParking(Mechanic,MechanicParking,98);
                        if(!At(Tug,TugParking))ReturnToParking(Tug,TugParking,88);
                        break;
                }
            }
            Drive(Fuel,dt);Drive(Mechanic,dt);
            if(!state.tugCoupled)Drive(Tug,dt);
            if(phase==AirportPhase.BackIntoHangar&&!state.tugCoupled)
            {
                var coupling=SmallAirportNetwork.Source(site,aircraft+site.HangarForward*CouplingOffset);coupling.y=0;
                var heading=Quaternion.Inverse(site.assetRotation)*-site.HangarForward;
                if(At(Tug,coupling)&&Vector3.Angle(Tug.heading,heading)<3)
                    state.couplingSeconds+=dt;
                else state.couplingSeconds=0;
                if(state.couplingSeconds>=CouplingSeconds)state.tugCoupled=true;
            }
        }
        internal void FollowTowDelta(Vector3 worldDelta,float speed)
        {
            if(!state.tugCoupled)return;
            // Both members advance by the same integrated displacement. No target
            // assignment is used when attaching, changing phase, or following.
            var delta=Quaternion.Inverse(site.assetRotation)*worldDelta;delta.y=0;
            Tug.position+=delta;Tug.speed=speed;
        }
        static void ReturnToParking(AirportServiceVehicleState vehicle,Vector3 parking,float aisleX)
        {
            if(At(vehicle,parking))return;
            // Return behind the parking row, then enter the vehicle's own stall.
            // Traversing z=10 would pass through the other parked vehicles.
            SetRoute(vehicle,true,new Vector3(aisleX,0,vehicle.position.z),new Vector3(aisleX,0,3),new Vector3(parking.x,0,3),parking);
        }
        static void SetRoute(AirportServiceVehicleState vehicle,bool reverse,params Vector3[] waypoints)
        {
            var points=new List<Vector3>{vehicle.position};
            foreach(var p in waypoints)if(Vector3.Distance(points[points.Count-1],p)>.01f)points.Add(p);
            vehicle.route=Rounded(points);vehicle.routeLength=Length(vehicle.route);vehicle.distance=0;vehicle.reversing=reverse;
        }
        static List<Vector3> Rounded(List<Vector3> points)
        {
            var path=new List<Vector3>{points[0]};
            for(int i=1;i<points.Count-1;i++)
            {
                var a=points[i]-points[i-1];var b=points[i+1]-points[i];
                float angle=Vector3.Angle(a,b);
                if(angle<1||angle>150){path.Add(points[i]);continue;}
                float trim=Mathf.Min(4,a.magnitude*.4f,b.magnitude*.4f);
                var entry=points[i]-a.normalized*trim;var exit=points[i]+b.normalized*trim;
                path.Add(entry);
                var p1=entry+a.normalized*trim*.55f;var p2=exit-b.normalized*trim*.55f;
                for(int j=1;j<=16;j++)
                {float t=j/16f,u=1-t;path.Add(u*u*u*entry+3*u*u*t*p1+3*u*t*t*p2+t*t*t*exit);}
            }
            if(points.Count>1)path.Add(points[points.Count-1]);
            return path;
        }
        static float Length(List<Vector3> path)
        {float length=0;for(int i=1;i<path.Count;i++)length+=Vector3.Distance(path[i-1],path[i]);return length;}
        static Vector3 Sample(List<Vector3> path,float distance)
        {
            distance=Mathf.Max(0,distance);
            for(int i=1;i<path.Count;i++)
            {float length=Vector3.Distance(path[i-1],path[i]);if(distance<=length)return Vector3.Lerp(path[i-1],path[i],length>.0001f?distance/length:1);distance-=length;}
            return path.Count>0?path[path.Count-1]:Vector3.zero;
        }
        static void Drive(AirportServiceVehicleState vehicle,float dt)
        {
            var path=vehicle.route;if(path==null||path.Count<2){vehicle.speed=0;return;}
            if(vehicle.routeLength<=0)vehicle.routeLength=Length(path);
            float length=vehicle.routeLength,remaining=Mathf.Max(0,length-vehicle.distance);
            if(remaining<=0&&vehicle.speed<=0)return;
            var tangent=Sample(path,vehicle.distance+.35f)-Sample(path,vehicle.distance-.35f);
            if(tangent.sqrMagnitude<.000001f)tangent=path[path.Count-1]-path[path.Count-2];
            tangent.y=0;var desired=tangent.normalized*(vehicle.reversing?-1:1);
            vehicle.heading=Vector3.RotateTowards(vehicle.heading,desired,SteeringDegrees*Mathf.Deg2Rad*dt,0).normalized;
            float target=Mathf.Min(VehicleMaxSpeed,Mathf.Sqrt(2*Acceleration*remaining));
            float error=Vector3.Angle(vehicle.heading,desired);
            target*=Mathf.Clamp01(1-error/18);
            float previous=vehicle.speed;
            vehicle.speed=Mathf.MoveTowards(previous,target,Acceleration*dt);
            // A route direction change first settles the steering. The tractor
            // never slides sideways while its model continues facing elsewhere.
            if(error<18)vehicle.distance=Mathf.Min(length,vehicle.distance+(previous+vehicle.speed)*.5f*dt);
            vehicle.position=Sample(path,vehicle.distance);
            if(length-vehicle.distance<.012f){vehicle.distance=length;vehicle.position=path[path.Count-1];vehicle.speed=0;}
        }
    }
}
