using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace OnAir
{
    public sealed class BlockGenerator : MonoBehaviour
    {
        public WorldKit kit;
        public BuildingData buildings;
        public int seed=2409;
        public Weather weather=Weather.Clear;
        public DayPeriod period=DayPeriod.Day;
        public Biome biome=Biome.City;
        [Range(0,1)] public float buildingDensity=1;
        public bool streamingRoads;
        public Transform generated;
        public int BuildingCount {get;private set;}
        // 8 metre road tiles and lots. This demo is a static 64 x 64 metre block.
        public static bool IsRoad(int x,int z) => x>=0&&x<8&&z>=0&&z<8&&(x==0||x==4||x==7||z==0||z==4||z==7);
        static bool IsPark(int x,int z) => x>=1&&x<=2&&z>=5&&z<=6;
        public static int RoadMask(int x,int z) => (IsRoad(x,z+1)?1:0)|(IsRoad(x+1,z)?2:0)|(IsRoad(x,z-1)?4:0)|(IsRoad(x-1,z)?8:0);
        static Vector3 Cell(int x,int z) => new Vector3(-28+8*x,0,-28+8*z);
        [ContextMenu("Rebuild block")]
        public void Rebuild()
        {
            if(!kit)throw new InvalidOperationException("Assign the World Kit.");
            if(buildings==null)throw new InvalidOperationException("Building data must be supplied before generating a block.");
            var rules=buildings.Rules;var prefabs=buildings.Prefabs;
            foreach(var r in rules)if(!prefabs.ContainsKey(r.PrefabId)||!prefabs[r.PrefabId].GetComponent<BuildingFootprint>())throw new InvalidOperationException("Missing footprint or prefab: "+r.PrefabId);
            foreach(var r in rules)if(r.MinScale!=1||r.MaxScale!=1)throw new InvalidOperationException("Static block uses authored footprints. Set min_scale and max_scale to 1: "+r.Id);
            if(generated)
            {
                generated.gameObject.SetActive(false);
                if(Application.isPlaying)Destroy(generated.gameObject);else DestroyImmediate(generated.gameObject);
            }
            generated=new GameObject("Generated street block").transform;generated.SetParent(transform,false);
            if(biome==Biome.City && kit.cityAsphalt && kit.cityPaving && kit.cityPaint)
            {
                BuildingCount=DenseCityLayout.Build(this);
                return;
            }
            var roads=new GameObject("Road network").transform;roads.SetParent(generated,false);
            var lots=new GameObject("Building lots").transform;lots.SetParent(generated,false);
            var greenery=new GameObject("Park and street furniture").transform;greenery.SetParent(generated,false);
            var random=new System.Random(seed);BuildingCount=0;string last=null;int repeats=0;
            for(int z=0;z<8;z++)for(int x=0;x<8;x++)
            {
                var position=Cell(x,z);
                if(IsRoad(x,z))
                {
                    int mask=RoadMask(x,z);
                    if(streamingRoads&&(x==0||x==4||x==7)){if(z==0)mask|=4;if(z==7)mask|=1;}
                    PlaceRoad(mask,position,roads);continue;
                }
                Place(IsPark(x,z)?kit.park:kit.pavement,position,0,lots);
                if(IsPark(x,z))
                {
                    Place((x+z)%2==0?kit.tree:kit.treeTall,position+new Vector3(-2,0,2),0,greenery);
                    Place(kit.tree,position+new Vector3(2,0,2),0,greenery);
                    Place(kit.bench,position+new Vector3(-2,0,-2),90,greenery);
                    Place(kit.planter,position+new Vector3(2,0,-2),0,greenery);continue;
                }
                if(buildingDensity<1&&random.NextDouble()>buildingDensity){Place(kit.tree,position,0,greenery);continue;}
                int roadX=NearestRoad(x),roadZ=NearestRoad(z);
                bool faceX=Math.Abs(x-roadX)<Math.Abs(z-roadZ);
                Vector3 front=faceX?new Vector3(Math.Sign(roadX-x),0,0):new Vector3(0,0,Math.Sign(roadZ-z));
                bool corner=IsRoad(x-1,z)||IsRoad(x+1,z);
                corner &= IsRoad(x,z-1)||IsRoad(x,z+1);
                int maxFloors=corner?2:(Math.Min(Math.Abs(x-roadX),Math.Abs(z-roadZ))>1?5:4);
                var eligible=rules.Where(r=>r.Matches(biome,weather,period)&&r.Weight>0&&Fits(prefabs[r.PrefabId],maxFloors)).ToList();
                if(maxFloors==5)eligible=eligible.Where(r=>prefabs[r.PrefabId].GetComponent<BuildingFootprint>().floors>=4).ToList();
                var nonRepeated=eligible.Where(r=>repeats<2||r.PrefabId!=last).ToList();
                var picked=BuildingRules.Pick(nonRepeated,biome,weather,period,random);
                if(picked==null){Place(kit.planter,position,0,greenery);continue;}
                float yaw=Quaternion.LookRotation(-front).eulerAngles.y;
                var building=Place(prefabs[picked.PrefabId],position,yaw,lots);building.name="Lot "+x+","+z+" / "+picked.Id+" / "+picked.PrefabId;
                if(Vector3.Dot(building.GetComponent<BuildingFootprint>().EntranceDirection,front)<.99f)throw new Exception("Entrance orientation mismatch.");
                repeats=picked.PrefabId==last?repeats+1:1;last=picked.PrefabId;BuildingCount++;
                // Furniture sits on the extra lot setback, outside the road's 1 m sidewalk.
                Vector3 side=Vector3.Cross(Vector3.up,front);
                Place((x+z)%2==0?kit.tree:kit.planter,position+front*3.6f+side*3.5f,0,greenery);
                if((x+z)%3==0)Place(kit.lamp,position+front*3.7f-side*3.4f,yaw,greenery);
            }
        }
        static bool Fits(GameObject prefab,int floors)
        {
            var footprint=prefab.GetComponent<BuildingFootprint>();return footprint.floors<=floors&&footprint.size.x<=7.95f&&footprint.size.y<=7.95f;
        }
        static int NearestRoad(int cell)
        {
            int best=0;foreach(int road in new[]{4,7})if(Math.Abs(cell-road)<Math.Abs(cell-best))best=road;return best;
        }
        void PlaceRoad(int mask,Vector3 pos,Transform parent)
        {
            GameObject prefab;int canonical;
            int count=0;for(int i=0;i<4;i++)if((mask&(1<<i))!=0)count++;
            if(count==4){prefab=kit.roadCross;canonical=15;}
            else if(count==3){prefab=kit.roadT;canonical=11;}
            else if(mask==5||mask==10){prefab=kit.roadStraight;canonical=5;}
            else {prefab=kit.roadCorner;canonical=3;}
            for(int turn=0;turn<4;turn++)
            {
                if(canonical==mask){Place(prefab,pos,turn*90,parent);return;}
                canonical=((canonical<<1)&15)|(canonical>>3);
            }
            throw new Exception("Unsupported road connection mask: "+mask);
        }
        static GameObject Place(GameObject prefab,Vector3 pos,float yaw,Transform parent)
        {
            if(!prefab)throw new InvalidOperationException("Missing city kit prefab.");
            GameObject item;
#if UNITY_EDITOR
            item=!Application.isPlaying?(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent):Instantiate(prefab,parent);
#else
            item=Instantiate(prefab,parent);
#endif
            item.transform.localPosition=pos;item.transform.localRotation=Quaternion.Euler(0,yaw,0);return item;
        }
    }
}
