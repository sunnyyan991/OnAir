using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Collector-only elevated corridors. Region ownership makes routes deterministic across slices.
    public sealed class WorldHighways
    {
        public sealed class Route
        {
            public readonly List<Vector2> points=new List<Vector2>();
            public bool noiseBarrier,upperTier,independentLayer,ramp,ascending;
            public Route previous,next;
            public float heading,turnAngle;
            public readonly List<float> supports=new List<float>();
            public Route parentRoute;public float mergeDistance;
            public readonly List<Vector3> exits=new List<Vector3>(); // distance, railing side, opening length
            public float length,turnRadius;
            public readonly List<Rect> occupied=new List<Rect>();
            public int overflownBuildings;
            public float HalfWidth=>upperTier?2.1f:2.9f;
            public float Height(float distance)=>independentLayer?(ramp?Mathf.Lerp(ascending?.11f:WorldLayerHighways.DeckHeight,ascending?WorldLayerHighways.DeckHeight:.11f,Mathf.SmoothStep(0,1,Mathf.Clamp01(distance/Mathf.Max(1,length)))):WorldLayerHighways.DeckHeight):upperTier?WorldUpperHighways.Height(distance,length):ContinuousWorldLayout.HighwayHeight(distance,length);
            public Vector2 Position(float distance,out Vector2 tangent)
            {
                for(int i=1;i<points.Count;i++){var delta=points[i]-points[i-1];float size=delta.magnitude;if(distance<=size||i==points.Count-1){tangent=delta/Mathf.Max(.001f,size);return points[i-1]+tangent*Mathf.Clamp(distance,0,size);}distance-=size;}
                tangent=Vector2.up;return points[0];
            }
            public bool AllowsSupport(Vector2 point)
            {
                foreach(var rect in occupied)if(new Rect(rect.xMin-1,rect.yMin-1,rect.width+2,rect.height+2).Contains(point))return false;
                return true;
            }
        }
        public static readonly float[] TurnRadii={24,36,48};
        public const float AngleStep=15;
        readonly ContinuousWorldPlan plan;readonly Dictionary<long,Route> cache=new Dictionary<long,Route>();
        public WorldHighways(ContinuousWorldPlan plan){this.plan=plan;}
        public void TrimCache(){if(cache.Count>256)cache.Clear();}
        public Route Region(int rx,int rz)
        {
            if(WorldLayerHighways.Enabled)return null;
            int ownerX=Mathf.FloorToInt(rx/4f)*4,ownerZ=Mathf.FloorToInt(rz/6f)*6;
            long key=((long)ownerX<<32)|(uint)ownerZ;
            if(cache.TryGetValue(key,out var saved))return saved;
            var work=PrepareRegion(rx,rz);while(work.MoveNext()){}
            return cache[key];
        }
        public System.Collections.IEnumerator PrepareRegion(int rx,int rz)
        {
            if(WorldLayerHighways.Enabled)yield break;
            // A 4 x 6 owner supports long collectors in either direction, shared across slices.
            int ownerX=Mathf.FloorToInt(rx/4f)*4,ownerZ=Mathf.FloorToInt(rz/6f)*6;
            long key=((long)ownerX<<32)|(uint)ownerZ;if(cache.ContainsKey(key))yield break;
            var settlement=plan.Ecology(ownerX*128+256,ownerZ*128+384);
            int chance=settlement.x+settlement.y>=.5f?95:85;
            if(plan.Hash(ownerX,ownerZ,4001)%100>=chance){cache[key]=null;yield break;}
            var budget=System.Diagnostics.Stopwatch.StartNew();
            float x=ownerX*128+2,z=ownerZ*128+2;
            var bend=new Route{noiseBarrier=plan.Hash(ownerX,ownerZ,4013)%3!=0};
            // Try different main-road lanes and positions before falling back to
            // a straight corridor. Curves must still pass the entire clearance test.
            var candidates=new List<Vector2>();
            for(int lane=0;lane<3;lane++)for(int placement=0;placement<2;placement++)candidates.Add(new Vector2(lane,placement));
            candidates.Sort((a,b)=>Mathf.Abs(x+a.x*128+64-plan.routeSlope*(z+384)).CompareTo(Mathf.Abs(x+b.x*128+64-plan.routeSlope*(z+384))));
            foreach(var candidate in candidates){
                float radius=TurnRadii[plan.Hash(ownerX+(int)candidate.x,ownerZ+(int)candidate.y,4014)%TurnRadii.Length];
                float localX=x+candidate.x*128,localZ=z+candidate.y*192;
                bool reverse=plan.Hash(ownerX+(int)candidate.x,ownerZ+(int)candidate.y,4015)%2==0;
                float first=localX+(reverse?128:0),last=localX+(reverse?0:128);
                var curved=new Route{noiseBarrier=bend.noiseBarrier,turnRadius=radius};
                RoundCorners(curved,new[]{new Vector2(first,localZ+8),new Vector2(first,localZ+256),new Vector2(last,localZ+256),new Vector2(last,localZ+568)},radius);
                bool valid=true;
                for(int i=0;valid&&i<curved.points.Count-1;i++){
                    float length=Vector2.Distance(curved.points[i],curved.points[i+1]);curved.length+=length;
                    for(float along=0;valid&&along<length;along+=16){
                        valid=Clear(Vector2.Lerp(curved.points[i],curved.points[i+1],along/length),Vector2.Lerp(curved.points[i],curved.points[i+1],Mathf.Min(length,along+16)/length));
                        if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
                    }
                }
                if(valid){cache[key]=curved;yield break;}
            }
            // A blocked long route can use a different wide-road alignment, but never becomes a short isolated stub.
            Route best=null;float bestScore=float.NegativeInfinity;
            for(int lane=0;lane<10;lane++)
            {
                bool horizontal=lane>=4;float roadX=x+lane*128,begin=-1;
                float end=horizontal?508:764;
                Vector2 origin=horizontal?new Vector2(x,z+(lane-4)*128):new Vector2(roadX,z);
                Vector2 axis=horizontal?Vector2.right:Vector2.up;
                for(float distance=8;distance<=end;distance+=4)
                {
                    if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
                    bool dry=distance<end&&Clear(origin+axis*distance,origin+axis*(distance+4));
                    if(dry&&begin<0)begin=distance;
                    if(!dry&&begin>=0)
                    {
                        float length=distance-begin-8;
                        var midpoint=origin+axis*((begin+distance)*.5f);
                        float score=length-Mathf.Abs(midpoint.x-plan.routeSlope*midpoint.y)*.6f;
                        if(length>=320&&score>bestScore)
                        {
                            bestScore=score;
                            best=new Route{noiseBarrier=bend.noiseBarrier,length=length};
                            best.points.Add(origin+axis*(begin+4));best.points.Add(origin+axis*(distance-4));
                        }
                        begin=-1;
                    }
                }
            }
            cache[key]=best;
        }
        bool Clear(Vector2 a,Vector2 b)
        {
            float length=Vector2.Distance(a,b);var d=(b-a)/Mathf.Max(.01f,length);var n=new Vector2(-d.y,d.x);
            for(float along=0;along<=length;along+=2)for(int side=-1;side<=1;side++)
            {
                var p=Vector2.Lerp(a,b,along/Mathf.Max(.01f,length))+n*3.2f*side;
                var settlement=plan.Ecology(p.x,p.y);
                if(plan.WaterDistance(p.x,p.y)<10||plan.Height(p.x,p.y)>0||plan.Farmland(p.x,p.y)||settlement.x+settlement.y<.5f||plan.crossings.Reserved(p.x,p.y,8))return false;
            }
            return true;
        }
        public bool Reserved(float x,float z)
        {
            for(int rz=Mathf.FloorToInt((z-5)/128);rz<=Mathf.FloorToInt((z+5)/128);rz++)for(int rx=Mathf.FloorToInt((x-5)/128);rx<=Mathf.FloorToInt((x+5)/128);rx++)
            {
                var route=Region(rx,rz);if(route==null)continue;
                for(int i=0;i<route.points.Count-1;i++){var a=route.points[i];var b=route.points[i+1];if(WorldCrossings.LinkDistance(new Vector2(x,z),new Vector4(a.x,a.y,b.x,b.y),8)<0)return true;}
            }
            return false;
        }
        // Authored radius catalogue and 15-degree arc steps: deterministic, no
        // unconstrained splines. Same function can later serve a rail radius catalogue.
        public static void RoundCorners(Route route,Vector2[] controls,float radius)
        {
            route.points.Clear();route.points.Add(controls[0]);
            for(int i=1;i<controls.Length-1;i++){
                Vector2 incoming=(controls[i]-controls[i-1]).normalized,outgoing=(controls[i+1]-controls[i]).normalized;
                float angle=Vector2.SignedAngle(incoming,outgoing),abs=Mathf.Abs(angle);
                if(abs<.01f){route.points.Add(controls[i]);continue;}
                float tangent=radius*Mathf.Tan(abs*Mathf.Deg2Rad*.5f);
                if(tangent>Mathf.Min(Vector2.Distance(controls[i-1],controls[i]),Vector2.Distance(controls[i],controls[i+1]))*.49f)throw new System.ArgumentException("Turn radius does not fit corridor controls");
                Vector2 entry=controls[i]-incoming*tangent;
                Vector2 normal=new Vector2(-incoming.y,incoming.x)*Mathf.Sign(angle),center=entry+normal*radius;
                Vector2 radial=entry-center;route.points.Add(entry);int steps=Mathf.RoundToInt(abs/AngleStep);
                if(Mathf.Abs(steps*AngleStep-abs)>.01f)throw new System.ArgumentException("Corridor turn must be a multiple of 15 degrees");
                for(int step=1;step<=steps;step++){float a=angle*step/steps*Mathf.Deg2Rad;route.points.Add(center+new Vector2(radial.x*Mathf.Cos(a)-radial.y*Mathf.Sin(a),radial.x*Mathf.Sin(a)+radial.y*Mathf.Cos(a)));}
            }
            route.points.Add(controls[controls.Length-1]);
        }
    }
}
