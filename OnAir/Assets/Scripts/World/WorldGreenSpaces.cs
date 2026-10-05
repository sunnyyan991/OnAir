using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Independent deterministic placement: never consumes the building/road random stream.
    public sealed partial class WorldGreenSpaces
    {
        readonly ContinuousWorldPlan plan;
        readonly Dictionary<Vector2Int,List<WorldPopulationCache.Building>> plots=new Dictionary<Vector2Int,List<WorldPopulationCache.Building>>();
        public WorldGreenSpaces(ContinuousWorldPlan plan){this.plan=plan;}
        public void Trim(){if(plots.Count>256){plots.Clear();parks.Clear();}}
        public List<WorldPopulationCache.Building> Prepare(ContinuousWorldPlan.Region region,WorldKit kit)
        {
            Trim();
            if(plots.TryGetValue(region.coordinates,out var result))return result;
            result=new List<WorldPopulationCache.Building>();plots.Add(region.coordinates,result);
            if(kit.greenSpaceAssets==null||kit.greenSpaceAssets.Length!=5)return result;
            foreach(var parcel in region.parcels)
            {
                var area=parcel.bounds;int hash=plan.Hash((int)area.x,(int)area.y,30401);
                var ecology=plan.Ecology(area.center.x,area.center.y);
                if(ecology.x+ecology.y<.65f||hash%100>=48)continue;
                // One candidate per parcel; large uses first, with smaller fallbacks.
                int roll=(hash/100)%100;
                int[] order=roll<15?new[]{4,2,1,0}:roll<40?new[]{3,2,1,0}:roll<70?new[]{2,1,0}:new[]{0,1};
                bool placed=false;
                foreach(int kind in order)
                {
                    if(kind==1)continue; // Pocket parks now follow the enclosed green block, not a fixed square prefab.
                    var prefab=kit.greenSpaceAssets[kind];if(!prefab)continue;
                    if(kind==0&&plan.Sample(area.center.x,area.center.y)==ContinuousWorldPlan.Land.Urban&&(hash/997)%100>=14)continue;
                    var size=prefab.GetComponent<BuildingFootprint>().size;
                    for(int attempt=0;attempt<8&&!placed;attempt++)
                    {
                        int rotation=attempt%4*90;var extent=rotation%180==0?size:new Vector2(size.y,size.x);
                        if(extent.x+2>area.width||extent.y+2>area.height)continue;
                        float tx=attempt<4?.5f:((hash>>(attempt+1))&255)/255f;
                        float tz=attempt<4?.5f:((hash>>(attempt+4))&255)/255f;
                        var lot=new Rect(area.xMin+1+(area.width-extent.x-2)*tx,area.yMin+1+(area.height-extent.y-2)*tz,extent.x,extent.y);
                        if(!Suitable(lot,kind==0))continue;
                        result.Add(new WorldPopulationCache.Building{prefab=prefab,bounds=lot,rotation=rotation});placed=true;
                    }
                    if(placed)break;
                }
            }
            PrepareParks(region,result);return result;
        }
        public bool Suitable(Rect lot,bool pavedShrine=false)
        {
            int nx=Mathf.CeilToInt(lot.width/2),nz=Mathf.CeilToInt(lot.height/2);
            for(int iz=0;iz<=nz;iz++)for(int ix=0;ix<=nx;ix++)
            {
                float x=lot.xMin+lot.width*ix/nx,z=lot.yMin+lot.height*iz/nz;
                var land=plan.Sample(x,z);
                if((land!=ContinuousWorldPlan.Land.Park&&!(pavedShrine&&land==ContinuousWorldPlan.Land.Urban))||plan.Height(x,z)>.01f||plan.Farmland(x,z)||GrayRoad(x,z)||plan.DiagonalReserved(x,z)||plan.crossings.Reserved(x,z,2)||plan.highways.Reserved(x,z))return false;
            }
            return true;
        }
        public bool Reserved(float x,float z)
        {
            var region=plan.BuildRegion(Mathf.FloorToInt(x/128),Mathf.FloorToInt(z/128));
            if(plots.TryGetValue(region.coordinates,out var list))foreach(var item in list)if(item.bounds.Contains(new Vector2(x,z)))return true;
            if(parks.TryGetValue(region.coordinates,out var spaces))foreach(var park in spaces)if(park.Reserved(new Vector2(x,z)))return true;
            return false;
        }
    }
}
