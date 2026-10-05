using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Existing houses stay in place. Only low roofs may be overflown.
    public sealed class WorldUpperHighways
    {
        public static bool Enabled=true;
        public const float DeckHeight=18,MaximumLowRoof=13.5f,RoofClearance=2.5f,MinimumLength=512;
        public const int MaximumLowFloors=3;
        readonly ContinuousWorldPlan plan;
        readonly Dictionary<long,WorldHighways.Route> routes=new Dictionary<long,WorldHighways.Route>();
        readonly Dictionary<GameObject,float> roofHeights=new Dictionary<GameObject,float>();
        public int TerrainRejections,BuildingRejections,DeckRejections,Candidates,PopulationRequests,SpatialBuildingChecks;
        public IEnumerable<WorldHighways.Route> PlannedRoutes=>routes.Values;
        public WorldUpperHighways(ContinuousWorldPlan plan){this.plan=plan;}
        public void TrimCache(){if(routes.Count>256)routes.Clear();}
        static int OwnerX(int rx)=>Mathf.FloorToInt((rx+2)/4f)*4-2;
        static int OwnerZ(int rz)=>Mathf.FloorToInt(rz/6f)*6;
        static long Key(int rx,int rz)=>((long)OwnerX(rx)<<32)|(uint)OwnerZ(rz);
        public WorldHighways.Route Region(int rx,int rz){routes.TryGetValue(Key(rx,rz),out var route);return route;}
        public static bool MayReach(int rx,int rz,Rect tile)=>new Rect(OwnerX(rx)*128-3,OwnerZ(rz)*128-3,518,774).Overlaps(tile);
        public static float Height(float distance,float length)
        {
            // Reach the upper tier before rounding over a street corner's low houses.
            float t=Mathf.Clamp01(Mathf.Min(distance,length-distance)/Mathf.Min(224,length*.46f));
            float ease=t<.1f?t*t/.2f:t>.9f?.9f-(1-t)*(1-t)/.2f:t-.05f;
            return Mathf.Lerp(.09f,DeckHeight,ease/.9f);
        }
        public float RoofHeight(GameObject prefab)
        {
            if(roofHeights.TryGetValue(prefab,out float h))return h;
            var fp=prefab.GetComponent<BuildingFootprint>();h=fp.height;
            if(h<=0){h=fp.floors*3+2;foreach(var r in prefab.GetComponentsInChildren<Renderer>(true))h=Mathf.Max(h,r.bounds.max.y-prefab.transform.position.y);}
            roofHeights[prefab]=h;return h;
        }
        List<WorldHighways.Route> CandidatesFor(int ox,int oz)
        {
            var result=new List<WorldHighways.Route>();var lanes=new List<int>{0,1,2,3};float x=ox*128+2,z=oz*128+2;
            lanes.Sort((a,b)=>Mathf.Abs(x+a*128-plan.routeSlope*(z+384)).CompareTo(Mathf.Abs(x+b*128-plan.routeSlope*(z+384))));
            foreach(bool longRoute in new[]{true,false})foreach(int bend in longRoute?new[]{384}:new[]{384,256,512})foreach(int lane in lanes)foreach(int side in new[]{-1,1})
            {
                int destination=lane+side;if(destination<0||destination>3)continue;
                float begin=longRoute?8:bend-224,finish=longRoute?760:bend+224;
                float radius=WorldHighways.TurnRadii[plan.Hash(ox+lane,oz+bend+side,30601)%3];
                var route=new WorldHighways.Route{upperTier=true,noiseBarrier=true,turnRadius=radius};
                WorldHighways.RoundCorners(route,new[]{new Vector2(x+lane*128,z+begin),new Vector2(x+lane*128,z+bend),new Vector2(x+destination*128,z+bend),new Vector2(x+destination*128,z+finish)},radius);
                Measure(route);result.Add(route);
            }
            // Short settlements along the flight axis can still support a long
            // transverse corridor. Its middle S turn stays on collector alignments.
            foreach(int lane in new[]{2,1,3,0,4,5})foreach(int side in new[]{-1,1})
            {
                int destination=lane+side;if(destination<0||destination>5)continue;
                float radius=WorldHighways.TurnRadii[plan.Hash(ox+side,oz+lane,30607)%3];var route=new WorldHighways.Route{upperTier=true,noiseBarrier=true,turnRadius=radius};
                WorldHighways.RoundCorners(route,new[]{new Vector2(x+8,z+lane*128),new Vector2(x+256,z+lane*128),new Vector2(x+256,z+destination*128),new Vector2(x+504,z+destination*128)},radius);Measure(route);result.Add(route);
            }
            foreach(int lane in lanes)foreach(var span in new[]{new Vector2(8,760),new Vector2(64,576),new Vector2(192,704)})
            {var route=new WorldHighways.Route{upperTier=true,noiseBarrier=true};route.points.Add(new Vector2(x+lane*128,z+span.x));route.points.Add(new Vector2(x+lane*128,z+span.y));Measure(route);result.Add(route);}
            return result;
        }
        static void Measure(WorldHighways.Route route){route.length=0;for(int i=1;i<route.points.Count;i++)route.length+=Vector2.Distance(route.points[i-1],route.points[i]);}
        bool TerrainClear(Vector2 p,Vector2 normal)
        {
            for(int side=-1;side<=1;side++){var q=p+normal*2.3f*side;var eco=plan.Ecology(q.x,q.y);if(plan.WaterDistance(q.x,q.y)<5||plan.Height(q.x,q.y)>.1f||plan.Farmland(q.x,q.y)||eco.x+eco.y<.5f||plan.crossings.Reserved(q.x,q.y,4))return false;}
            return true;
        }
        bool LowerDeckClear(Vector2 p,float height)
        {
            var lower=plan.highways.Region(Mathf.FloorToInt(p.x/128),Mathf.FloorToInt(p.y/128));if(lower==null)return true;float walked=0;
            for(int i=1;i<lower.points.Count;i++){var a=lower.points[i-1];var d=lower.points[i]-a;float length=d.magnitude;float t=Mathf.Clamp01(Vector2.Dot(p-a,d)/Mathf.Max(.01f,d.sqrMagnitude));if((p-a-d*t).sqrMagnitude<42.25f&&Mathf.Abs(height-lower.Height(walked+length*t))<3)return false;walked+=length;}
            return true;
        }
        public IEnumerator PrepareRegion(int rx,int rz,WorldPopulationCache population,Func<ContinuousWorldPlan.Region,IEnumerator> ensurePopulation)
        {
            long key=Key(rx,rz);if(WorldLayerHighways.Enabled||!Enabled||routes.ContainsKey(key))yield break;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            foreach(var route in CandidatesFor(OwnerX(rx),OwnerZ(rz)))
            {
                Candidates++;bool valid=true;var samples=new List<Vector3>();float distance=0;
                // Reject terrain and conflicting lower decks before requesting any houses.
                for(int i=1;valid&&i<route.points.Count;i++)
                {
                    var a=route.points[i-1];var b=route.points[i];float length=Vector2.Distance(a,b);var delta=(b-a).normalized;var normal=new Vector2(-delta.y,delta.x);int steps=Mathf.CeilToInt(length/2);
                    for(int s=0;s<=steps;s++)
                    {
                        var p=Vector2.Lerp(a,b,s/(float)steps);float along=distance+length*s/steps;
                        if(!TerrainClear(p,normal)){TerrainRejections++;valid=false;break;}
                        if(!LowerDeckClear(p,route.Height(along))){DeckRejections++;valid=false;break;}
                        samples.Add(new Vector3(p.x,p.y,along));
                        if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
                    }
                    distance+=length;
                }
                if(!valid)continue;
                var crossed=new HashSet<Rect>();var visited=new Dictionary<Vector2Int,WorldPopulationCache.Region>();
                foreach(var sample in samples)
                {
                    var p=new Vector2(sample.x,sample.y);float height=route.Height(sample.z);
                    for(int z=Mathf.FloorToInt((p.y-2.6f)/128);valid&&z<=Mathf.FloorToInt((p.y+2.6f)/128);z++)
                    for(int x=Mathf.FloorToInt((p.x-2.6f)/128);valid&&x<=Mathf.FloorToInt((p.x+2.6f)/128);x++)
                    {
                        var cell=new Vector2Int(x,z);
                        if(!visited.TryGetValue(cell,out var data))
                        {
                            var region=plan.BuildRegion(x,z);PopulationRequests++;
                            if(!population.TryGet(region,out data)){var work=ensurePopulation(region);while(work.MoveNext()){yield return null;budget.Restart();}if(!population.TryGet(region,out data))throw new InvalidOperationException("Missing upper-corridor population");}
                            visited.Add(cell,data);
                        }
                        var nearby=data.Near(p);if(nearby==null)continue;
                        foreach(var building in nearby)
                        {
                            SpatialBuildingChecks++;var r=building.bounds;
                            if(p.x<r.xMin-2.3f||p.x>r.xMax+2.3f||p.y<r.yMin-2.3f||p.y>r.yMax+2.3f)continue;
                            var fp=building.prefab.GetComponent<BuildingFootprint>();float roof=RoofHeight(building.prefab);
                            if(fp.floors>MaximumLowFloors||roof>MaximumLowRoof||height-.5f<roof+RoofClearance){BuildingRejections++;valid=false;break;}
                            crossed.Add(r);
                        }
                    }
                    if(!valid)break;
                    if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
                }
                if(!valid)continue;
                foreach(var data in visited.Values)foreach(var b in data.buildings)route.occupied.Add(b.bounds);
                route.overflownBuildings=crossed.Count;routes[key]=route;yield break;
            }
            routes[key]=null;
        }
    }
}
