using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // The world streamer owns this planner; tiles only consume its committed network.
    // Complete road-to-road corridors provide a safe terminal even if future extension
    // is impossible. No unvalidated, elevated endpoint is ever published.
    public sealed class WorldLayerHighways
    {
        public static bool Enabled=true;
        public const float DeckHeight=16,Clearance=2.5f,Section=256;
        static HighwayEntranceDefinition definition;
        static HighwayEntranceDefinition Definition=>definition?definition:(definition=Resources.Load<HighwayEntranceDefinition>("Transport/TRN-HWY-001"));
        public static float RampLength=>Definition?Definition.approachLength:384;
        public const float MinimumRadius=36;
        readonly ContinuousWorldPlan plan;
        readonly List<WorldHighways.Route> routes=new List<WorldHighways.Route>();
        readonly Dictionary<GameObject,float> roofs=new Dictionary<GameObject,float>();
        readonly HashSet<Vector2Int> attempted=new HashSet<Vector2Int>();
        bool accepted,seen;float nextSeedZ=float.NegativeInfinity;
        public int Candidates,BuildingChecks,PlanningFailures,Revision,NetworksCreated;
        public string State{get;private set;}="waiting";
        public IEnumerable<WorldHighways.Route> Routes=>routes;
        public bool Active=>routes.Count>0;
        public bool Published{get;set;}
        public WorldLayerHighways(ContinuousWorldPlan owner){plan=owner;}
        public static Vector2 Direction(float heading)=>new Vector2(Mathf.Sin(heading*Mathf.Deg2Rad),Mathf.Cos(heading*Mathf.Deg2Rad));
        static Vector2 End(WorldHighways.Route r)=>r.points[r.points.Count-1];
        static void Line(WorldHighways.Route r,float distance){r.points.Add(End(r)+Direction(r.heading)*distance);}
        static void Arc(WorldHighways.Route r,float angle,float radius)
        {
            if(Mathf.Abs(angle)<.01f)return;
            var origin=End(r);var tangent=Direction(r.heading);var right=new Vector2(tangent.y,-tangent.x);
            int steps=Mathf.CeilToInt(Mathf.Abs(angle)/3);float sign=Mathf.Sign(angle);
            for(int k=1;k<=steps;k++){float a=Mathf.Abs(angle)*Mathf.Deg2Rad*k/steps;r.points.Add(origin+tangent*(radius*Mathf.Sin(a))+right*(sign*radius*(1-Mathf.Cos(a))));}
            r.heading+=angle;r.turnRadius=radius;r.turnAngle+=angle;
        }
        static void Measure(WorldHighways.Route r){r.length=0;for(int k=1;k<r.points.Count;k++)r.length+=Vector2.Distance(r.points[k-1],r.points[k]);}
        static WorldHighways.Route New(Vector2 p,float heading){var r=new WorldHighways.Route{upperTier=true,independentLayer=true,noiseBarrier=true,heading=heading};r.points.Add(p);return r;}
        public static WorldHighways.Route Segment(Vector2 start,float heading,float angle,float radius,float tail=128)
        {
            var r=New(start,heading);Line(r,64);Arc(r,angle,radius);Line(r,tail);Measure(r);return r;
        }
        static WorldHighways.Route Ramp(Vector2 start,float heading,bool ascending)
        {
            var r=New(start,heading);r.ramp=true;r.ascending=ascending;r.noiseBarrier=false;Line(r,RampLength);Measure(r);return r;
        }
        List<WorldHighways.Route> Choices(Vector2 start,float heading,float principal,bool recentBend)
        {
            var eco=plan.Ecology(start.x,start.y);
            int hash=plan.Hash(Mathf.RoundToInt(start.x),Mathf.RoundToInt(start.y),31001);
            bool bend=!recentBend&&hash%100<(eco.x+eco.y>.5f?42:20);
            int sign=hash%2==0?1:-1;
            var angles=bend?new[]{15*sign,30*sign,0,-15*sign,-30*sign,45*sign,-45*sign,90*sign,-90*sign}:new[]{0,15*sign,-15*sign,30*sign,-30*sign,45*sign,-45*sign,90*sign,-90*sign};
            var choices=new List<WorldHighways.Route>();
            // 90 degrees is tried last, only after all ordinary headings have failed.
            foreach(int a in angles){
                if(Mathf.Abs(Mathf.DeltaAngle(principal,heading+a))>90)continue;
                foreach(float tail in new[]{128f,64f}){
                    float radius=a==0?MinimumRadius:(hash%3==0?64:hash%3==1?48:MinimumRadius);
                    var candidate=Segment(start,heading,a,radius,tail);
                    var end=End(candidate);
                    if(Mathf.Abs(end.x-plan.routeSlope*end.y)>512)continue;
                    choices.Add(candidate);
                }
            }
            if(Mathf.Abs(start.x-plan.routeSlope*start.y)>240)
                choices.Sort((a,b)=>{
                    float Score(WorldHighways.Route r)=>Mathf.Abs(r.turnAngle)>=89?10000+Mathf.Abs(End(r).x-plan.routeSlope*End(r).y):Mathf.Abs(End(r).x-plan.routeSlope*End(r).y);
                    return Score(a).CompareTo(Score(b));
                });
            return choices;
        }
        float Roof(GameObject prefab)
        {
            if(!roofs.TryGetValue(prefab,out float h)){var fp=prefab.GetComponent<BuildingFootprint>();h=fp?(fp.height>0?fp.height:fp.floors*3+2):20;roofs[prefab]=h;}return h;
        }
        bool GroundConnection(Vector2 p,float heading)
        {
            var dir=Direction(heading);var side=new Vector2(-dir.y,dir.x);
            for(float d=-12;d<=12;d+=2)for(int lane=-1;lane<=1;lane++){
                var q=p+dir*d+side*(lane*1.1f);
                if(!plan.greenSpaces.GrayRoad(q.x,q.y)||plan.Height(q.x,q.y)>.01f||plan.WaterDistance(q.x,q.y)<7)return false;
            }
            return true;
        }
        static float SegmentDistance(Vector2 p,Vector2 a,Vector2 b){var v=b-a;return Vector2.Distance(p,a+v*Mathf.Clamp01(Vector2.Dot(p-a,v)/Mathf.Max(.001f,v.sqrMagnitude)));}
        bool Collision(WorldHighways.Route route,List<WorldHighways.Route> chain)
        {
            for(float d=12;d<=route.length;d+=4){
                var p=route.Position(d,out _);
                foreach(var other in chain)for(int k=1;k<other.points.Count;k++)
                    if(SegmentDistance(p,other.points[k-1],other.points[k])<6)return true;
            }
            return false;
        }
        IEnumerator Validate(WorldHighways.Route route,WorldPopulationCache cache,Func<ContinuousWorldPlan.Region,IEnumerator> ensure)
        {
            accepted=false;Candidates++;var budget=System.Diagnostics.Stopwatch.StartNew();
            var touched=new Dictionary<Vector2Int,WorldPopulationCache.Region>();var footprints=new HashSet<Rect>();
            for(float distance=0;distance<route.length+2;distance+=2){
                float d=Mathf.Min(distance,route.length);var p=route.Position(d,out var tangent);var normal=new Vector2(-tangent.y,tangent.x);float deck=route.Height(d);
                for(int side=-1;side<=1;side++){
                    var q=p+normal*(side*route.HalfWidth);
                    if(plan.Height(q.x,q.y)>(route.ramp?Mathf.Max(.01f,deck-.5f):deck-Clearance-.5f))yield break;
                }
                for(int rz=Mathf.FloorToInt((p.y-4)/128);rz<=Mathf.FloorToInt((p.y+4)/128);rz++)
                for(int rx=Mathf.FloorToInt((p.x-4)/128);rx<=Mathf.FloorToInt((p.x+4)/128);rx++){
                    var key=new Vector2Int(rx,rz);
                    if(!touched.TryGetValue(key,out var data)){
                        var region=plan.BuildRegion(rx,rz);
                        if(!cache.TryGet(region,out data)){var work=ensure(region);while(work.MoveNext()){yield return null;budget.Restart();}if(!cache.TryGet(region,out data))throw new InvalidOperationException("Highway population missing");}
                        touched.Add(key,data);
                    }
                    var near=data.Near(p);if(near==null)continue;
                    foreach(var b in near){
                        BuildingChecks++;var rect=b.bounds;
                        if(p.x<rect.xMin-2.3f||p.x>rect.xMax+2.3f||p.y<rect.yMin-2.3f||p.y>rect.yMax+2.3f)continue;
                        if(Roof(b.prefab)+Clearance>deck-.5f)yield break;footprints.Add(rect);
                    }
                }
                if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
            }
            foreach(var data in touched.Values)foreach(var b in data.buildings)route.occupied.Add(b.bounds);
            // Pick supports separately from the deck. A short roof crossing may use a
            // wider span, but a long unsupported viaduct is rejected before publication.
            float previous=0;
            for(float d=12;d<route.length-4;d+=2){
                if(d-previous<20||route.Height(d)<2)continue;
                var p=route.Position(d,out _);
                if(route.AllowsSupport(p)&&plan.WaterDistance(p.x,p.y)>-3){route.supports.Add(d);previous=d;}
                if(d-previous>100&&route.Height(d)>4)yield break;
            }
            if(route.length-previous>110)yield break;
            route.overflownBuildings=footprints.Count;accepted=true;
        }
        // End connectors reach a real collector road with matching tangent. They are
        // staged with the whole corridor, never grafted onto a visible dead end.
        IEnumerable<WorldHighways.Route[]> Terminals(Vector2 start,float heading)
        {
            float cardinal=Mathf.Round(heading/90)*90,delta=Mathf.DeltaAngle(heading,cardinal);
            for(int offset=0;offset<3;offset++){
                var connector=New(start,heading);Line(connector,64);Arc(connector,delta,48);Line(connector,64);
                var p=End(connector);var dir=Direction(cardinal);var right=new Vector2(dir.y,-dir.x);
                bool vertical=Mathf.Abs(dir.y)>.5f;float coordinate=vertical?p.x:p.y;
                float target=Mathf.Round((coordinate-2)/128)*128+2+(offset==0?0:offset==1?-128:128);
                float shift=(target-coordinate)*(vertical?right.x:right.y);
                if(Mathf.Abs(shift)>.05f){
                    float angle=Mathf.Abs(shift)<14?15:30,rad=MinimumRadius,arcShift=2*rad*(1-Mathf.Cos(angle*Mathf.Deg2Rad));
                    if(Mathf.Abs(shift)<arcShift)continue;
                    float sign=Mathf.Sign(shift);Arc(connector,sign*angle,rad);
                    Line(connector,(Mathf.Abs(shift)-arcShift)/Mathf.Sin(angle*Mathf.Deg2Rad));
                    Arc(connector,-sign*angle,rad);
                }
                Line(connector,48);Measure(connector);
                var ramp=Ramp(End(connector),cardinal,false);
                if(GroundConnection(End(ramp),cardinal))yield return new[]{connector,ramp};
            }
        }
        sealed class Search{public List<WorldHighways.Route> choices;public int next;public bool terminalTried;}
        IEnumerator Complete(WorldHighways.Route entry,WorldPopulationCache cache,Func<ContinuousWorldPlan.Region,IEnumerator> ensure,List<WorldHighways.Route> answer)
        {
            var chain=new List<WorldHighways.Route>{entry};
            var stack=new List<Search>{new Search{choices=Choices(End(entry),entry.heading,entry.heading,false)}};
            int attempts=0;
            while(stack.Count>0&&attempts<160){
                var frame=stack[stack.Count-1];var last=chain[chain.Count-1];
                if(chain.Count>=5&&!frame.terminalTried){
                    frame.terminalTried=true;
                    foreach(var terminal in Terminals(End(last),last.heading)){
                        bool valid=true;var staged=new List<WorldHighways.Route>(chain);
                        foreach(var r in terminal){
                            if(Collision(r,staged)){valid=false;break;}
                            var work=Validate(r,cache,ensure);while(work.MoveNext())yield return null;
                            if(!accepted){valid=false;break;}staged.Add(r);
                        }
                        if(valid){answer.AddRange(staged);yield break;}
                    }
                }
                if(chain.Count>=10||frame.next>=frame.choices.Count){stack.RemoveAt(stack.Count-1);chain.RemoveAt(chain.Count-1);continue;}
                var candidate=frame.choices[frame.next++];attempts++;
                if(Collision(candidate,chain))continue;
                var check=Validate(candidate,cache,ensure);while(check.MoveNext())yield return null;if(!accepted)continue;
                chain.Add(candidate);stack.Add(new Search{choices=Choices(End(candidate),candidate.heading,entry.heading,candidate.turnRadius>0)});
            }
        }
        public static bool Intersects(WorldHighways.Route route,Rect bounds)
        {
            bounds=new Rect(bounds.xMin-5,bounds.yMin-5,bounds.width+10,bounds.height+10);
            for(int k=1;k<route.points.Count;k++){
                var a=route.points[k-1];var b=route.points[k];int steps=Mathf.Max(1,Mathf.CeilToInt(Vector2.Distance(a,b)/4));
                for(int n=0;n<=steps;n++)if(bounds.Contains(Vector2.Lerp(a,b,n/(float)steps)))return true;
            }
            return false;
        }
        public void Observe(ICollection<Vector2Int> loaded,float currentZ)
        {
            if(!Active)return;
            foreach(var tile in loaded)foreach(var route in routes)if(Intersects(route,TerrainStreamer.TileBounds(tile))){seen=true;return;}
            if(!seen){
                float nearest=float.MaxValue,lastZ=float.MinValue;
                foreach(var route in routes)foreach(var p in route.points){nearest=Mathf.Min(nearest,Mathf.Abs(p.y-currentZ));lastZ=Mathf.Max(lastZ,p.y);}
                if(nearest<4096&&currentZ<lastZ+256)return;
            }
            routes.Clear();seen=false;Published=false;Revision++;State="waiting";nextSeedZ=currentZ+128;
        }
        public IEnumerator PrepareArea(Rect area,Rect protectedView,bool initial,WorldPopulationCache cache,Func<ContinuousWorldPlan.Region,IEnumerator> ensure,Func<Rect> currentView=null)
        {
            if(!Enabled||Active||area.yMax<nextSeedZ)yield break;
            if(attempted.Count>1024)attempted.Clear();
            State="planning";
            var keys=new List<Vector2Int>();
            for(int z=Mathf.FloorToInt(area.yMin/128);z<=Mathf.FloorToInt(area.yMax/128);z++)
            for(int x=Mathf.FloorToInt(area.xMin/128);x<=Mathf.FloorToInt(area.xMax/128);x++){var key=new Vector2Int(x,z);if(!attempted.Contains(key))keys.Add(key);}
            // Prefer roads near the future camera footprint, with enough seeded
            // variation to avoid always selecting the same central collector.
            keys.Sort((a,b)=>{
                float A=Mathf.Abs(a.x*128+64-area.center.x)+(plan.Hash(a.x,a.y,31051)%128)*.75f;
                float B=Mathf.Abs(b.x*128+64-area.center.x)+(plan.Hash(b.x,b.y,31051)%128)*.75f;
                return A.CompareTo(B);
            });
            int entries=0;
            foreach(var key in keys){
                attempted.Add(key);var region=plan.BuildRegion(key.x,key.y);
                var candidates=new List<WorldHighways.Route>();
                foreach(var road in region.roads){
                    if(Mathf.Min(road.width,road.height)<(Definition?Definition.minimumRoadWidth:3.9f)||Mathf.Max(road.width,road.height)<32)continue;
                    bool vertical=road.height>road.width;
                    foreach(int sign in new[]{-1,1}){
                        float heading=vertical?(sign>0?0:180):(sign>0?90:270);
                        var p=road.center;
                        if(!area.Contains(p)||!GroundConnection(p,heading))continue;
                        candidates.Add(Ramp(p,heading,true));
                    }
                }
                candidates.Sort((a,b)=>plan.Hash((int)a.points[0].x,(int)a.points[0].y,(int)a.heading+31053).CompareTo(plan.Hash((int)b.points[0].x,(int)b.points[0].y,(int)b.heading+31053)));
                foreach(var entry in candidates){
                    if(++entries>24){State="waiting";yield break;}
                    if(!initial&&Intersects(entry,protectedView))continue;
                    var work=Validate(entry,cache,ensure);while(work.MoveNext())yield return null;if(!accepted)continue;
                    var answer=new List<WorldHighways.Route>();work=Complete(entry,cache,ensure,answer);while(work.MoveNext())yield return null;
                    if(answer.Count==0){PlanningFailures++;continue;}
                    var exclusion=currentView!=null?currentView():protectedView;
                    bool visible=false;if(!initial)foreach(var r in answer)if(Intersects(r,exclusion)){visible=true;break;}if(visible)continue;
                    for(int k=0;k<answer.Count;k++){answer[k].previous=k>0?answer[k-1]:null;answer[k].next=k+1<answer.Count?answer[k+1]:null;}
                    routes.AddRange(answer);Published=false;Revision++;NetworksCreated++;State="running";yield break;
                }
                yield return null;
            }
            State="waiting";
        }
        // Editor compatibility; runtime never asks an individual tile to plan.
        public IEnumerator Prepare(float minimum,float maximum,WorldPopulationCache cache,Func<ContinuousWorldPlan.Region,IEnumerator> ensure)
        {return PrepareArea(new Rect(-256,minimum,512,maximum-minimum),default,true,cache,ensure);}
    }
}
