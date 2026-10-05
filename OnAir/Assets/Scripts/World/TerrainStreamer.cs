using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    [DefaultExecutionOrder(200)] // After CameraFollow.LateUpdate; before the camera renders.
    public sealed class TerrainStreamer : MonoBehaviour
    {
        public const float SegmentLength=64;
        public JourneyController journey;public BuildingData buildings;public FlightController flight;
        public Camera sceneCamera;public EnvironmentController environment;public AircraftCabinView wingView;
        [System.Serializable] public struct LandscapeMemory {public int seed,version,slice;public Weather weather;public DayPeriod period;public float solarPhase,routeSlope;}
        readonly Dictionary<int,LandscapeMemory> memories=new Dictionary<int,LandscapeMemory>();
        public IEnumerable<LandscapeMemory> Memories=>memories.Values;
        [Min(60)] public float retainedFlightSeconds=60;
        [Range(2,12)] public float generationBudgetMs=6;
        [Range(2,12)] public float lookAheadSeconds=6;
        public int RetainedSlices=>Mathf.Max(3,Mathf.CeilToInt(flight.speedMetersPerSecond*Mathf.Max(60,retainedFlightSeconds)/journey.Current.terrain.distanceMeters)+1);
        // Compatibility diagnostic. Continuous loading no longer generates this entire strip.
        public float CorridorHalfWidth=>Mathf.Max(256,Mathf.Ceil((sceneCamera.orthographicSize*(sceneCamera.aspect*.71f+1)+Mathf.Max(80,flight.altitude+60)*.71f+32)/64)*64);
        public int VisibleFirst{get;private set;}public int VisibleLast{get;private set;}
        public int ActiveBuildingCount{get;private set;}public int GenerationCount{get;private set;}
        public int PreparedSlices{get;private set;}public double MaxPreparationStepMs{get;private set;}
        public string SlowestPreparationStage{get;private set;}
        public int EmergencyCompletions{get;private set;}public int MissingVisibleTiles{get;private set;}
        public int RequiredTileCount=>required.Count;public int ResidentTileCount=>chunks.Count;
        public float LoadedGroundArea=>chunks.Count*64*64;
        public bool HasPreparedNext=>pending!=null;
        public IEnumerable<GameObject> Chunks=>chunks.Values;
        readonly Dictionary<Vector2Int,GameObject> chunks=new Dictionary<Vector2Int,GameObject>();
        readonly HashSet<Vector2Int> required=new HashSet<Vector2Int>(),wanted=new HashSet<Vector2Int>();
        readonly HashSet<Vector2Int> ensure=new HashSet<Vector2Int>();
        readonly List<Vector2> demandPoints=new List<Vector2>(32),sweepPoints=new List<Vector2>(64),viewHull=new List<Vector2>(32),wantedHull=new List<Vector2>(64);
        readonly List<Vector2Int> expired=new List<Vector2Int>();
        readonly List<int> expiredMemories=new List<int>();
        ContinuousWorldPlan plan;int revision=-1,teleport=-1;
        WorldPopulationCache populationCache=new WorldPopulationCache();
        IEnumerator pending;GameObject pendingObject;Vector2Int pendingKey;
        Vector2 viewCenter;float nextTrim;
        Vector2 velocity;
        IEnumerator highwayWork;ContinuousWorldLayout highwayPlanner;float nextHighwayCheck;
        bool initialHighway;
        public string HighwayState=>plan?.layerHighways.State;
        public int HighwayNetworksCreated=>plan?.layerHighways.NetworksCreated??0;
        public static Rect TileBounds(Vector2Int key)=>new Rect(key.x*64,key.y*64-32,64,64);
        public double InitialBuildMs{get;private set;}
        void Start(){var watch=System.Diagnostics.Stopwatch.StartNew();Refresh();InitialBuildMs=watch.Elapsed.TotalMilliseconds;}
        void LateUpdate(){Refresh(false);Pump(generationBudgetMs);PumpHighways(pending==null?4:1.5f);}
        void CancelHighways(){(highwayWork as System.IDisposable)?.Dispose();highwayWork=null;if(highwayPlanner)Destroy(highwayPlanner.gameObject);highwayPlanner=null;}
        void ClearChunks(){CancelHighways();initialHighway=false;CancelPending();foreach(var go in chunks.Values){go.SetActive(false);Destroy(go);}chunks.Clear();memories.Clear();populationCache.palette.Dispose();populationCache=new WorldPopulationCache();}
        public void RebuildAll(){ClearChunks();revision=-1;Refresh();}
        public void Refresh(bool immediate=true)
        {
            journey.Initialize();bool reset=revision!=journey.Revision||teleport!=journey.TeleportRevision;
            if(reset)
            {
                if(flight)flight.Tick(0);var follow=sceneCamera.GetComponent<CameraFollow>();if(follow)follow.Follow(0);
                if(revision!=journey.Revision){ClearChunks();plan=new ContinuousWorldPlan(journey.seed,flight.routeSlope);revision=journey.Revision;}
                if(teleport!=journey.TeleportRevision)CancelHighways();
                teleport=journey.TeleportRevision;
            }
            if(!journey.continuousWorld){RefreshLegacy();return;}
            var cameraFollow=sceneCamera.GetComponent<CameraFollow>();if(cameraFollow&&cameraFollow.enabled)cameraFollow.Follow(0);
            BuildDemand();
            if(pending!=null&&!wanted.Contains(pendingKey))CancelPending();
            // Teleport/resize/overload safety net. Ordinary travel must be served by prefetch;
            // EmergencyCompletions exposes stalls instead of silently counting them as success.
            ensure.Clear();ensure.UnionWith(required);
            if(reset||immediate)foreach(var key in wanted)if(ArrivalTime(key)<3)ensure.Add(key);
            foreach(var key in ensure)if(!chunks.ContainsKey(key))
            {
                if(!reset&&!immediate){EmergencyCompletions++;Debug.LogWarning("Streaming emergency at frame "+Time.frameCount+" tile "+key+" pending "+(pending!=null?pendingKey.ToString():"none"));}
                if(pending!=null&&pendingKey==key){while(pending.MoveNext()){}Publish();}
                else
                {
                    var go=new GameObject("World tile "+key);go.SetActive(false);go.transform.SetParent(transform,false);
                    var layout=go.AddComponent<ContinuousWorldLayout>();layout.CommercialViewDirection=-sceneCamera.transform.forward;
                    layout.Build(journey.seed,key.y,journey.Current.terrain.profile.kit,buildings,plan,flight.routeSlope,256,TileBounds(key),populationCache);Commit(key,go);
                }
            }
            expired.Clear();ActiveBuildingCount=0;
            foreach(var pair in chunks)
            {
                if(!wanted.Contains(pair.Key)){expired.Add(pair.Key);continue;}
                pair.Value.transform.localPosition=new Vector3(0,0,(pair.Key.y-journey.Current.index)*64);
                ActiveBuildingCount+=pair.Value.GetComponent<ContinuousWorldLayout>().BuildingCount;
            }
            foreach(var key in expired){chunks[key].SetActive(false);Destroy(chunks[key]);chunks.Remove(key);GenerationCount++;}
            MissingVisibleTiles=0;foreach(var key in required)if(!chunks.ContainsKey(key))MissingVisibleTiles++;
            Remember();if(Time.unscaledTime>=nextTrim){plan.TrimCaches();nextTrim=Time.unscaledTime+2;}
        }
        void Remember()
        {
            int current=journey.Current.index;
            for(int i=current-RetainedSlices;i<=current;i++)if(!memories.ContainsKey(i))memories[i]=new LandscapeMemory{seed=journey.seed,version=ContinuousWorldPlan.GeneratorVersion,slice=i,weather=journey.context.weather,period=journey.context.period,solarPhase=journey.context.solarPhase,routeSlope=flight.routeSlope};
            expiredMemories.Clear();foreach(int i in memories.Keys)if(i<current-RetainedSlices||i>current)expiredMemories.Add(i);foreach(int i in expiredMemories)memories.Remove(i);
        }
        void BuildDemand(float footprintMargin=12,float cacheGuard=8)
        {
            var points=demandPoints;points.Clear();float origin=journey.Current.index*64;
            float top=Mathf.Max(80,flight.ObstacleCeiling+8);
            for(int y=0;y<2;y++)for(int x=0;x<2;x++)for(int level=0;level<2;level++)
            {
                var ray=sceneCamera.ViewportPointToRay(new Vector3(x,y,0));if(Mathf.Abs(ray.direction.y)<.001f)continue;
                var p=ray.GetPoint((level*top-ray.origin.y)/ray.direction.y);
                for(int dz=-1;dz<=1;dz+=2)for(int dx=-1;dx<=1;dx+=2)points.Add(new Vector2(p.x+dx*footprintMargin,p.z+origin+dz*footprintMargin));
            }
            if(wingView&&wingView.IsCabin&&wingView.CabinCamera)
            {
                // Finite perspective footprint, with a straight far plane hidden behind
                // the final haze band. Keep the trapezoid rather than its wasteful AABB:
                // the two rear corners of that rectangle can never enter this view.
                points.Clear();var camera=wingView.CabinCamera;var eye=camera.transform.position;
                for(int dz=-1;dz<=1;dz+=2)for(int dx=-1;dx<=1;dx+=2)
                    points.Add(new Vector2(eye.x+dx*24,eye.z+origin+dz*24));
                for(int y=0;y<2;y++)for(int x=0;x<2;x++)
                {
                    var p=camera.ViewportToWorldPoint(new Vector3(x,y,AircraftCabinView.HazeEnd+32));
                    for(int dz=-1;dz<=1;dz+=2)for(int dx=-1;dx<=1;dx+=2)
                        points.Add(new Vector2(p.x+dx*footprintMargin,p.z+origin+dz*footprintMargin));
                }
            }
            Hull(points,viewHull);var hull=viewHull;required.Clear();Collect(hull,required);
            float speed=flight.speedMetersPerSecond*64/journey.Current.terrain.distanceMeters;velocity=new Vector2(flight.routeSlope*speed,speed);
            viewCenter=Vector2.zero;foreach(var p in hull)viewCenter+=p;viewCenter/=Mathf.Max(1,hull.Count);
            VisibleFirst=int.MaxValue;VisibleLast=int.MinValue;foreach(var k in required){VisibleFirst=Mathf.Min(VisibleFirst,k.y);VisibleLast=Mathf.Max(VisibleLast,k.y);}
            float lead=Mathf.Max(24,flight.speedMetersPerSecond*64/journey.Current.terrain.distanceMeters*lookAheadSeconds);
            var forward=new Vector2(flight.routeSlope*lead,lead);var sweep=sweepPoints;sweep.Clear();
            // Side/rear hysteresis covers sway and changing cruise height, and prevents
            // boundary tiles being destroyed and immediately requested again.
            foreach(var p in hull)for(int dz=-1;dz<=1;dz+=2)for(int dx=-1;dx<=1;dx+=2){var guard=p+new Vector2(dx*cacheGuard,dz*cacheGuard);sweep.Add(guard);sweep.Add(guard+forward);}
            Hull(sweep,wantedHull);wanted.Clear();Collect(wantedHull,wanted);
        }
#if UNITY_EDITOR || DEVELOPMENT_BUILD
        public Vector2Int ReviewDemandCounts(float seconds){float previous=lookAheadSeconds;lookAheadSeconds=seconds;BuildDemand();var result=new Vector2Int(required.Count,wanted.Count);lookAheadSeconds=previous;BuildDemand();return result;}
        public Vector2Int ReviewLegacyDemandCounts(){BuildDemand(24,16);var result=new Vector2Int(required.Count,wanted.Count);BuildDemand();return result;}
#endif
        static float Cross(Vector2 a,Vector2 b,Vector2 c)=>(b.x-a.x)*(c.y-a.y)-(b.y-a.y)*(c.x-a.x);
        float ArrivalTime(Vector2Int key)
        {
            var r=TileBounds(key);float enter=0,leave=float.PositiveInfinity;
            // Swept separating-axis test: deadline is when the moving view reaches this tile,
            // not its distance from the centre (which incorrectly favours side/rear tiles).
            for(int i=-2;i<viewHull.Count;i++)
            {
                Vector2 axis=i==-2?Vector2.right:i==-1?Vector2.up:new Vector2(-(viewHull[(i+1)%viewHull.Count]-viewHull[i]).y,(viewHull[(i+1)%viewHull.Count]-viewHull[i]).x).normalized;
                float low=float.MaxValue,high=float.MinValue;foreach(var p in viewHull){float d=Vector2.Dot(axis,p);low=Mathf.Min(low,d);high=Mathf.Max(high,d);}
                float mid=Vector2.Dot(axis,r.center),radius=(Mathf.Abs(axis.x)+Mathf.Abs(axis.y))*32,v=Vector2.Dot(axis,velocity);
                if(Mathf.Abs(v)<.00001f){if(high<mid-radius||low>mid+radius)return 100000;continue;}
                float a=(mid-radius-high)/v,b=(mid+radius-low)/v;enter=Mathf.Max(enter,Mathf.Min(a,b));leave=Mathf.Min(leave,Mathf.Max(a,b));
                if(leave<enter)return 100000;
            }
            return enter;
        }
        static void Hull(List<Vector2> points,List<Vector2> h)
        {
            points.Sort((a,b)=>a.x==b.x?a.y.CompareTo(b.y):a.x.CompareTo(b.x));h.Clear();
            foreach(var p in points){while(h.Count>=2&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}
            int lower=h.Count;for(int i=points.Count-2;i>=0;i--){var p=points[i];while(h.Count>lower&&Cross(h[h.Count-2],h[h.Count-1],p)<=0)h.RemoveAt(h.Count-1);h.Add(p);}if(h.Count>1)h.RemoveAt(h.Count-1);
        }
        static void Collect(List<Vector2> polygon,HashSet<Vector2Int> result)
        {
            if(polygon.Count<3)return;float minX=float.MaxValue,maxX=float.MinValue,minZ=float.MaxValue,maxZ=float.MinValue;
            foreach(var p in polygon){minX=Mathf.Min(minX,p.x);maxX=Mathf.Max(maxX,p.x);minZ=Mathf.Min(minZ,p.y);maxZ=Mathf.Max(maxZ,p.y);}
            for(int z=Mathf.FloorToInt((minZ+32)/64);z<=Mathf.FloorToInt((maxZ+32)/64);z++)for(int x=Mathf.FloorToInt(minX/64);x<=Mathf.FloorToInt(maxX/64);x++)
            {
                var k=new Vector2Int(x,z);var r=TileBounds(k);bool outside=false;
                for(int i=0;i<polygon.Count;i++){var a=polygon[i];var b=polygon[(i+1)%polygon.Count];if(Cross(a,b,new Vector2(r.xMin,r.yMin))<0&&Cross(a,b,new Vector2(r.xMin,r.yMax))<0&&Cross(a,b,new Vector2(r.xMax,r.yMin))<0&&Cross(a,b,new Vector2(r.xMax,r.yMax))<0){outside=true;break;}}
                if(!outside)result.Add(k);
            }
        }
        void Pump(float milliseconds)
        {
            if(!journey.continuousWorld)return;var budget=System.Diagnostics.Stopwatch.StartNew();
            while(budget.Elapsed.TotalMilliseconds<milliseconds)
            {
                if(pending==null)
                {
                    bool found=false;Vector2Int chosen=default;float best=float.MaxValue;
                    foreach(var key in wanted)if(!chunks.ContainsKey(key))
                    {
                        float score=ArrivalTime(key)*100000+(TileBounds(key).center-viewCenter).sqrMagnitude;
                        if(score<best){best=score;chosen=key;found=true;}
                    }
                    if(!found)return;pendingKey=chosen;pendingObject=new GameObject("Preparing tile "+chosen);pendingObject.SetActive(false);pendingObject.transform.SetParent(transform,false);
                    var layout=pendingObject.AddComponent<ContinuousWorldLayout>();layout.CommercialViewDirection=-sceneCamera.transform.forward;
                    pending=layout.BuildIncrementally(journey.seed,chosen.y,journey.Current.terrain.profile.kit,buildings,plan,flight.routeSlope,256,TileBounds(chosen),populationCache);
                }
                var step=System.Diagnostics.Stopwatch.StartNew();bool more=pending.MoveNext();step.Stop();if(step.Elapsed.TotalMilliseconds>MaxPreparationStepMs){MaxPreparationStepMs=step.Elapsed.TotalMilliseconds;SlowestPreparationStage=pendingObject.GetComponent<ContinuousWorldLayout>().BuildStage;}
                if(!more){Publish();PreparedSlices++;}
            }
        }
        void Publish(){var go=pendingObject;var key=pendingKey;(pending as System.IDisposable)?.Dispose();pending=null;pendingObject=null;Commit(key,go);}
        Rect DemandBounds()
        {
            float left=float.MaxValue,right=float.MinValue,bottom=float.MaxValue,top=float.MinValue;
            foreach(var p in wantedHull){left=Mathf.Min(left,p.x);right=Mathf.Max(right,p.x);bottom=Mathf.Min(bottom,p.y);top=Mathf.Max(top,p.y);}
            return Rect.MinMaxRect(left,bottom,right,top);
        }
        void PumpHighways(float milliseconds)
        {
            if(!journey.continuousWorld||!WorldLayerHighways.Enabled||wanted.Count==0)return;
            var timer=System.Diagnostics.Stopwatch.StartNew();
            if(highwayWork==null){
                if(Time.unscaledTime<nextHighwayCheck)return;
                nextHighwayCheck=Time.unscaledTime+2;
                plan.layerHighways.Observe(wanted,journey.Current.index*64);
                if(!highwayPlanner){
                    var obj=new GameObject("Independent highway planning");obj.SetActive(false);obj.transform.SetParent(transform,false);
                    highwayPlanner=obj.AddComponent<ContinuousWorldLayout>();highwayPlanner.CommercialViewDirection=-sceneCamera.transform.forward;
                    highwayPlanner.ConfigurePlanner(plan,journey.Current.terrain.profile.kit,buildings);
                }
                highwayWork=PlanHighways();
            }
            while(timer.Elapsed.TotalMilliseconds<milliseconds&&highwayWork!=null)
                if(!highwayWork.MoveNext()){(highwayWork as System.IDisposable)?.Dispose();highwayWork=null;}
        }
        IEnumerator PlanHighways()
        {
            var view=DemandBounds();
            float ahead=view.yMax+128,width=Mathf.Min(384,view.width);
            // The deck-height camera footprint is offset from the aircraft's ground position.
            var ray=sceneCamera.ViewportPointToRay(new Vector3(.5f,.5f,0));
            var centre=ray.GetPoint((WorldLayerHighways.DeckHeight-ray.origin.y)/ray.direction.y);
            float centreX=centre.x+flight.routeSlope*(ahead+192-centre.z-journey.Current.index*64);
            var area=initialHighway?view:new Rect(centreX-width*.5f,ahead,width,384);
            var work=plan.layerHighways.PrepareArea(area,view,initialHighway,populationCache,r=>highwayPlanner.PrepareHighwayPopulation(r,populationCache),DemandBounds);
            while(work.MoveNext())yield return null;
            initialHighway=false;
            // Stage all currently resident pieces before exposing a new network.
            // A tile never starts or steers a route; tiles only clip its finished mesh.
            var snapshot=new List<GameObject>(chunks.Values);
            foreach(var go in snapshot){
                if(!go)continue;
                work=go.GetComponent<ContinuousWorldLayout>().SyncHighways(true);
                while(go&&work.MoveNext())yield return null;
            }
            plan.layerHighways.Published=true;
            foreach(var go in chunks.Values)if(go)go.GetComponent<ContinuousWorldLayout>().RevealHighways();
        }
        void Commit(Vector2Int key,GameObject go)
        {
            go.name="World tile "+key;go.transform.localPosition=new Vector3(0,0,(key.y-journey.Current.index)*64);
            var transport=go.GetComponent<ContinuousWorldLayout>().SyncHighways();while(transport.MoveNext()){}
            if(environment)environment.PrepareWindows(go);go.AddComponent<DistantDetailShadows>().sceneCamera=sceneCamera;chunks.Add(key,go);go.SetActive(true);GenerationCount++;
        }
        void CancelPending(){(pending as System.IDisposable)?.Dispose();pending=null;if(pendingObject)Destroy(pendingObject);pendingObject=null;}
        void OnDestroy(){CancelHighways();CancelPending();populationCache.palette.Dispose();}
        void CreateLegacy(JourneyController.Leg leg)
        {
            var terrain=leg.terrain;var go=new GameObject("Terrain "+leg.index+" / "+terrain.biome);go.transform.SetParent(transform,false);
            var layout=go.AddComponent<BlockGenerator>();layout.kit=terrain.profile.kit;layout.buildings=buildings;layout.seed=leg.seed;layout.biome=terrain.biome;layout.weather=leg.entryWeather;layout.period=leg.entryPeriod;layout.buildingDensity=terrain.buildingDensity;layout.streamingRoads=true;layout.Rebuild();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Terrain base";ground.transform.SetParent(go.transform,false);ground.transform.localPosition=new Vector3(0,-.7f,0);ground.transform.localScale=new Vector3(192,1,64.02f);ground.GetComponent<Renderer>().sharedMaterial=terrain.profile.ground;Destroy(ground.GetComponent<Collider>());
            if(terrain.biome!=Biome.City && terrain.profile.scenery)for(int i=0;i<5;i++){var prop=Instantiate(terrain.profile.scenery,go.transform);prop.transform.localPosition=new Vector3(i%2==0?-38:38,0,-24+i*12);}
            chunks.Add(new Vector2Int(0,leg.index),go);GenerationCount++;
        }

        void RefreshLegacy()
        {
            int current=journey.Current.index;
            for(int i=0;i<3;i++){var leg=journey.Ahead(i);var key=new Vector2Int(0,leg.index);if(chunks.ContainsKey(key))continue;
                CreateLegacy(leg);}

            expired.Clear();ActiveBuildingCount=0;foreach(var pair in chunks){if(pair.Key.y<current-1||pair.Key.y>current+2){expired.Add(pair.Key);continue;}pair.Value.transform.localPosition=Vector3.forward*((pair.Key.y-current)*64);ActiveBuildingCount+=pair.Value.GetComponent<BlockGenerator>().BuildingCount;}
            foreach(var key in expired){Destroy(chunks[key]);chunks.Remove(key);}
        }
    }
}
