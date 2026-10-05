using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Finite local catchments, expressed as cardinal/diagonal reaches. No perpetual central river.
    public sealed class WorldHydrology
    {
        public struct Reach
        {
            public Vector2 a,b;public float width;public int order;public bool bevel,rounded;
            public float Distance(float x,float z)
            {
                var d=b-a;float length=d.magnitude;var direction=length>.0001f?d/length:Vector2.zero;var p=new Vector2(x,z)-a;
                float u=Mathf.Clamp(Vector2.Dot(p,direction),0,length);
                return (p-direction*u).magnitude-width;
            }
        }
        readonly ContinuousWorldPlan world;readonly Dictionary<int,List<Reach>> basins=new Dictionary<int,List<Reach>>();
        public WorldHydrology(ContinuousWorldPlan world){this.world=world;}
        public void TrimCache(){if(basins.Count>32)basins.Clear();}
        public List<Reach> Basin(int id)
        {
            if(basins.TryGetValue(id,out var cached))return cached;
            var list=new List<Reach>();float z=id*768;var random=new System.Random(world.Hash(id,0,1701));
            if(random.Next(100)<12){basins.Add(id,list);return list;}
            float width=world.ZoneAt(0,z+256)==ContinuousWorldPlan.EcologyZone.RiverPlain?24+random.Next(13):7+random.Next(12);
            bool transverse=random.Next(100)<40;
            int steps=2+random.Next(4);
            var points=new List<Vector2>{new Vector2((random.Next(9)-4)*32,z+48+random.Next(3)*16)};
            int trend=random.Next(2)*2-1;
            for(int step=0;step<steps;step++)
            {
                var p=points[points.Count-1];points.Add(p+Vector2.up*(Mathf.Floor(544f/steps/16)*16+random.Next(3)*16));
                if(step==steps-1||random.Next(100)>70)continue;
                p=points[points.Count-1];if(p.x>64)trend=-1;if(p.x< -64)trend=1;
                float nextX=Mathf.Clamp(p.x+trend*(96+random.Next(2)*32),-160,160);
                if(Mathf.Abs(nextX-p.x)>32)points.Add(new Vector2(nextX,p.y));
            }
            AddPath(list,points,width,0,z,transverse);
            var trunks=list.FindAll(r=>!r.bevel);
            int branchCount=random.Next(4);
            for(int branch=0;branch<branchCount&&trunks.Count>0;branch++)
            {
                if(random.Next(100)>=65)continue;
                var trunk=trunks[random.Next(trunks.Count)];var root=Vector2.Lerp(trunk.a,trunk.b,.3f+(float)random.NextDouble()*.4f);
                var direction=(trunk.b-trunk.a).normalized;var normal=new Vector2(-direction.y,direction.x)*(random.Next(2)*2-1);
                var tip=root+normal*(64+random.Next(3)*32);float narrow=Mathf.Max(3,width*(.3f+(float)random.NextDouble()*.2f));
                AddPath(list,new List<Vector2>{root,tip,tip+direction*64},narrow,1,z,transverse);
                if(random.Next(100)<40){var mid=Vector2.Lerp(root,tip,.5f);AddPath(list,new List<Vector2>{mid,mid-direction*64},Mathf.Max(2,narrow*.55f),2,z,transverse);}
            }
            // Translate the whole catchment, preserving its cardinal reaches and bevels.
            float offset=Mathf.Round(world.routeSlope*(z+384)/32)*32;
            for(int i=0;i<list.Count;i++)
            {
                var reach=list[i];
                // Rotate the entire catchment, including branches and bevels. This changes
                // the visible topology without shearing cardinal reaches into diagonals.
                if(transverse){reach.a=new Vector2(reach.a.y-z-384,z+384-reach.a.x);reach.b=new Vector2(reach.b.y-z-384,z+384-reach.b.x);}
                reach.a.x+=offset;reach.b.x+=offset;list[i]=reach;
            }
            basins.Add(id,list);return list;
        }
        void AddPath(List<Reach> reaches,List<Vector2> points,float width,int order,float basinZ,bool transverse)
        {
            var trim=new float[points.Count];
            for(int i=1;i<points.Count-1;i++)
            {
                var incoming=(points[i]-points[i-1]).normalized;var outgoing=(points[i+1]-points[i]).normalized;
                if(Mathf.Abs(Vector2.Dot(incoming,outgoing))<.01f)trim[i]=Mathf.Min(Mathf.Max(20,width*1.7f),Mathf.Min(Vector2.Distance(points[i-1],points[i]),Vector2.Distance(points[i],points[i+1]))*.3f);
            }
            for(int i=0;i<points.Count-1;i++)
            {
                var direction=(points[i+1]-points[i]).normalized;var a=points[i]+direction*trim[i];var b=points[i+1]-direction*trim[i+1];
                if((b-a).sqrMagnitude>.01f)reaches.Add(new Reach{a=a,b=b,width=width,order=order});
                if(i<points.Count-2&&trim[i+1]>0){
                    var next=(points[i+2]-points[i+1]).normalized;
                    // Classify in final world coordinates without consuming topology RNG.
                    var corner=points[i+1];
                    if(transverse)corner=new Vector2(corner.y-basinZ-384,basinZ+384-corner.x);
                    corner.x+=Mathf.Round(world.routeSlope*(basinZ+384)/32)*32;
                    var zone=world.ZoneAt(corner.x,corner.y);
                    bool natural=zone==ContinuousWorldPlan.EcologyZone.Forest||zone==ContinuousWorldPlan.EcologyZone.Hills||zone==ContinuousWorldPlan.EcologyZone.RiverPlain;
                    if(zone==ContinuousWorldPlan.EcologyZone.Town||zone==ContinuousWorldPlan.EcologyZone.CoastalTown)
                        natural=world.Hash(Mathf.RoundToInt(corner.x/32),Mathf.RoundToInt(corner.y/32),1799)%100<35;
                    if(!natural){reaches.Add(new Reach{a=b,b=points[i+1]+next*trim[i+1],width=width,order=order,bevel=true});continue;}
                    float angle=Vector2.SignedAngle(direction,next),radius=trim[i+1];
                    var normal=new Vector2(-direction.y,direction.x)*Mathf.Sign(angle);
                    var center=b+normal*radius;var radial=b-center;var previous=b;
                    int samples=Mathf.RoundToInt(Mathf.Abs(angle)/15);
                    for(int s=1;s<=samples;s++){float theta=angle*s/samples*Mathf.Deg2Rad;var point=center+new Vector2(radial.x*Mathf.Cos(theta)-radial.y*Mathf.Sin(theta),radial.x*Mathf.Sin(theta)+radial.y*Mathf.Cos(theta));reaches.Add(new Reach{a=previous,b=point,width=width,order=order,bevel=true,rounded=true});previous=point;}
                }
            }
        }
        public float RiverDistance(float x,float z)
        {
            int id=Mathf.FloorToInt(z/768);float distance=10000;
            for(int i=id-1;i<=id+1;i++)foreach(var reach in Basin(i))distance=Mathf.Min(distance,reach.Distance(x,z));return distance;
        }
        public float Distance(float x,float z)
        {
            var zone=world.ZoneRange(z,out float begin,out float end);
            float localX=x-Mathf.Round(world.routeSlope*(begin+end)*.5f/32)*32;
            float marine=10000;
            if(zone==ContinuousWorldPlan.EcologyZone.Ocean||zone==ContinuousWorldPlan.EcologyZone.CoastalTown)
            {
                float left=zone==ContinuousWorldPlan.EcologyZone.Ocean?-224:48;
                marine=Octagon(localX,z,(left+400)/2,(begin+end)/2,(400-left)/2,(end-begin)/2-12,48);
            }
            float water=Mathf.Min(RiverDistance(x,z),marine);
            if(zone==ContinuousWorldPlan.EcologyZone.Ocean)
            {
                float center=(begin+end)/2;
                int region=Mathf.FloorToInt(begin);float island=10000;
                int count=1+world.Hash(region,0,1771)%4;
                for(int i=0;i<count;i++)
                {
                    float cx=-160+world.Hash(region,i,1773)%321,cz=center-120+world.Hash(region,i,1777)%241;
                    float rx=20+world.Hash(region,i,1781)%45,rz=20+world.Hash(region,i,1783)%49;
                    island=Mathf.Min(island,Octagon(localX,z,cx,cz,rx,rz,Mathf.Min(rx,rz)*.5f));
                }
                water=Mathf.Max(water,-island);
            }
            return water;
        }
        static float Octagon(float x,float z,float cx,float cz,float rx,float rz,float bevel)
        {
            x=Mathf.Abs(x-cx);z=Mathf.Abs(z-cz);return Mathf.Max(Mathf.Max(x-rx,z-rz),(x+z-rx-rz+bevel)*.70710678f);
        }
    }
}


