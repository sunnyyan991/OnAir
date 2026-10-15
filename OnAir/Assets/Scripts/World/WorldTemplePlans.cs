using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed partial class WorldGreenSpaces
    {
        sealed class TempleSite { public Vector2Int owner;public WorldPopulationCache.Building building; }
        readonly Dictionary<int,TempleSite> templeSites=new Dictionary<int,TempleSite>();
        static bool TempleZone(ContinuousWorldPlan.EcologyZone zone)=>zone==ContinuousWorldPlan.EcologyZone.City||zone==ContinuousWorldPlan.EcologyZone.Town;
        void AddTemple(ContinuousWorldPlan.Region region,WorldKit kit,List<WorldPopulationCache.Building> result)
        {
            var checkedZones=new HashSet<int>();
            for(float z=region.coordinates.y*128;z<=region.coordinates.y*128+128;z+=32){
                var zone=plan.ZoneRange(z,out float begin,out float end);if(!TempleZone(zone))continue;
                for(int i=0;i<128;i++){if(plan.ZoneRange(begin-1,out float a,out _)!=zone)break;begin=a;}
                for(int i=0;i<128;i++){if(plan.ZoneRange(end+1,out _,out float b)!=zone)break;end=b;}
                int key=Mathf.RoundToInt(begin);if(!checkedZones.Add(key))continue;
                if(!templeSites.TryGetValue(key,out var site)){
                    site=ChooseTemple(begin,end,kit.greenSpaceAssets[4]);
                    // Cache eviction is safe: selection uses only world geometry and a stable hash.
                    if(templeSites.Count>128)templeSites.Clear();templeSites[key]=site;
                }
                if(site!=null&&site.owner==region.coordinates)result.Add(site.building);
            }
        }
        TempleSite ChooseTemple(float begin,float end,GameObject prefab)
        {
            if(!prefab||plan.Hash((int)begin,0,38101)%100>=80)return null;
            var candidates=new List<ContinuousWorldPlan.Parcel>();
            for(int rz=Mathf.FloorToInt(begin/128);rz<=Mathf.FloorToInt(end/128);rz++){
                int center=Mathf.FloorToInt(plan.routeSlope*(rz*128+64)/128);
                for(int rx=center-2;rx<=center+1;rx++)foreach(var p in plan.BuildRegion(rx,rz).parcels)
                    if(p.bounds.center.y>begin+48&&p.bounds.center.y<end-48&&p.bounds.width>=26&&p.bounds.height>=26)candidates.Add(p);
            }
            candidates.Sort((a,b)=>plan.Hash((int)a.bounds.x,(int)a.bounds.y,38107).CompareTo(plan.Hash((int)b.bounds.x,(int)b.bounds.y,38107)));
            var size=prefab.GetComponent<BuildingFootprint>().size;
            foreach(var parcel in candidates){var area=parcel.bounds;
                for(int rot=0;rot<2;rot++){var extent=rot==0?size:new Vector2(size.y,size.x);if(extent.x+2>area.width||extent.y+2>area.height)continue;
                    for(int k=0;k<9;k++){float tx=(k%3)*.5f,tz=(k/3)*.5f;var lot=new Rect(area.xMin+1+(area.width-extent.x-2)*tx,area.yMin+1+(area.height-extent.y-2)*tz,extent.x,extent.y);
                        if(lot.yMin<begin+48||lot.yMax>end-48||!TempleSuitable(lot))continue;
                        return new TempleSite{owner=new Vector2Int(Mathf.FloorToInt(area.center.x/128),Mathf.FloorToInt(area.center.y/128)),building=new WorldPopulationCache.Building{prefab=prefab,bounds=lot,rotation=rot*90}};
                    }
                }
            }
            return null;
        }
        public bool TempleSuitable(Rect lot)
        {
            for(float z=lot.yMin;z<=lot.yMax+1;z+=2)for(float x=lot.xMin;x<=lot.xMax+1;x+=2){float px=Mathf.Min(x,lot.xMax),pz=Mathf.Min(z,lot.yMax);var land=plan.Sample(px,pz);
                if(!TempleZone(plan.ZoneAt(px,pz))||(land!=ContinuousWorldPlan.Land.Park&&land!=ContinuousWorldPlan.Land.Forest)||plan.Height(px,pz)>.01f||plan.AirportReserved(px,pz,8)||plan.WaterDistance(px,pz)<8||plan.Farmland(px,pz)||GrayRoad(px,pz)||plan.DiagonalReserved(px,pz)||plan.crossings.Reserved(px,pz,2)||plan.highways.Reserved(px,pz))return false;
            }
            return true;
        }
    }
}
