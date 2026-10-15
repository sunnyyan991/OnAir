using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // Metres in the small field's local space. Motion and paint share these paths.
    public static class SmallAirportNetwork
    {
        public sealed class Bay
        {
            public string name="M1";
            public int district;
            public Vector3 centre=new Vector3(116,0,36),forward=Vector3.left;
            public Vector3 Front=>centre+forward*33;
            public Vector3 Join=>Front+forward*14;
            public Vector3 Side=>Vector3.Cross(forward,Vector3.up);
        }
        public const float RunwayWidth=18,TaxiWidth=12,Radius=12,ParallelX=35;
        public static readonly Vector3[] RunwayCentres={Vector3.zero};
        public static readonly float[] RunwayLengths={240},RunwayAngles={0};
        public static readonly Bay[] Bays={new Bay()};
        public static readonly Vector3[] TerminalCentres={new Vector3(117,0,-48)};
        public static readonly Vector2[] LandOutline={new Vector2(-45,-165),new Vector2(165,-165),new Vector2(165,165),new Vector2(-45,165)};
        public static readonly Rect Apron=new Rect(45,-78,87,156);
        public static bool ApronContains(Vector3 p)=>p.x>=Apron.xMin&&p.x<=Apron.xMax&&p.z>=Apron.yMin&&p.z<=Apron.yMax;
        public static bool RunwayOccupied(Vector3 p,float margin,int except=-1)=>except!=0&&Mathf.Abs(p.x)<RunwayWidth/2+margin&&Mathf.Abs(p.z)<120+margin;
        public static Vector3 RunwayPoint(int runway,float x,float z)=>new Vector3(x,0,z);
        public static Vector3 World(AirportVisitPlan.Site site,Vector3 p)=>site.assetPosition+site.assetRotation*p;
        public static Vector3 Source(AirportVisitPlan.Site site,Vector3 p)=>Quaternion.Inverse(site.assetRotation)*(p-site.assetPosition);
        public static float ExitOffset(int runway,bool reverse)=>reverse?-70:70;
        public static Vector3 Departure(AirportVisitPlan.Site site)=>World(site,new Vector3(0,0,site.reverse?70:-70));
        public static int SelectBay(AirportVisitPlan.Site site)=>0;
        static Vector3 Point(float x,float z)=>new Vector3(x,0,z);
        static void Append(List<Vector3> into,IEnumerable<Vector3> piece)
        {foreach(var p in piece)if(into.Count==0||(p-into[into.Count-1]).sqrMagnitude>.000001f)into.Add(p);}
        // Analytic circular arcs, sampled at a fixed angular interval, not arbitrary Beziers.
        static List<Vector3> Arc(float x,float z,float radius,float begin,float end)
        {
            int count=Mathf.CeilToInt(Mathf.Abs(end-begin)/3);var result=new List<Vector3>();
            for(int i=0;i<=count;i++){float a=Mathf.Lerp(begin,end,i/(float)count)*Mathf.Deg2Rad;result.Add(Point(x+radius*Mathf.Cos(a),z+radius*Mathf.Sin(a)));}
            return result;
        }
        public static List<Vector3> RunwayConnection(bool reverse)
        {
            float sign=reverse?-1:1;var path=new List<Vector3>();
            // Both bends turn the same way: no changing-curvature S or sharp neck.
            Append(path,Arc(12,sign*70,12,180,reverse?270:90));
            Append(path,new[]{Point(23,sign*82)});
            Append(path,Arc(23,sign*70,12,reverse?-90:90,0));
            return path;
        }
        static List<Vector3> ArrivalFork(bool reverse)
        {
            // Arrivals join one shared eastbound staging row at z=12.
            return reverse?Arc(47,0,12,180,90):Arc(47,24,12,180,270);
        }
        static List<Vector3> DepartureFork(bool reverse)
        {
            return reverse?Arc(47,48,12,270,180):Arc(47,24,12,90,180);
        }
        public static List<List<Vector3>> PermanentPaths()
        {
            var result=new List<List<Vector3>>{new List<Vector3>{Point(35,-70),Point(35,70)},RunwayConnection(false),RunwayConnection(true)};
            foreach(bool reverse in new[]{false,true}){result.Add(ArrivalFork(reverse));result.Add(DepartureFork(reverse));}
            result.Add(new List<Vector3>{Point(47,12),Point(83,12)});
            result.Add(new List<Vector3>{Point(47,36),Bays[0].Front,Point(116,36)});
            var parking=Arc(47,-60,12,180,90);Append(parking,new[]{Point(78,-48)});result.Add(parking);
            return result;
        }
        public static List<Vector3> Route(AirportVisitPlan.Site site,bool outward)
        {
            var bay=Bays[0];var path=new List<Vector3>();
            if(!outward)
            {
                Append(path,RunwayConnection(site.reverse));Append(path,ArrivalFork(site.reverse));Append(path,new[]{Point(83,12)});
                // Staging turn remains on concrete; no loop road or permanent circle paint.
                Append(path,Arc(83,24,12,-90,90));
            }
            else
            {
                Append(path,new[]{bay.centre,bay.Front,Point(47,36)});Append(path,DepartureFork(site.reverse));
                var runway=RunwayConnection(!site.reverse);runway.Reverse();Append(path,runway);
            }
            for(int i=0;i<path.Count;i++)path[i]=World(site,path[i]);return path;
        }
        public static float Length(List<Vector3> path)
        {float length=0;for(int i=1;i<path.Count;i++)length+=Vector3.Distance(path[i-1],path[i]);return length;}
    }
}
