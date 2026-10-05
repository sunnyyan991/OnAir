using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed partial class WorldGreenSpaces
    {
        public struct Furniture {public Vector2 position;public float yaw;public bool lamp;}
        public sealed class ParkPatch
        {
            public Rect bounds;public readonly List<Rect> paths=new List<Rect>();public readonly List<Furniture> furniture=new List<Furniture>();
            public bool Reserved(Vector2 p){foreach(var path in paths)if(new Rect(path.xMin-.6f,path.yMin-.6f,path.width+1.2f,path.height+1.2f).Contains(p))return true;foreach(var f in furniture)if((f.position-p).sqrMagnitude<2)return true;return false;}
        }
        readonly Dictionary<Vector2Int,List<ParkPatch>> parks=new Dictionary<Vector2Int,List<ParkPatch>>();
        public IReadOnlyList<ParkPatch> Parks(ContinuousWorldPlan.Region region)=>parks.TryGetValue(region.coordinates,out var found)?found:System.Array.Empty<ParkPatch>();
        public bool GrayRoad(float x,float z)
        {
            // Match the actual 2m raster used by ContinuousWorldLayout, including
            // removed roads at water, farms and hills. A BSP parcel alone is insufficient.
            float gx=Mathf.Floor(x/2)*2,gz=Mathf.Floor(z/2)*2;var centre=new Vector2(gx+1,gz+1);
            var region=plan.BuildRegion(Mathf.FloorToInt(centre.x/128),Mathf.FloorToInt(centre.y/128));bool road=false;
            foreach(var r in region.roads)if(r.Contains(centre)){road=true;break;}if(!road)return false;
            for(int i=0;i<5;i++){float px=i==4?gx+1:gx+(i%2)*2,pz=i==4?gz+1:gz+(i/2)*2;
                if(plan.crossings.LotAt(px,pz)||plan.ZoneAt(px,pz)==ContinuousWorldPlan.EcologyZone.Ocean||plan.Farmland(px,pz)||plan.WaterDistance(px,pz)<7||plan.Height(px,pz)>0||(plan.Sample(px,pz)==ContinuousWorldPlan.Land.Forest&&plan.Ecology(px,pz).x<=.55f))return false;}
            return true;
        }
        bool Enclosed(Rect r)
        {
            for(float x=r.xMin+1;x<r.xMax;x+=2)if(!GrayRoad(x,r.yMin-1)||!GrayRoad(x,r.yMax+1))return false;
            for(float z=r.yMin+1;z<r.yMax;z+=2)if(!GrayRoad(r.xMin-1,z)||!GrayRoad(r.xMax+1,z))return false;
            return true;
        }
        bool Green(Vector2 p,List<WorldPopulationCache.Building> sites)
        {
            var land=plan.Sample(p.x,p.y);if(land!=ContinuousWorldPlan.Land.Park&&land!=ContinuousWorldPlan.Land.Forest)return false;
            if(plan.Height(p.x,p.y)>0||plan.Farmland(p.x,p.y)||plan.WaterDistance(p.x,p.y)<8||plan.highways.Reserved(p.x,p.y)||plan.crossings.Reserved(p.x,p.y,2)||plan.DiagonalReserved(p.x,p.y))return false;
            foreach(var b in sites)if(new Rect(b.bounds.xMin-.5f,b.bounds.yMin-.5f,b.bounds.width+1,b.bounds.height+1).Contains(p))return false;
            return true;
        }
        static Rect Intersection(Rect a,Rect b){float x=Mathf.Max(a.xMin,b.xMin),z=Mathf.Max(a.yMin,b.yMin);return new Rect(x,z,Mathf.Max(0,Mathf.Min(a.xMax,b.xMax)-x),Mathf.Max(0,Mathf.Min(a.yMax,b.yMax)-z));}
        void PrepareParks(ContinuousWorldPlan.Region region,List<WorldPopulationCache.Building> sites)
        {
            var result=new List<ParkPatch>();parks[region.coordinates]=result;
            foreach(var parcel in region.parcels)
            {
                var r=parcel.bounds;int h=plan.Hash((int)(r.x*4),(int)(r.y*4),30617);var eco=plan.Ecology(r.center.x,r.center.y);
                if(h%100>=55||eco.x+eco.y<.65f||r.width<8||r.height<8||!Enclosed(r))continue;
                var cells=new List<Rect>();float greenArea=0;
                for(float z=r.yMin;z<r.yMax;z+=4)for(float x=r.xMin;x<r.xMax;x+=4)
                {
                    var cell=new Rect(x,z,Mathf.Min(4,r.xMax-x),Mathf.Min(4,r.yMax-z));bool ok=true;
                    for(int k=0;k<5;k++){var p=k==4?cell.center:new Vector2(k%2==0?cell.xMin+.1f:cell.xMax-.1f,k/2==0?cell.yMin+.1f:cell.yMax-.1f);if(!Green(p,sites)){ok=false;break;}}
                    if(ok){cells.Add(cell);greenArea+=cell.width*cell.height;}
                }
                if(greenArea<64||greenArea<r.width*r.height*.55f)continue;
                var patch=new ParkPatch{bounds=r};var lines=new List<Rect>();float width=Mathf.Clamp(Mathf.Min(r.width,r.height)/12,1.25f,2.25f);
                float px=Mathf.Lerp(r.xMin,r.xMax,.38f+(h%19)*.012f),pz=Mathf.Lerp(r.yMin,r.yMax,.38f+((h/19)%19)*.012f);
                lines.Add(new Rect(r.xMin,pz-width/2,r.width,width));lines.Add(new Rect(px-width/2,r.yMin,width,r.height));
                float spacing=16+(h/37)%9;
                for(float z=pz-spacing;z>r.yMin+4;z-=spacing)lines.Add(new Rect(r.xMin,z-.45f,r.width,.9f));
                for(float z=pz+spacing;z<r.yMax-4;z+=spacing)lines.Add(new Rect(r.xMin,z-.45f,r.width,.9f));
                for(float x=px-spacing;x>r.xMin+4;x-=spacing)lines.Add(new Rect(x-.45f,r.yMin,.9f,r.height));
                for(float x=px+spacing;x<r.xMax-4;x+=spacing)lines.Add(new Rect(x-.45f,r.yMin,.9f,r.height));
                float pathArea=0;int lineIndex=0;
                foreach(var line in lines)
                {
                    if(lineIndex++>1&&pathArea>greenArea*.2f)break;
                    foreach(var cell in cells){var clip=Intersection(cell,line);if(clip.width>.01f&&clip.height>.01f){patch.paths.Add(clip);pathArea+=clip.width*clip.height;}}
                    bool horizontal=line.width>line.height;float length=horizontal?line.width:line.height;
                    for(float d=5;d<length-3;d+=12+(h%5))
                    {
                        bool lamp=patch.furniture.Count%2==1;float offset=(horizontal?line.height:line.width)/2+.85f;
                        var p=horizontal?new Vector2(line.xMin+d,line.center.y+offset):new Vector2(line.center.x+offset,line.yMin+d);
                        if(patch.furniture.Count>=Mathf.Clamp(Mathf.FloorToInt(greenArea/90),2,12))break;
                        if(!r.Contains(p)||!Green(p,sites)||!Green(p+Vector2.one*.8f,sites)||!Green(p-Vector2.one*.8f,sites)||patch.Reserved(p))continue;
                        patch.furniture.Add(new Furniture{position=p,yaw=horizontal?0:90,lamp=lamp});
                    }
                }
                if(patch.paths.Count>0)result.Add(patch);
            }
        }
    }
}
