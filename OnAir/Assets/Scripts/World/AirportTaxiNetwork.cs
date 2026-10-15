using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // All coordinates belong to the reusable airport, before its visit transform.
    // Runway exits feed one authored taxi network; hangars never follow a runway.
    public static class AirportTaxiNetwork
    {
        public sealed class Bay
        {
            public string name;
            public int district;
            public Vector3 centre, forward;
            public Vector3 Front => centre + forward * 40;
            public Vector3 Join => Front + forward * 30;
            public Vector3 Side => Vector3.Cross(forward,Vector3.up);
        }
        // Buildings remain at unit scale; only the surrounding site is compressed.
        public static readonly Vector3[] TerminalCentres={new Vector3(-610,0,430),new Vector3(-610,0,-430),new Vector3(-1740,0,0)};
        public static readonly Vector3 TowerCentre=new Vector3(-780,0,30), CargoCentre=new Vector3(-990,0,-1200), RemoteCentre=new Vector3(-500,0,1140);
        public static readonly Vector3[] RunwayCentres={new Vector3(-1250,0,0),new Vector3(-1170,0,1150),Vector3.zero,new Vector3(-50,0,-1210)};
        public static readonly float[] RunwayLengths={900,800,1000,800}, RunwayAngles={0,64,0,73};
        static readonly float[] parallels={170,-160,120,-145};
        public static readonly Bay[] Bays={
            new Bay{name="S1",district=0,centre=new Vector3(290,0,-200),forward=Vector3.left},
            new Bay{name="S2",district=0,centre=new Vector3(290,0,0),forward=Vector3.left},
            new Bay{name="S3",district=0,centre=new Vector3(290,0,200),forward=Vector3.left},
            new Bay{name="W1",district=1,centre=new Vector3(-920,0,-160),forward=Vector3.left},
            new Bay{name="W2",district=1,centre=new Vector3(-920,0,160),forward=Vector3.left},
            new Bay{name="N1",district=2,centre=RunwayPoint(1,-380,-110),forward=Quaternion.Euler(0,64,0)*Vector3.right},
            new Bay{name="N2",district=2,centre=RunwayPoint(1,-380,110),forward=Quaternion.Euler(0,64,0)*Vector3.right},
            new Bay{name="NW1",district=3,centre=RunwayPoint(3,-380,0),forward=Quaternion.Euler(0,73,0)*Vector3.right}
        };
        public static readonly Vector2[] LandOutline={new Vector2(-2200,-1500),new Vector2(-2200,900),new Vector2(-1800,1800),new Vector2(-800,1820),new Vector2(470,950),new Vector2(470,-950),new Vector2(-600,-1050),new Vector2(-620,-1550),new Vector2(-1500,-1580)};
        public const float SouthLink=-900,NorthLink=900;
        public static readonly Vector3 NeighbourHangarLocal=new Vector3(-56,0,-6);
        public static readonly Vector3[] ParkedAircraftLocal={new Vector3(-70,0,28),new Vector3(-70,0,49),new Vector3(47,0,35)};
        public static float AimingDistance(float length)=>Mathf.Min(400,length*.25f);
        public static int TouchdownStations(float length)=>Mathf.Min(6,Mathf.FloorToInt((length/2-70)/150));
        static float BayAlong(int runway,Bay bay)=>(Quaternion.Euler(0,-RunwayAngles[runway],0)*(bay.Join-RunwayCentres[runway])).z;
        public static Vector3 RunwayPoint(int runway,float x,float z) => RunwayCentres[runway]+Quaternion.Euler(0,RunwayAngles[runway],0)*new Vector3(x,0,z);
        public static Vector3 World(AirportVisitPlan.Site s,Vector3 source) => s.assetPosition+s.assetRotation*source;
        public static Vector3 Source(AirportVisitPlan.Site s,Vector3 global) => Quaternion.Inverse(s.assetRotation)*(global-s.assetPosition);
        public static bool RunwayOccupied(Vector3 source,float margin,int except=-1)
        {
            for(int i=0;i<4;i++)
            {
                if(i==except)continue;
                var p=Quaternion.Euler(0,-RunwayAngles[i],0)*(source-RunwayCentres[i]);
                if(Mathf.Abs(p.x)<30+margin&&Mathf.Abs(p.z)<RunwayLengths[i]/2+margin)return true;
            }
            return false;
        }
        public static float ExitOffset(int runway,bool reverse)
        {
            float half=RunwayLengths[runway]/2;
            float rolloutEnd=RunwayLengths[runway]*.62f;
            return reverse?half-rolloutEnd:-half+rolloutEnd;
        }
        // Depart through the same physical junction used by the opposite arrival.
        static float DepartureOffset(AirportVisitPlan.Site s)=>ExitOffset(s.runway,!s.reverse);
        public static Vector3 Departure(AirportVisitPlan.Site s) => World(s,RunwayPoint(s.runway,0,DepartureOffset(s)));
        // Permanent paving includes the whole airport, not just the active visit.
        // The small aircraft can still use a shorter local route to its own bay.
        public static List<List<Vector3>> RunwayBranches()
        {
            var branches=new List<List<Vector3>>();
            for(int runway=0;runway<4;runway++)
            {
                float parallel=parallels[runway],half=RunwayLengths[runway]/2;
                foreach(bool reverse in new[]{false,true})
                {
                    int direction=reverse?-1:1;
                    // One shared stem splits into both travel directions on the
                    // parallel taxiway. Visits consume these same sampled curves.
                    branches.Add(RunwayStem(runway,reverse));
                    foreach(int along in new[]{-1,1})branches.Add(RunwayFork(runway,reverse,along));
                    // Keep the distinctive end entries without another diagonal
                    // through the usable middle junction.
                    float end=direction*(half-65);
                    branches.Add(Curve(RunwayPoint(runway,0,end),RunwayPoint(runway,parallel*.5f,end),
                        RunwayPoint(runway,parallel,end),RunwayPoint(runway,parallel,end+direction*30)));
                }
            }
            return branches;
        }
        static float StemAlong(int runway,bool reverse)
        {
            // A/C bays sit just ahead of the exit; B/D bays are inward of it.
            // Keep room for a tangent merge in either direction, without a U turn.
            int direction=reverse?-1:1;
            return ExitOffset(runway,reverse)+direction*(runway==0||runway==2?12:54);
        }
        static Vector3 StemEnd(int runway,bool reverse)=>RunwayPoint(runway,
            parallels[runway]-Mathf.Sign(parallels[runway])*40,StemAlong(runway,reverse));
        public static List<Vector3> RunwayStem(int runway,bool reverse)
        {
            float t=ExitOffset(runway,reverse),advance=runway==0||runway==2?16:40;
            int direction=reverse?-1:1;var end=StemEnd(runway,reverse);
            var lateral=Quaternion.Euler(0,RunwayAngles[runway],0)*Vector3.right*Mathf.Sign(parallels[runway]);
            return Curve(RunwayPoint(runway,0,t),RunwayPoint(runway,0,t+direction*advance),end-lateral*35,end);
        }
        public static List<Vector3> RunwayFork(int runway,bool reverse,int along)
        {
            var start=StemEnd(runway,reverse);
            var end=RunwayPoint(runway,parallels[runway],StemAlong(runway,reverse)+along*18);
            var rotation=Quaternion.Euler(0,RunwayAngles[runway],0);
            return Curve(start,start+rotation*Vector3.right*Mathf.Sign(parallels[runway])*18,
                end-rotation*Vector3.forward*along*12,end);
        }
        public static List<Vector3> BayApproach(int runway,Bay bay,int along)
        {
            float t=BayAlong(runway,bay);var start=RunwayPoint(runway,parallels[runway],t-along*18);
            var longitudinal=Quaternion.Euler(0,RunwayAngles[runway],0)*Vector3.forward*along;
            return Curve(start,start+longitudinal*12,bay.Join+bay.forward*20,bay.Join);
        }
        public static List<Vector3> BayAlignment(Bay bay)
        {
            var end=bay.Front+bay.Side*32;
            return Curve(bay.Join,bay.Join-bay.forward*15,end+bay.forward*10,end);
        }
        static List<Vector3> Curve(Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            var path=new List<Vector3>();
            for(int i=0;i<=32;i++){float t=i/32f,u=1-t;path.Add(u*u*u*a+3*u*u*t*b+3*u*t*t*c+t*t*t*d);}
            return path;
        }
        public static List<List<Vector3>> PermanentPaths()
        {
            var paths=new List<List<Vector3>>();
            for(int runway=0;runway<4;runway++)
            {
                float half=runway==0||runway==2?NorthLink:RunwayLengths[runway]/2+40;
                paths.Add(new List<Vector3>{RunwayPoint(runway,parallels[runway],runway==0||runway==2?SouthLink:-half),RunwayPoint(runway,parallels[runway],half)});
            }
            // Additional apron-side spines and perimeter links connect the large halls.
            paths.Add(new List<Vector3>{new Vector3(-1420,0,SouthLink),new Vector3(-1420,0,NorthLink)});
            paths.Add(new List<Vector3>{new Vector3(20,0,-965),new Vector3(20,0,-800),new Vector3(-120,0,-775),new Vector3(-120,0,NorthLink)});
            // The south link bends between cargo and the D maintenance district.
            paths.Add(new List<Vector3>{new Vector3(-1420,0,-870),new Vector3(-690,0,-870),new Vector3(-635,0,SouthLink),
                new Vector3(-320,0,SouthLink),new Vector3(-280,0,-965),new Vector3(50,0,-965),new Vector3(50,0,SouthLink),new Vector3(120,0,SouthLink)});
            paths.Add(new List<Vector3>{new Vector3(-1420,0,NorthLink),new Vector3(120,0,NorthLink)});
            paths.AddRange(RunwayBranches());
            // West apron alternatives go around A's ends, never across its runway.
            foreach(float z in new[]{-620f,620f})
                paths.Add(new List<Vector3>{new Vector3(-1420,0,z),new Vector3(-1080,0,z)});
            // Rounded junction shortcuts retain the characteristic grass islands.
            foreach(float x in new[]{-1420f,120f})foreach(int side in new[]{-1,1})
            {
                float z=side<0?(x<0?-870:SouthLink):NorthLink,inward=x<0?1:-1;
                paths.Add(Curve(new Vector3(x,0,z-side*60),new Vector3(x,0,z),
                    new Vector3(x,0,z),new Vector3(x+inward*60,0,z)));
            }
            // B connects at both ends: the northern branch skirts the remote apron.
            paths.Add(Round(new List<Vector3>{RunwayPoint(1,parallels[1],-440),
                new Vector3(-1660,0,1005),new Vector3(-1420,0,900)},45));
            paths.Add(Round(new List<Vector3>{RunwayPoint(1,parallels[1],440),
                RunwayPoint(1,parallels[1],500),new Vector3(-345,0,1450),new Vector3(-120,0,1300)},45));
            // Cargo stand guidance joins the same network on the existing apron.
            paths.Add(Round(new List<Vector3>{new Vector3(-710,0,-1450),new Vector3(-710,0,-935),
                new Vector3(-675,0,SouthLink),new Vector3(-620,0,SouthLink)},28));
            // T1/T2 share one aligned apron centreline through their narrow gap.
            // Their inward ends do not each curl back into the east spine.
            paths.Add(new List<Vector3>{new Vector3(TerminalCentres[0].x+248,0,-835),new Vector3(TerminalCentres[0].x+248,0,835)});
            for(int index=0;index<TerminalCentres.Length;index++)foreach(int side in new[]{-1,1})
            {
                if(index==0&&side<0||index==1&&side>0)continue;
                var terminal=TerminalCentres[index];
                float z=terminal.z+side*(terminal==TerminalCentres[2]?360:405),x=terminal.x+248;
                float spine=terminal.x<-1400?-1420:-120;
                if(index==1&&side<0)
                    paths.Add(Curve(new Vector3(x,0,z),new Vector3(x,0,SouthLink+25),
                        new Vector3(x-85,0,SouthLink),new Vector3(x-128,0,SouthLink)));
                else
                    paths.Add(Curve(new Vector3(x,0,z),new Vector3(spine,0,z),
                        new Vector3(spine,0,z+side*30),new Vector3(spine,0,z+side*65)));
            }
            foreach(float delta in new[]{-210f,210f})
                paths.Add(delta<0?new List<Vector3>{new Vector3(RemoteCentre.x+155,0,RemoteCentre.z+delta),new Vector3(-120,0,RemoteCentre.z+delta)}:
                    Round(new List<Vector3>{new Vector3(RemoteCentre.x+155,0,RemoteCentre.z+delta),
                        new Vector3(-180,0,RemoteCentre.z+delta),new Vector3(-120,0,1300)},28));
            paths.Add(new List<Vector3>{new Vector3(-120,0,900),new Vector3(-120,0,1300)});
            for(int i=0;i<Bays.Length;i++)
            {
                int runway=Bays[i].district==0?2:Bays[i].district==1?0:Bays[i].district==2?1:3;
                foreach(int along in new[]{-1,1})paths.Add(BayApproach(runway,Bays[i],along));
                paths.Add(BayAlignment(Bays[i]));
                paths.Add(new List<Vector3>{Bays[i].Join,Bays[i].Front});
            }
            foreach(float x in new[]{120f,200f})
            {
                float angle=RunwayAngles[3]*Mathf.Deg2Rad;
                float t=(x-RunwayCentres[3].x-parallels[3]*Mathf.Cos(angle))/Mathf.Sin(angle);
                paths.Add(new List<Vector3>{RunwayPoint(3,parallels[3],t),new Vector3(x,0,SouthLink),new Vector3(120,0,-850)});
            }
            // Cubic curves are already smooth; only round authored corner polylines.
            for(int i=0;i<paths.Count;i++)if(paths[i].Count<12)paths[i]=Round(paths[i],18);
            return paths;
        }
        public static int SelectBay(AirportVisitPlan.Site s)
        {
            int district=s.runway==0?1:s.runway==2?0:s.runway==1?2:3;
            int selected=-1;float best=float.MaxValue;
            for(int i=0;i<Bays.Length;i++)
            {
                if(Bays[i].district!=district)continue;
                float score=Length(SourceRoute(s,i,false))+(i+s.id)%3*15;
                if(score<best){best=score;selected=i;}
            }
            return selected;
        }
        public static List<Vector3> Route(AirportVisitPlan.Site s,bool outward)
        {
            var result=SourceRoute(s,s.bay,outward);
            for(int i=0;i<result.Count;i++)result[i]=World(s,result[i]);
            return result;
        }
        static List<Vector3> SourceRoute(AirportVisitPlan.Site s,int bayIndex,bool outward)
        {
            var bay=Bays[bayIndex];bool entryReverse=outward?!s.reverse:s.reverse;
            int along=BayAlong(s.runway,bay)>=StemAlong(s.runway,entryReverse)?1:-1;
            var stem=RunwayStem(s.runway,entryReverse);
            var fork=RunwayFork(s.runway,entryReverse,along);
            var approach=BayApproach(s.runway,bay,along);
            var route=new List<Vector3>();
            void Append(List<Vector3> piece,bool backwards=false)
            {
                if(backwards)piece.Reverse();
                foreach(var point in piece)
                    if(route.Count==0||(route[route.Count-1]-point).sqrMagnitude>.000001f)route.Add(point);
            }
            if(!outward)
            {
                Append(stem);Append(fork);Append(approach);Append(BayAlignment(bay));
                var circle=bay.Front+bay.Side*16;
                for(int i=1;i<=32;i++)
                {float a=i*Mathf.PI/32;route.Add(circle+bay.Side*(16*Mathf.Cos(a))-bay.forward*(16*Mathf.Sin(a)));}
                return route;
            }
            Append(new List<Vector3>{bay.centre,bay.Front,bay.Join});
            Append(approach,true);Append(fork,true);Append(stem,true);
            return route;
        }
        static List<Vector3> Clean(List<Vector3> source)
        {
            var result=new List<Vector3>();
            foreach(var p in source)if(result.Count==0||Vector3.Distance(result[result.Count-1],p)>.01f)result.Add(p);
            return result;
        }
        public static List<Vector3> Round(List<Vector3> source,float radius)
        {
            var points=Clean(source);var output=new List<Vector3>{points[0]};
            for(int i=1;i<points.Count-1;i++)
            {
                var before=points[i]-points[i-1];var after=points[i+1]-points[i];
                var a=before.normalized;var b=after.normalized;
                float angle=Mathf.Acos(Mathf.Clamp(Vector3.Dot(a,b),-1,1));
                if(angle<.01f){output.Add(points[i]);continue;}
                if(angle>Mathf.PI-.01f)throw new InvalidOperationException("Taxi network contains a reversing edge");
                float trim=Mathf.Min(radius*Mathf.Tan(angle/2),before.magnitude*.4f,after.magnitude*.4f);
                var begin=points[i]-a*trim;var end=points[i]+b*trim;
                output.Add(begin);
                for(int j=1;j<=12;j++){float t=j/12f,u=1-t;output.Add(u*u*begin+2*u*t*points[i]+t*t*end);}
            }
            output.Add(points[points.Count-1]);return output;
        }
        public static float Length(List<Vector3> route)
        {float result=0;for(int i=1;i<route.Count;i++)result+=Vector3.Distance(route[i-1],route[i]);return result;}
    }
}
