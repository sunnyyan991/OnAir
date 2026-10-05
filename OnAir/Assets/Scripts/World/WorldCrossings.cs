using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // A crossing owns its bridge and both approaches in one local coordinate frame.
    public sealed class WorldCrossings
    {
        public sealed class Crossing
        {
            public Vector2 center, direction, riverDirection;
            public float halfWater, halfLength, width;
            public bool natural;
            public readonly List<Rect> lots=new List<Rect>();
            public readonly List<Vector4> links=new List<Vector4>();
            public float Distance(Vector2 p,float margin=0)
            {
                var q=p-center;float along=Vector2.Dot(q,direction),side=Vector2.Dot(q,riverDirection);
                return Mathf.Max(Mathf.Abs(along)-halfLength-margin,Mathf.Abs(side)-width*.5f-margin);
            }
        }
        readonly ContinuousWorldPlan plan;
        readonly Dictionary<int,List<Crossing>> cache=new Dictionary<int,List<Crossing>>();
        public WorldCrossings(ContinuousWorldPlan plan){this.plan=plan;}
        public void TrimCache(){if(cache.Count>32)cache.Clear();}
        public List<Crossing> Basin(int id)
        {
            if(cache.TryGetValue(id,out var saved))return saved;
            var result=new List<Crossing>();cache[id]=result;
            foreach(var reach in plan.hydrology.Basin(id))
            {
                float span=(reach.b-reach.a).magnitude;
                if(reach.bevel||reach.order>1||span<70)continue;
                var tangent=(reach.b-reach.a).normalized;
                if(Mathf.Abs(tangent.x)>.001f&&Mathf.Abs(tangent.y)>.001f)continue;
                var normal=new Vector2(-tangent.y,tangent.x);var middle=(reach.a+reach.b)*.5f;
                bool nature=plan.Ecology(middle.x,middle.y).z>.5f;
                var candidates=new List<Vector2>();
                float clearance=reach.width+12;
                if(nature){for(float t=clearance;t<span-clearance;t+=32)candidates.Add(reach.a+tangent*t);}
                else
                {
                    float lo=Mathf.Min(Mathf.Abs(tangent.x)>.5f?reach.a.x:reach.a.y,Mathf.Abs(tangent.x)>.5f?reach.b.x:reach.b.y);
                    float hi=lo+span;
                    for(float coordinate=Mathf.Ceil((lo+clearance-2)/128)*128+2;coordinate<hi-clearance;coordinate+=128)
                        candidates.Add(Mathf.Abs(tangent.x)>.5f?new Vector2(coordinate,middle.y):new Vector2(middle.x,coordinate));
                }
                candidates.Sort((a,b)=>Vector2.SqrMagnitude(a-middle).CompareTo(Vector2.SqrMagnitude(b-middle)));
                foreach(var center in candidates)
                {
                    if(plan.ZoneAt(center.x,center.y)==ContinuousWorldPlan.EcologyZone.Ocean||plan.WaterDistance(center.x,center.y)>-reach.width*.6f)continue;
                    if(result.Exists(c=>Vector2.Distance(c.center,center)<120))continue;
                    foreach(float extension in nature?new[]{0f}:new[]{72f,48f})
                    {
                        float length=reach.width+7+extension;bool valid=true;
                        for(int side=-1;valid&&side<=1;side+=2)for(float d=reach.width+7;valid&&d<=length;d+=2)
                        {
                            var p=center+normal*d*side;
                            if(plan.WaterDistance(p.x,p.y)<6||plan.Height(p.x,p.y)>0)valid=false;
                            if(!nature&&d>=reach.width+10&&!RoadLanding(p))valid=false;
                        }
                        if(!valid)continue;
                        var crossing=new Crossing{center=center,direction=normal,riverDirection=tangent,halfWater=reach.width,halfLength=length,width=nature?2.2f:3.8f,natural=nature};
                        if(result.Exists(other=>Envelope(other).Overlaps(Envelope(crossing))))continue;
                        result.Add(crossing);
                        break;
                    }
                    if(result.Count>=3)return result;
                }
            }
            return result;
        }
        public IEnumerable<Crossing> Near(float z)
        {
            int id=Mathf.FloorToInt(z/768);
            for(int i=id-1;i<=id+1;i++)foreach(var c in Basin(i))yield return c;
        }
        public static Rect Envelope(Crossing c)
        {
            var half=new Vector2(Mathf.Abs(c.direction.x)*c.halfLength+Mathf.Abs(c.riverDirection.x)*c.width*.5f+4,Mathf.Abs(c.direction.y)*c.halfLength+Mathf.Abs(c.riverDirection.y)*c.width*.5f+4);
            return new Rect(c.center-half,half*2);
        }
        public bool LotAt(float x,float z){foreach(var c in Near(z))if(c.lots.Exists(r=>r.Contains(new Vector2(x,z))))return true;return false;}
        bool Connect(Crossing c)
        {
            // Reserve access first; decorative bridgehead lots must not obstruct a connection.
            c.lots.Clear();int connected=0;
            for(int side=-1;side<=1;side+=2)
            {
                var from=c.center+c.direction*c.halfLength*side;float best=110;Vector2 chosen=from,corner=from;
                int rx=Mathf.FloorToInt(from.x/128),rz=Mathf.FloorToInt(from.y/128);
                for(int iz=rz-1;iz<=rz+1;iz++)for(int ix=rx-1;ix<=rx+1;ix++)foreach(var road in plan.BuildRegion(ix,iz).roads)
                {
                    var to=new Vector2(Mathf.Clamp(from.x,road.xMin+.75f,road.xMax-.75f),Mathf.Clamp(from.y,road.yMin+.75f,road.yMax-.75f));
                    to=new Vector2(Mathf.Floor(to.x/2)*2+1,Mathf.Floor(to.y/2)*2+1);
                    if(!road.Contains(to)||!RoadLanding(to))continue;
                    var d=to-from;float length=Mathf.Abs(d.x)+Mathf.Abs(d.y);if(length>=best)continue;
                    var bend=Mathf.Abs(c.direction.x)>.5f?new Vector2(to.x,from.y):new Vector2(from.x,to.y);
                    if(!ClearLink(from,bend,c)||!ClearLink(bend,to,c))continue;best=length;chosen=to;corner=bend;
                }
                if(best<110){connected++;if((corner-from).sqrMagnitude>.01f)c.links.Add(new Vector4(from.x,from.y,corner.x,corner.y));if((chosen-corner).sqrMagnitude>.01f)c.links.Add(new Vector4(corner.x,corner.y,chosen.x,chosen.y));}
            }
            return connected==2;
        }
        bool RoadLanding(Vector2 p)
        {
            for(int z=-1;z<=1;z++)for(int x=-1;x<=1;x++)
            {float xx=p.x+x,zz=p.y+z;if(plan.WaterDistance(xx,zz)<9||plan.Height(xx,zz)>.001f||plan.Ecology(xx,zz).z>.6f||plan.Farmland(xx,zz))return false;}
            return true;
        }
        bool ClearLink(Vector2 a,Vector2 b,Crossing c)
        {
            float length=(b-a).magnitude;var direction=length>.001f?(b-a)/length:Vector2.right;var normal=new Vector2(-direction.y,direction.x);
            for(float t=0;t<=length+1;t+=1)for(int side=-1;side<=1;side++)
            {
                var p=Vector2.Lerp(a,b,Mathf.Clamp01(t/Mathf.Max(.001f,length)))+normal*(c.width*.5f+.3f)*side;
                if(plan.WaterDistance(p.x,p.y)<7||plan.Height(p.x,p.y)>.001f||plan.Farmland(p.x,p.y)||c.lots.Exists(r=>r.Contains(p)))return false;
            }
            return true;
        }
        public static float LinkDistance(Vector2 p,Vector4 link,float width)
        {
            var a=new Vector2(link.x,link.y);var b=new Vector2(link.z,link.w);var d=b-a;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.001f,d.sqrMagnitude));return Vector2.Distance(p,a+d*t)-width*.5f;
        }
        public bool Reserved(float x,float z,float margin=1)
        {
            var p=new Vector2(x,z);
            foreach(var c in Near(z))if(c.Distance(p,margin)<0||c.lots.Exists(r=>r.Contains(p))||c.links.Exists(l=>LinkDistance(p,l,c.width+margin*2)<0))return true;return false;
        }
    }
}


