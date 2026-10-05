using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Profiling;
namespace OnAir
{
    // Cosmetic traffic uses rendered ground roads and elevated route centre lines. No colliders, rigid bodies,
    // realtime lights, global navigation, or per-car Update/material instances.
    [DefaultExecutionOrder(350)]
    public sealed class CityTraffic:MonoBehaviour
    {
        public GameEntry game;
        public const int Capacity=96;
        public int ActiveCount{get;private set;}
        public int HighwayCount{get;private set;}
        public const float BrakeDistance=8,Acceleration=1.2f,Deceleration=2.8f,TurnSpeedFactor=.5f,TurnDegreesPerSecond=140;
        public int RoadCellCount=>nodes.Count;
        public double LastUpdateMs{get;private set;}
        public static int ColourFamily(int ticket)=>ticket%100<50?0:ticket%100<80?1:2;
        sealed class Car {public Transform root;public Vector2Int from,to,direction;public float progress,speed,cruise,life,distance;public bool active,turning;public WorldHighways.Route route;public int travel=1;public Vector3 mapPosition,segmentStart;}
        readonly Car[] cars=new Car[Capacity];
        readonly Dictionary<Vector2Int,int> nodes=new Dictionary<Vector2Int,int>();
        readonly Dictionary<GameObject,HashSet<Vector2Int>> tiles=new Dictionary<GameObject,HashSet<Vector2Int>>();
        readonly List<GameObject> expired=new List<GameObject>();
        readonly List<Vector2Int> choices=new List<Vector2Int>();
        readonly HashSet<Vector2Int> occupied=new HashSet<Vector2Int>();
        readonly List<Material> materials=new List<Material>();
        readonly List<Mesh> meshes=new List<Mesh>();
        static readonly Vector2Int[] Directions={Vector2Int.up,Vector2Int.right,Vector2Int.down,Vector2Int.left};
        System.Random random;Material front,rear;float refresh;int revision=-1,teleport=-1;
        readonly System.Diagnostics.Stopwatch updateWatch=new System.Diagnostics.Stopwatch();
        Material Make(string name,Color color){var m=new Material(Shader.Find("OnAir/Cloud Receiving Lit")){name=name,enableInstancing=true};m.SetColor("_BaseColor",color);m.SetFloat("_Smoothness",.15f);materials.Add(m);return m;}
        static Color Hex(string value){ColorUtility.TryParseHtmlString("#"+value,out var c);return c;}
        Mesh BoxMesh(Vector3[] centres,Vector3[] sizes)
        {
            var source=GameObject.CreatePrimitive(PrimitiveType.Cube);var cube=source.GetComponent<MeshFilter>().sharedMesh;
            var combine=new CombineInstance[centres.Length];for(int i=0;i<combine.Length;i++)combine[i]=new CombineInstance{mesh=cube,transform=Matrix4x4.TRS(centres[i],Quaternion.identity,sizes[i])};
            var mesh=new Mesh{name="Shared low poly vehicle"};mesh.CombineMeshes(combine);meshes.Add(mesh);Destroy(source);return mesh;
        }
        void Part(Transform root,string name,Mesh mesh,Material material,bool shadow=true){var part=new GameObject(name);part.transform.SetParent(root,false);part.AddComponent<MeshFilter>().sharedMesh=mesh;var r=part.AddComponent<MeshRenderer>();r.sharedMaterial=material;r.shadowCastingMode=shadow?UnityEngine.Rendering.ShadowCastingMode.On:UnityEngine.Rendering.ShadowCastingMode.Off;}
        void Start()
        {
            random=new System.Random(game.journey.seed^30403);
            var palette=new[]{"DADBD2","C2C7C5","D5CDBD","E2DED2","ADB6B5","515E62","5A6059","635C60","344A54","B9463B","CEAA4A","48858D","7279AD"};
            var paints=new Material[palette.Length];for(int i=0;i<paints.Length;i++)paints[i]=Make("Traffic paint "+i,Hex(palette[i]));
            var glass=Make("Traffic blue grey windows",Hex("354E59"));var tyre=Make("Traffic wheels",Hex("303837"));
            front=Make("White headlamps",Hex("F5F4DF"));rear=Make("Red tail lamps",Hex("DC302B"));front.EnableKeyword("_EMISSION");rear.EnableKeyword("_EMISSION");
            var body=BoxMesh(new[]{new Vector3(0,.55f,0)},new[]{new Vector3(1.25f,.65f,2.75f)});
            var cabin=BoxMesh(new[]{new Vector3(0,1.03f,-.1f)},new[]{new Vector3(1.06f,.48f,1.45f)});
            var wheels=BoxMesh(new[]{new Vector3(-.63f,.28f,.85f),new Vector3(.63f,.28f,.85f),new Vector3(-.63f,.28f,-.85f),new Vector3(.63f,.28f,-.85f)},new[]{new Vector3(.22f,.48f,.5f),new Vector3(.22f,.48f,.5f),new Vector3(.22f,.48f,.5f),new Vector3(.22f,.48f,.5f)});
            var head=BoxMesh(new[]{new Vector3(-.43f,.59f,1.39f),new Vector3(.43f,.59f,1.39f)},new[]{new Vector3(.32f,.26f,.08f),new Vector3(.32f,.26f,.08f)});
            var tail=BoxMesh(new[]{new Vector3(-.43f,.59f,-1.39f),new Vector3(.43f,.59f,-1.39f)},new[]{new Vector3(.32f,.26f,.08f),new Vector3(.32f,.26f,.08f)});
            for(int i=0;i<Capacity;i++){var root=new GameObject("TRN-VEH-001 pooled car "+i).transform;root.SetParent(transform,false);int family=ColourFamily(random.Next(100));int paint=family==0?random.Next(5):family==1?5+random.Next(4):9+random.Next(4);Part(root,"Body",body,paints[paint]);Part(root,"Windows",cabin,glass);Part(root,"Wheels",wheels,tyre);Part(root,"Headlights",head,front,false);Part(root,"Tail lights",tail,rear,false);cars[i]=new Car{root=root,cruise=2.5f+(float)random.NextDouble()*2};root.gameObject.SetActive(false);}
        }
        readonly List<WorldHighways.Route> highwayRoutes=new List<WorldHighways.Route>();
        readonly HashSet<WorldHighways.Route> routeSet=new HashSet<WorldHighways.Route>();
        readonly List<Rect> loaded=new List<Rect>();
        int highwayPopulation;
        Vector3 Cell(Vector2Int p)=>new Vector3(p.x*2+1,.11f,p.y*2+1);
        Vector3 Local(Vector3 p){p.z-=game.journey.Current.index*64;return p;}
        bool Visible(Vector3 p){var v=game.sceneCamera.WorldToViewportPoint(Local(p)+Vector3.up*.65f);return v.z>0&&v.x>-.08f&&v.x<1.08f&&v.y>-.08f&&v.y<1.08f;}
        bool Loaded(Vector2 p){foreach(var r in loaded)if(r.Contains(p))return true;return false;}
        void Discover()
        {
            expired.Clear();foreach(var pair in tiles)if(!pair.Key||!pair.Key.activeInHierarchy)expired.Add(pair.Key);
            foreach(var key in expired){foreach(var node in tiles[key]){if(nodes[node]==1)nodes.Remove(node);else nodes[node]--;}tiles.Remove(key);}
            routeSet.Clear();loaded.Clear();
            foreach(var go in game.world.Chunks)
            {
                var layout=go.GetComponent<ContinuousWorldLayout>();if(!layout)continue;loaded.Add(layout.WorldBounds);foreach(var route in layout.HighwayRoutes)routeSet.Add(route);
                if(tiles.ContainsKey(go))continue;var set=new HashSet<Vector2Int>();
                foreach(var r in layout.RoadFootprints)for(int z=Mathf.CeilToInt(r.yMin/2);z*2+2<=r.yMax+.001f;z++)for(int x=Mathf.CeilToInt(r.xMin/2);x*2+2<=r.xMax+.001f;x++){var cell=new Vector2Int(x,z);if(layout.TrafficCell(cell))set.Add(cell);}
                tiles.Add(go,set);foreach(var node in set){nodes.TryGetValue(node,out int count);nodes[node]=count+1;}
            }
            choices.Clear();foreach(var node in nodes.Keys)choices.Add(node);
            highwayRoutes.Clear();highwayRoutes.AddRange(routeSet);
            // A newly streamed elevated corridor must receive cars even if all pool
            // slots were previously occupied by ground traffic. Reassign only offscreen cars.
            int moved=0;
            if(highwayRoutes.Count>0&&highwayPopulation<32)foreach(var car in cars)
            {
                if(!car.active||car.route!=null||Visible(car.mapPosition))continue;
                var oldPosition=car.mapPosition;
                car.active=false;
                if(SpawnHighway(car)){moved++;if(moved>=4)break;}
                else {car.active=true;car.mapPosition=oldPosition;}
            }
        }
        bool Available(Vector2Int cell)=>nodes.ContainsKey(cell)&&!occupied.Contains(cell);
        bool LeftLane(Vector2Int cell,Vector2Int direction){var left=new Vector2Int(-direction.y,direction.x);return !nodes.ContainsKey(cell+left)||nodes.ContainsKey(cell-left);}
        bool Junction(Vector2Int cell,Vector2Int dir){var left=new Vector2Int(-dir.y,dir.x);return nodes.ContainsKey(cell+left*2)||nodes.ContainsKey(cell-left*2);}
        bool PickNext(Car car)
        {
            var left=new Vector2Int(-car.direction.y,car.direction.x);bool turn=random.Next(100)<12;
            for(int i=0;i<3;i++)
            {
                var dir=turn?(i==0?left:i==1?-left:car.direction):(i==0?car.direction:i==1?left:-left);
                if(!Available(car.to+dir)||!nodes.ContainsKey(car.to+dir*2)||!LeftLane(car.to+dir*2,dir))continue;
                car.turning=dir!=car.direction;car.segmentStart=car.mapPosition;car.from=car.to;car.to+=dir;car.direction=dir;car.progress=0;return true;
            }
            return false;
        }
        void Stop(Car car){if(car.active&&car.route!=null)highwayPopulation--;car.active=false;car.route=null;car.root.gameObject.SetActive(false);}
        public static Vector3 HighwayPosition(WorldHighways.Route route,float distance,int travel,out Vector3 heading)
        {
            var p=route.Position(distance,out var tangent);var left=new Vector2(-tangent.y,tangent.x)*travel;
            p+=left*(route.upperTier?.85f:1.25f);
            float step=.5f;float grade=(route.Height(Mathf.Min(route.length,distance+step))-route.Height(Mathf.Max(0,distance-step)))/(distance<step||distance>route.length-step?step:step*2);
            heading=new Vector3(tangent.x*travel,grade*travel,tangent.y*travel).normalized;
            return new Vector3(p.x,route.Height(distance)+.06f,p.y);
        }
        bool FreeSpawn(Vector3 p){foreach(var other in cars)if(other.active&&Mathf.Abs(other.mapPosition.y-p.y)<2&&(other.mapPosition-p).sqrMagnitude<64)return false;return true;}
        bool SpawnHighway(Car car)
        {
            if(highwayPopulation>=32||highwayRoutes.Count==0)return false;
            for(int i=0;i<16;i++)
            {
                var route=highwayRoutes[random.Next(highwayRoutes.Count)];float d=8+(float)random.NextDouble()*(route.length-16);int travel=random.Next(2)==0?1:-1;
                var p=HighwayPosition(route,d,travel,out var heading);if(!Loaded(new Vector2(p.x,p.z))||(Time.time>3&&Visible(p))||!FreeSpawn(p))continue;
                car.route=route;car.distance=d;car.travel=travel;car.active=true;car.speed=0;car.mapPosition=p;car.root.position=Local(p);car.root.rotation=Quaternion.LookRotation(heading);car.root.gameObject.SetActive(true);highwayPopulation++;return true;
            }
            return false;
        }
        void Spawn(Car car)
        {
            if(SpawnHighway(car)||choices.Count==0)return;
            for(int attempt=0;attempt<16;attempt++)
            {
                var cell=choices[random.Next(choices.Count)];var p=Cell(cell);if((Time.time>3&&Visible(p))||!FreeSpawn(p))continue;
                var dir=Directions[random.Next(4)];if(!Available(cell)||!Available(cell+dir)||!Available(cell+dir*2)||!nodes.ContainsKey(cell+dir*3)||!LeftLane(cell,dir))continue;
                car.route=null;car.from=cell;car.to=cell+dir;car.direction=dir;car.progress=0;car.life=0;car.active=true;car.speed=0;car.turning=false;car.mapPosition=p;car.segmentStart=p;
                car.root.position=Local(p);car.root.rotation=Quaternion.LookRotation(new Vector3(dir.x,0,dir.y));car.root.gameObject.SetActive(true);occupied.Add(cell);occupied.Add(cell+dir);return;
            }
        }
        float Clearance(Car car,Vector3 direction)
        {
            float gap=100;
            foreach(var other in cars)
            {
                if(!other.active||other==car||Mathf.Abs(car.mapPosition.y-other.mapPosition.y)>1.6f)continue;
                if(car.route!=null&&car.route==other.route&&car.travel==other.travel){float along=(other.distance-car.distance)*car.travel;if(along>0)gap=Mathf.Min(gap,along-3.75f);continue;}
                var delta=other.mapPosition-car.mapPosition;float ahead=delta.x*direction.x+delta.z*direction.z;
                float across=Mathf.Abs(delta.x*direction.z-delta.z*direction.x);
                if(ahead>0&&ahead<16&&across<1.3f)gap=Mathf.Min(gap,ahead-3.75f);
            }
            return gap;
        }
        public static float FollowingSpeed(float cruise,float gap)=>cruise*Mathf.SmoothStep(0,1,Mathf.Clamp01(gap/BrakeDistance));
        public static float IntegrateSpeed(float speed,float target,float seconds)=>Mathf.MoveTowards(speed,target,seconds*(target<speed?Deceleration:Acceleration));
        bool JoinGround(Car car,Vector3 heading)
        {
            var cell=new Vector2Int(Mathf.FloorToInt(car.mapPosition.x/2),Mathf.FloorToInt(car.mapPosition.z/2));
            var dir=Mathf.Abs(heading.x)>Mathf.Abs(heading.z)?new Vector2Int(heading.x>0?1:-1,0):new Vector2Int(0,heading.z>0?1:-1);
            if(!Available(cell)||!Available(cell+dir))return false;
            car.route=null;highwayPopulation--;car.from=cell;car.to=cell+dir;car.direction=dir;car.progress=0;car.segmentStart=car.mapPosition;car.turning=false;return true;
        }
        bool ContinueLayer(Car car)
        {
            var connection=car.travel>0?car.route.next:car.route.previous;
            if(connection!=null){car.route=connection;car.distance=car.travel>0?0:connection.length;return true;}
            if(car.route.ramp&&car.travel<0&&car.route.parentRoute!=null){car.distance=car.route.mergeDistance;car.route=car.route.parentRoute;return true;}
            var end=car.route.Position(car.travel>0?car.route.length:0,out _);
            foreach(var next in highwayRoutes){if(next==car.route||!next.independentLayer||next.ramp)continue;
                var join=next.Position(car.travel>0?0:next.length,out _);
                if((join-end).sqrMagnitude>.01f)continue;
                car.route=next;car.distance=car.travel>0?0:next.length;return true;
            }
            return false;
        }
        bool EnterHighway(Car car)
        {
            foreach(var route in highwayRoutes){
                if(!route.independentLayer||!route.ramp)continue;
                int travel=route.ascending?1:-1;float distance=travel>0?0:route.length;
                var position=HighwayPosition(route,distance,travel,out var heading);
                if(Vector3.Dot(heading,new Vector3(car.direction.x,0,car.direction.y))<.98f||(position-car.mapPosition).sqrMagnitude>2.5f)continue;
                bool occupiedEntry=false;foreach(var other in cars)if(other!=car&&other.active&&(other.mapPosition-position).sqrMagnitude<25){occupiedEntry=true;break;}
                if(occupiedEntry)return false;
                occupied.Remove(car.from);occupied.Remove(car.to);car.route=route;car.travel=travel;car.distance=distance;car.mapPosition=position;highwayPopulation++;return true;
            }
            return false;
        }
        void Advance(Car car,float dt)
        {
            Vector3 heading;float target=car.cruise,remaining;
            if(car.route!=null)
            {
                HighwayPosition(car.route,car.distance,car.travel,out heading);
                car.route.Position(Mathf.Clamp(car.distance+car.travel*10,0,car.route.length),out var ahead);
                car.route.Position(car.distance,out var current);
                if(Vector2.Angle(ahead,current)>2)target*=TurnSpeedFactor;
                remaining=car.travel>0?car.route.length-car.distance:car.distance;
                bool groundEnd=car.route.ramp&&car.route.Height(car.travel>0?car.route.length:0)<1;
                if(remaining<.05f){if(groundEnd){if(!JoinGround(car,heading))car.speed=IntegrateSpeed(car.speed,0,dt);}else if(!ContinueLayer(car))car.speed=IntegrateSpeed(car.speed,0,dt);return;}
                if(!car.route.independentLayer||groundEnd)target=Mathf.Min(target,Mathf.Sqrt(2*Deceleration*remaining));
            }
            else
            {
                heading=new Vector3(car.direction.x,0,car.direction.y);
                if(car.turning||Junction(car.to,car.direction))target*=TurnSpeedFactor;
                remaining=Vector3.Distance(car.mapPosition,Cell(car.to));
                if(car.progress>=.9999f)
                {
                    if(EnterHighway(car))return;
                    occupied.Remove(car.from);
                    if(!PickNext(car)){car.speed=IntegrateSpeed(car.speed,0,dt);car.life+=dt;if(car.life>15&&!Visible(car.mapPosition))Stop(car);return;}
                    occupied.Add(car.from);occupied.Add(car.to);heading=new Vector3(car.direction.x,0,car.direction.y);remaining=Vector3.Distance(car.mapPosition,Cell(car.to));car.life=0;
                }
                // Look beyond the current 2m cell; begin braking before a closed end.
                float open=remaining;
                for(int n=1;n<=5;n++){if(!nodes.ContainsKey(car.to+car.direction*n)){if(!Junction(car.to,car.direction))target=Mathf.Min(target,Mathf.Sqrt(2*Deceleration*open));break;}open+=2;}
            }
            float gap=Clearance(car,heading);target=Mathf.Min(target,FollowingSpeed(car.cruise,gap));car.speed=IntegrateSpeed(car.speed,target,dt);
            float movement=Mathf.Min(car.speed*dt,Mathf.Max(0,gap));
            if(car.route!=null)
            {
                car.distance=Mathf.Clamp(car.distance+movement*car.travel,0,car.route.length);
                car.mapPosition=HighwayPosition(car.route,car.distance,car.travel,out heading);
                if(!Loaded(new Vector2(car.mapPosition.x,car.mapPosition.z))&&!Visible(car.mapPosition)){Stop(car);return;}
            }
            else
            {
                float length=Mathf.Max(.01f,Vector3.Distance(car.segmentStart,Cell(car.to)));
                car.progress=Mathf.Min(1,car.progress+movement/length);car.mapPosition=Vector3.Lerp(car.segmentStart,Cell(car.to),car.progress);
            }
            car.root.position=Local(car.mapPosition);car.root.rotation=Quaternion.RotateTowards(car.root.rotation,Quaternion.LookRotation(heading),dt*TurnDegreesPerSecond);
        }
        void LateUpdate()
        {
            if(random==null||!game.journey.continuousWorld)return;
            updateWatch.Restart();Profiler.BeginSample("OnAir.CityTraffic");
            if(revision!=game.journey.Revision||teleport!=game.journey.TeleportRevision){foreach(var car in cars)Stop(car);tiles.Clear();nodes.Clear();revision=game.journey.Revision;teleport=game.journey.TeleportRevision;refresh=0;random=new System.Random(game.journey.seed^30403);}
            if(Time.unscaledTime>=refresh){Discover();refresh=Time.unscaledTime+.5f;}
            var session=game.session;float phase=session.automaticDaylight?session.solarPhase:session.period==DayPeriod.Day?.25f:session.period==DayPeriod.Dawn?.055f:session.period==DayPeriod.Dusk?.455f:session.period==DayPeriod.BlueHour?.535f:.75f;
            float night=1-Mathf.SmoothStep(0,1,Mathf.Sin(phase*Mathf.PI*2)*3);front.SetColor("_EmissionColor",new Color(1.3f,1.3f,1.22f)*night);rear.SetColor("_EmissionColor",new Color(1.45f,.025f,.015f)*night);
            occupied.Clear();highwayPopulation=0;foreach(var car in cars)if(car.active){if(car.route==null){occupied.Add(car.from);occupied.Add(car.to);}else highwayPopulation++;}
            ActiveCount=0;HighwayCount=0;float dt=session.SimulationPaused?0:Mathf.Min(Time.deltaTime,.1f);
            foreach(var car in cars)
            {
                bool lostRoad=car.active&&(car.route==null?(!nodes.ContainsKey(car.from)||!nodes.ContainsKey(car.to)):!routeSet.Contains(car.route));
                if(lostRoad&&!Visible(car.mapPosition))Stop(car);
                if(!car.active&&!session.SimulationPaused)Spawn(car);if(!car.active)continue;
                if(dt>0&&!lostRoad)Advance(car,dt);
                // Also update stopped cars and failed junction/ramp transitions:
                // the floating map origin changes even when a car does not move.
                if(car.active)car.root.position=Local(car.mapPosition);
                if(car.active){ActiveCount++;if(car.route!=null)HighwayCount++;}
            }
            Profiler.EndSample();LastUpdateMs=updateWatch.Elapsed.TotalMilliseconds;
        }
        void OnDestroy(){foreach(var material in materials)if(material)Destroy(material);foreach(var mesh in meshes)if(mesh)Destroy(mesh);}
    }
}
