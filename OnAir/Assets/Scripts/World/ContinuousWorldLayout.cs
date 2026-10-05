using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    public sealed class ContinuousWorldLayout:MonoBehaviour
    {
        public readonly List<Rect> RotatedFootprints=new List<Rect>();
        public readonly List<Rect> Footprints=new List<Rect>();
        public readonly List<Rect> Parcels=new List<Rect>();
        public readonly List<WorldPopulationCache.Region> PopulationRegions=new List<WorldPopulationCache.Region>();
        public readonly List<Rect> RoadFootprints=new List<Rect>();
        // Only fully dry, level asphalt cells belong to ground traffic. A painted
        // rectangle can extend under a water cutout and is not itself proof of a road.
        public bool TrafficCell(Vector2Int cell){float x=cell.x*2,z=cell.y*2;return CanRoad(x,z,2,2);}
        public readonly List<WorldHighways.Route> HighwayRoutes=new List<WorldHighways.Route>();
        public readonly List<Vector4> ShoreEdges=new List<Vector4>();
        public int TimberBridgeTiles{get;private set;}
        public int BuildingCount {get;private set;}
        public string BuildStage{get;private set;}="terrain";
        public readonly Dictionary<string,double> InitialStageMs=new Dictionary<string,double>();
        public Rect WorldBounds=>new Rect(minX,start,maxX-minX,end-start);
        readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();
        readonly List<Texture2D> terrainTextures=new List<Texture2D>();
        readonly Dictionary<Vector2,Vector3> terrainNormals=new Dictionary<Vector2,Vector3>();
        WorldPopulationCache.Region recording;
        readonly List<Rect> streetReservations=new List<Rect>();
        // Direction from a building towards the fixed game view, supplied by the streamer.
        public Vector3 CommercialViewDirection=new Vector3(-1,0,-1);
        public const float CommercialFacingProbability=.7f;
        WorldPopulationCache ownedPopulationCache;
        ContinuousWorldPlan plan;WorldKit kit;BuildingData data;int index;float start,end,minX,maxX;
        readonly List<Vector3>[] vertices=Enumerable.Range(0,19).Select(_=>new List<Vector3>()).ToArray();
        readonly List<int>[] triangles=Enumerable.Range(0,19).Select(_=>new List<int>()).ToArray();
        public void Build(int worldSeed,int slice,WorldKit worldKit,BuildingData buildings,ContinuousWorldPlan sharedPlan=null,float routeSlope=0,float halfWidth=256,Rect? tile=null,WorldPopulationCache populationCache=null)
        {
            var steps=BuildIncrementally(worldSeed,slice,worldKit,buildings,sharedPlan,routeSlope,halfWidth,tile,populationCache);
            var timer=new System.Diagnostics.Stopwatch();bool more;
            do{timer.Restart();more=steps.MoveNext();InitialStageMs.TryGetValue(BuildStage,out double elapsed);InitialStageMs[BuildStage]=elapsed+timer.Elapsed.TotalMilliseconds;}while(more);
        }
        public System.Collections.IEnumerator BuildIncrementally(int worldSeed,int slice,WorldKit worldKit,BuildingData buildings,ContinuousWorldPlan sharedPlan=null,float routeSlope=0,float halfWidth=256,Rect? tile=null,WorldPopulationCache populationCache=null)
        {
            var budget=System.Diagnostics.Stopwatch.StartNew();
            plan=sharedPlan??new ContinuousWorldPlan(worldSeed,routeSlope);kit=worldKit;data=buildings;index=slice;start=slice*64-32;end=start+64;float centre=Mathf.Round(routeSlope*slice)*64;minX=centre-halfWidth;maxX=centre+halfWidth;
            if(tile.HasValue){minX=tile.Value.xMin;maxX=tile.Value.xMax;}
            // Finish shared corridor planning in small steps before placement queries it.
            // Cover complete population regions and neighbouring reservations, not only this tile.
            BuildStage="transport planning";
            for(int rz=Mathf.FloorToInt(start/128)-1;rz<=Mathf.FloorToInt((end-.01f)/128)+1;rz++)
            for(int rx=Mathf.FloorToInt(minX/128)-1;rx<=Mathf.FloorToInt((maxX-.01f)/128)+1;rx++)
            {
                var work=plan.highways.PrepareRegion(rx,rz);
                while(work.MoveNext()){yield return null;budget.Restart();}
            }
            BuildStage="terrain";
            var regions=new List<ContinuousWorldPlan.Region>();
            for(int rz=Mathf.FloorToInt(start/128);rz<=Mathf.FloorToInt((end-.01f)/128);rz++)for(int rx=Mathf.FloorToInt(minX/128);rx<=Mathf.FloorToInt((maxX-.01f)/128);rx++)regions.Add(plan.BuildRegion(rx,rz));
            var roads=regions.SelectMany(r=>r.roads).ToArray();
            foreach(var region in regions){plan.greenSpaces.Prepare(region,kit);if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}}
            // A shared 2-unit sample grid makes both water and roads identical across slice boundaries.
            for(float z=start;z<end;z+=2)for(float x=minX;x<maxX;x+=2)
            {
                var land=plan.Sample(x+1,z+1);bool road=roads.Any(r=>r.Contains(new Vector2(x+1,z+1)));
                bool farm=plan.Farmland(x+1,z+1);bool natural=plan.Ecology(x+1,z+1).z>.5f||plan.ZoneAt(x+1,z+1)==ContinuousWorldPlan.EcologyZone.Ocean;
                bool bridge=false;road=road&&CanRoad(x,z,2,2);
                int group=land==ContinuousWorldPlan.Land.Water?0:land==ContinuousWorldPlan.Land.Bank?(natural?9:4):land==ContinuousWorldPlan.Land.Forest?2:land==ContinuousWorldPlan.Land.Park?3:4;
                if(road&&land!=ContinuousWorldPlan.Land.Water&&(land!=ContinuousWorldPlan.Land.Forest||plan.Ecology(x+1,z+1).x>.55f))group=5;
                if(farm)group=((int)x%8==0||(int)z%24==2||(int)z%24==20)?13:14;
                if(bridge)group=natural?10:5;
                if(bridge&&natural)TimberBridgeTiles++;
                bool hillside=plan.EcologyBlendAt(x+1,z+1).Weight(ContinuousWorldPlan.EcologyZone.Hills)>.5f;
                float terrainHeight=plan.Height(x+1,z+1);
                if(group==2){TerrainCell(hillside?9:2,x,z,0);}
                // Adjacent ground tiles share one elevation: raised road tops without sidewalls leave sky-visible cracks.
                else GroundCell(group==0?1:group,x,z,0);
                WaterCell(x,z);
                if(bridge&&natural){Quad(10,x,z+.08f,2,.12f,.85f);Quad(10,x,z+1.8f,2,.12f,.85f);Face(10,new Vector3(x,.25f,z+.1f),new Vector3(x,.85f,z+.1f),new Vector3(x+.12f,.85f,z+.1f),new Vector3(x+.12f,.25f,z+.1f));}
                if(farm&&group==14)Quad(9,x+.5f,z+.2f,.12f,1.6f,.06f);
                if(bridge&&!natural)
                {
                    float bridgeZ=Mathf.Round((z+1)/128)*128;
                    if(Mathf.Abs(z-bridgeZ)<.1f){Quad(6,x,z+.18f,2,.12f,.28f);if(((int)x/2)%3!=0)Quad(6,x,z+1.94f,1.2f,.08f,.28f);}
                    if(Mathf.Abs(z-bridgeZ-2)<.1f)Quad(6,x,z+1.70f,2,.12f,.28f);
                }
                if((land==ContinuousWorldPlan.Land.Forest||land==ContinuousWorldPlan.Land.Park)&&!farm&&!plan.greenSpaces.Reserved(x+1,z+1)&&!plan.DiagonalReserved(x+1,z+1)&&!plan.highways.Reserved(x+1,z+1)&&!plan.crossings.Reserved(x+1,z+1,2)&&terrainHeight<39&&group!=5&&group!=10&&plan.Hash((int)x,(int)z,58)%(hillside?23:land==ContinuousWorldPlan.Land.Forest?plan.TreeSpacing(x,z):17)==0)
                {
                    // Tree height varies by authored species, not by stretching baked pixels.
                    var tree=Instantiate(plan.Hash(Mathf.FloorToInt(x/48),Mathf.FloorToInt(z/48),413)%3==0?kit.treeTall:kit.tree,transform);tree.transform.localPosition=new Vector3(x+1,plan.Height(x+1,z+1)+.02f,z+1-index*64);tree.GetComponent<PixelObjectView>()?.Configure();
                }
                if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
            }

            BuildStage="population";if(populationCache==null)populationCache=ownedPopulationCache=new WorldPopulationCache();
            foreach(var region in regions)
            {
                foreach(var parcel in region.parcels)if(parcel.bounds.yMax>=start&&parcel.bounds.yMin<=end&&parcel.bounds.xMax>=minX&&parcel.bounds.xMin<=maxX)Parcels.Add(parcel.bounds);
                if(!populationCache.TryGet(region,out var population))
                {
                    var work=EnsurePopulation(region,populationCache);
                    while(work.MoveNext()){yield return null;budget.Restart();}
                    populationCache.TryGet(region,out population);
                }
                PopulationRegions.Add(population);
                foreach(var item in population.buildings)
                {
                    var rect=item.bounds;
                    if(rect.center.y<start||rect.center.y>=end||rect.center.x<minX||rect.center.x>=maxX)continue;
                    var obj=Instantiate(item.prefab,transform);obj.transform.localPosition=new Vector3(rect.center.x,.02f,rect.center.y-index*64);obj.transform.localRotation=Quaternion.Euler(0,item.rotation,0);
                    obj.GetComponent<PixelObjectView>()?.Configure();
                    if(item.tint)populationCache.palette.Apply(obj.GetComponent<BuildingView>(),item.color);
                    Footprints.Add(rect);BuildingCount++;
                    if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
                }
                foreach(var road in population.roads)
                {
                    float left=Mathf.Max(minX,road.xMin),right=Mathf.Min(maxX,road.xMax),bottom=Mathf.Max(start,road.yMin),top=Mathf.Min(end,road.yMax);
                    if(right>left&&top>bottom)Quad(5,left,bottom,right-left,top-bottom,.09f);
                }
            }
            foreach(var line in regions.SelectMany(r=>r.diagonalRoads))BuildRoadLink(line,4);
            BuildStage="park paths";
            foreach(var region in regions)
            {
                var parks=plan.greenSpaces.Parks(region);if(parks.Count==0)continue;
                var host=new GameObject("Street-enclosed green parks");host.transform.SetParent(transform,false);host.AddComponent<CityParkGeometry>().Build(parks,WorldBounds,index*64,kit.cityPaving);
                if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
            }
            BuildStage="bridges";var crossingSteps=BuildCrossings();while(crossingSteps.MoveNext()){yield return null;budget.Restart();}
            var builtRoutes=new HashSet<WorldHighways.Route>();
            BuildStage="highways";
            for(int rz=Mathf.FloorToInt(start/128)-1;rz<=Mathf.FloorToInt(end/128);rz++)for(int rx=Mathf.FloorToInt(minX/128)-1;rx<=Mathf.FloorToInt(maxX/128);rx++){var route=plan.highways.Region(rx,rz);if(route!=null&&builtRoutes.Add(route)){var steps=BuildHighway(route);while(steps.MoveNext()){yield return null;budget.Restart();}}}

            BuildStage="upper highways";
            // Neighbour owners are needed only when their actual deck can reach this
            // tile; the upper planner does not participate in terrain reservations.
            for(int rz=Mathf.FloorToInt((start-132)/128);rz<=Mathf.FloorToInt((end+132)/128);rz++)
            for(int rx=Mathf.FloorToInt((minX-132)/128);rx<=Mathf.FloorToInt((maxX+132)/128);rx++)
            {
                if(!WorldUpperHighways.MayReach(rx,rz,WorldBounds))continue;
                var work=plan.upperHighways.PrepareRegion(rx,rz,populationCache,r=>EnsurePopulation(r,populationCache));
                while(work.MoveNext()){yield return null;budget.Restart();}
                var route=plan.upperHighways.Region(rx,rz);
                if(route!=null&&builtRoutes.Add(route)){var steps=BuildHighway(route);while(steps.MoveNext()){yield return null;budget.Restart();}}
            }

            BuildStage="mesh upload";
            Material[] surface={Mat("River",new Color(.12f,.23f,.29f)),Mat("Bank",new Color(.42f,.47f,.39f)),Mat("Woodland",new Color(.23f,.32f,.23f)),Mat("Park",new Color(.36f,.43f,.30f)),kit.cityPaving,kit.cityAsphalt,kit.cityPaint,Mat("Riverside paving",new Color(.57f,.55f,.50f)),Mat("Concrete embankment",new Color(.49f,.51f,.50f)),Mat("Waterside grass",new Color(.36f,.43f,.30f)),Mat("Timber bridge",new Color(.34f,.26f,.18f)),Mat("Rock terraces",new Color(.40f,.40f,.36f)),Mat("Sand bank",new Color(.67f,.63f,.49f)),Mat("Field bund",new Color(.39f,.34f,.22f)),Mat("Flooded paddy",new Color(.30f,.39f,.29f)),Mat("Shallow sea",new Color(.18f,.34f,.37f)),Mat("Painted bridge steel",new Color(.21f,.33f,.34f)),Mat("Bridge railing",new Color(.66f,.68f,.65f)),Mat("Expressway green",new Color(.045f,.23f,.16f))};
            foreach(int group in new[]{2,9})
            {
                var texture=new Texture2D(128,1,TextureFormat.RGB24,false){wrapMode=TextureWrapMode.Clamp,filterMode=FilterMode.Bilinear,name="Slope color gradient"};
                var baseColor=surface[group].GetColor("_BaseColor");var colors=new Color[128];
                for(int k=0;k<128;k++)colors[k]=Color.Lerp(baseColor,new Color(.40f,.40f,.36f),Mathf.SmoothStep(0,1,(k/127f*80-27)/24));
                texture.SetPixels(colors);texture.Apply(false,true);terrainTextures.Add(texture);surface[group].SetColor("_BaseColor",Color.white);surface[group].SetTexture("_BaseMap",texture);
            }
            for(int i=0;i<19;i++)if(vertices[i].Count>0)
            {
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Continuous surface "+i};meshes.Add(mesh);mesh.SetVertices(vertices[i]);mesh.SetTriangles(triangles[i],0);
                if(i==0||i==15)mesh.SetNormals(Enumerable.Repeat(Vector3.up,vertices[i].Count).ToArray());
                else if(i==1||i==2||i==3||i==4||i==9)
                {
                    var normals=new Vector3[vertices[i].Count];
                    for(int v=0;v<normals.Length;v++){normals[v]=Vector3.up;if(v%256==0&&budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}}
                    mesh.SetNormals(normals);
                }
                else mesh.RecalculateNormals();
                if(i==2||i==9)mesh.SetUVs(0,vertices[i].Select(v=>new Vector2(v.y/80,.5f)).ToArray());
                mesh.RecalculateBounds();var obj=new GameObject(mesh.name);obj.transform.SetParent(transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=surface[i];if(i==0||i==1||i==3||i==4||i==5||i==6||i==7||i==12||i==13||i==14||i==15)renderer.shadowCastingMode=ShadowCastingMode.Off;
                // The mesh owns its data now; do not retain a second set of construction buffers.
                vertices[i].Clear();vertices[i].Capacity=0;triangles[i].Clear();triangles[i].Capacity=0;
                if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
            }
        }
        public void ConfigurePlanner(ContinuousWorldPlan owner,WorldKit worldKit,BuildingData buildings)
        {plan=owner;kit=worldKit;data=buildings;}
        public System.Collections.IEnumerator PrepareHighwayPopulation(ContinuousWorldPlan.Region region,WorldPopulationCache cache)=>EnsurePopulation(region,cache);
        public int LayerRevision{get;private set;}=-1;
        GameObject layerObject;
        public System.Collections.IEnumerator SyncHighways(bool staged=false)
        {
            if(!WorldLayerHighways.Enabled||LayerRevision==plan.layerHighways.Revision)yield break;
            if(layerObject){layerObject.SetActive(false);Destroy(layerObject);}
            HighwayRoutes.RemoveAll(r=>r.independentLayer);
            if(!plan.layerHighways.Active){LayerRevision=plan.layerHighways.Revision;yield break;}
            layerObject=new GameObject("Elevated road network");layerObject.SetActive(false);layerObject.transform.SetParent(transform,false);
            var geometry=layerObject.AddComponent<ContinuousWorldLayout>();
            geometry.plan=plan;geometry.kit=kit;geometry.index=index;geometry.start=start;geometry.end=end;geometry.minX=minX;geometry.maxX=maxX;
            foreach(var route in plan.layerHighways.Routes){
                if(!WorldLayerHighways.Intersects(route,WorldBounds))continue;
                var work=geometry.BuildHighway(route);while(work.MoveNext())yield return null;
            }
            foreach(int group in new[]{5,6,8,16,17,18}){
                if(geometry.vertices[group].Count==0)continue;
                Material material=group==5?kit.cityAsphalt:group==6?kit.cityPaint:
                    geometry.Mat("Elevated structure "+group,group==8?new Color(.49f,.51f,.50f):group==16?new Color(.21f,.33f,.34f):group==17?new Color(.66f,.68f,.65f):new Color(.045f,.23f,.16f));
                var mesh=new Mesh{indexFormat=IndexFormat.UInt32,name="Elevated surface "+group};geometry.meshes.Add(mesh);
                mesh.SetVertices(geometry.vertices[group]);mesh.SetTriangles(geometry.triangles[group],0);mesh.RecalculateNormals();mesh.RecalculateBounds();
                var obj=new GameObject(mesh.name);obj.transform.SetParent(layerObject.transform,false);obj.AddComponent<MeshFilter>().sharedMesh=mesh;obj.AddComponent<MeshRenderer>().sharedMaterial=material;
                geometry.vertices[group].Clear();geometry.triangles[group].Clear();yield return null;
            }
            LayerRevision=plan.layerHighways.Revision;
            if(!staged&&plan.layerHighways.Published)RevealHighways();
        }
        public void RevealHighways(){
            if(!layerObject||layerObject.activeSelf)return;
            HighwayRoutes.AddRange(layerObject.GetComponent<ContinuousWorldLayout>().HighwayRoutes);layerObject.SetActive(true);
        }
        Vector3 TerrainNormal(Vector3 vertex)
        {
            var p=new Vector2(vertex.x,vertex.z+index*64);if(terrainNormals.TryGetValue(p,out var normal))return normal;
            normal=new Vector3(plan.Height(p.x-1,p.y)-plan.Height(p.x+1,p.y),2,plan.Height(p.x,p.y-1)-plan.Height(p.x,p.y+1)).normalized;terrainNormals[p]=normal;return normal;
        }
        Material Mat(string name,Color color){var m=new Material(kit.cityPaving){name=name};m.SetColor("_BaseColor",color);materials.Add(m);return m;}
        void Face(int group,Vector3 a,Vector3 b,Vector3 c,Vector3 d)
        {
            int n=vertices[group].Count;var shift=Vector3.back*(index*64);var v=vertices[group];var t=triangles[group];
            v.Add(a+shift);v.Add(b+shift);v.Add(c+shift);v.Add(d+shift);
            t.Add(n);t.Add(n+1);t.Add(n+2);t.Add(n);t.Add(n+2);t.Add(n+3);
        }
        void ShoreStrip(int group,float a,float b,float z,float inner,float outer,float y,int side)
        {
            var p=new Vector3(a+side*inner,y,z);var q=new Vector3(b+side*inner,y,z+2);var r=new Vector3(b+side*outer,y,z+2);var s=new Vector3(a+side*outer,y,z);
            if(side>0)Face(group,p,q,r,s);else Face(group,s,r,q,p);
        }
        bool CanRoad(float x,float z,float w,float d)
        {
            foreach(var p in new[]{new Vector2(x,z),new Vector2(x+w,z),new Vector2(x,z+d),new Vector2(x+w,z+d),new Vector2(x+w/2,z+d/2)})
                if(plan.crossings.LotAt(p.x,p.y)||plan.ZoneAt(p.x,p.y)==ContinuousWorldPlan.EcologyZone.Ocean||plan.Farmland(p.x,p.y)||plan.WaterDistance(p.x,p.y)<7||plan.Height(p.x,p.y)>0||(plan.Sample(p.x,p.y)==ContinuousWorldPlan.Land.Forest&&plan.Ecology(p.x,p.y).x<=.55f))return false;
            return true;
        }
        bool BridgeAt(float x,float z)=>plan.crossings.Reserved(x,z,0);
        void RockWall(Vector3 a,Vector3 b,Vector3 c,Vector3 d)=>Face(11,d,c,b,a);
        void Cliff(float x,float z,float h)
        {
            float a=plan.Height(x-1,z+1),b=plan.Height(x+3,z+1),c=plan.Height(x+1,z-1),d=plan.Height(x+1,z+3);
            if(h>a)RockWall(new Vector3(x,a,z),new Vector3(x,h,z),new Vector3(x,h,z+2),new Vector3(x,a,z+2));
            if(h>b)RockWall(new Vector3(x+2,b,z+2),new Vector3(x+2,h,z+2),new Vector3(x+2,h,z),new Vector3(x+2,b,z));
            if(h>c)RockWall(new Vector3(x+2,c,z),new Vector3(x+2,h,z),new Vector3(x,h,z),new Vector3(x,c,z));
            if(h>d)RockWall(new Vector3(x,d,z+2),new Vector3(x,h,z+2),new Vector3(x+2,h,z+2),new Vector3(x+2,d,z+2));
        }
        void Quad(int group,float x,float z,float w,float d,float y){if(group==5&&y<.1f)RoadFootprints.Add(new Rect(x,z,w,d));int n=vertices[group].Count;z-=index*64;vertices[group].AddRange(new[]{new Vector3(x,y,z),new Vector3(x,y,z+d),new Vector3(x+w,y,z+d),new Vector3(x+w,y,z)});triangles[group].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        void GroundCell(int group,float x,float z,float y)
        {
            float a=plan.WaterDistance(x,z),b=plan.WaterDistance(x,z+2),c=plan.WaterDistance(x+2,z+2),d=plan.WaterDistance(x+2,z);
            if(Mathf.Min(a,b,c,d)>=0){TerrainCell(group,x,z,y);return;}
            if(Mathf.Max(a,b,c,d)<=0)return;
            Surface(Clip(new List<Vector2>{new Vector2(x,z),new Vector2(x,z+2),new Vector2(x+2,z+2)},p=>plan.WaterDistance(p.x,p.y)),group,y);
            Surface(Clip(new List<Vector2>{new Vector2(x,z),new Vector2(x+2,z+2),new Vector2(x+2,z)},p=>plan.WaterDistance(p.x,p.y)),group,y);
        }
        void TerrainCell(int group,float x,float z,float offset)
        {
            float height=plan.Height(x+1,z+1);Quad(group,x,z,2,2,height+offset);if(height>0)Cliff(x,z,height);
        }
        void WaterCell(float x,float z)=>WaterPatch(x,z,2);
        void WaterPatch(float x,float z,float size)
        {
            float da=plan.WaterDistance(x,z),db=plan.WaterDistance(x,z+size),dc=plan.WaterDistance(x+size,z+size),dd=plan.WaterDistance(x+size,z);
            if(Mathf.Min(da,db,dc,dd)>6)return;
            if(Mathf.Max(da,db,dc,dd)<-9){Quad(0,x,z,size,size,.05f);return;}
            // A narrow diagonal channel can have its deepest point inside a cell, not at a corner.
            // Resolve the absolute-distance ridge before interpolating the depth colors.
            float half=size*.5f,center=plan.WaterDistance(x+half,z+half);
            if(size>.5f&&Mathf.Abs(center-(da+db+dc+dd)*.25f)>.001f)
            {
                WaterPatch(x,z,half);WaterPatch(x+half,z,half);WaterPatch(x,z+half,half);WaterPatch(x+half,z+half,half);return;
            }
            var a=new Vector2(x,z);var b=new Vector2(x,z+size);var c=new Vector2(x+size,z+size);var d=new Vector2(x+size,z);
            WaterTriangle(a,b,c);WaterTriangle(a,c,d);
        }
        void WaterTriangle(Vector2 a,Vector2 b,Vector2 c)
        {
            var source=new List<Vector2>{a,b,c};var center=(a+b+c)/3;
            float da=plan.WaterDistance(a.x,a.y),db=plan.WaterDistance(b.x,b.y),dc=plan.WaterDistance(c.x,c.y);var ab=b-a;var ac=c-a;float det=ab.x*ac.y-ab.y*ac.x;var gradient=new Vector2(((db-da)*ac.y-(dc-da)*ab.y)/det,(ab.x*(dc-da)-ac.x*(db-da))/det);
            Func<Vector2,float> distance=p=>da+Vector2.Dot(gradient,p-a);
            bool sea=plan.ZoneAt(center.x,center.y)==ContinuousWorldPlan.EcologyZone.Ocean;
            bool natural=sea||plan.Ecology(center.x,center.y).z>.5f;
            float sa=ShallowWidth(a),sb=ShallowWidth(b),sc=ShallowWidth(c);
            var shallowGradient=new Vector2(((sb-sa)*ac.y-(sc-sa)*ab.y)/det,(ab.x*(sc-sa)-ac.x*(sb-sa))/det);
            Func<Vector2,float> depth=p=>distance(p)+sa+Vector2.Dot(shallowGradient,p-a);
            // Disjoint distance bands share all vertices. No independently extruded shore patches.
            Surface(Clip(source,p=>-depth(p)),0,.05f);
            Surface(Clip(Clip(source,p=>-distance(p)),depth),15,.05f);
            if(natural)Band(source,distance,0,4,sea?12:9,.021f);
            else {Band(source,distance,0,.45f,8,.18f);Band(source,distance,.45f,2.9f,7,.16f);Band(source,distance,2.9f,6,9,.025f);}
            var contour=new List<Vector2>();var points=new[]{a,b,c};
            for(int i=0;i<3;i++){var p=points[i];var q=points[(i+1)%3];float u=plan.WaterDistance(p.x,p.y),v=plan.WaterDistance(q.x,q.y);if((u<=0)!=(v<=0))contour.Add(Vector2.Lerp(p,q,u/(u-v)));}
            if(contour.Count==2){var p=contour[0];var q=contour[1];ShoreEdges.Add(new Vector4(p.x,p.y,q.x,q.y));if(!natural){Face(8,new Vector3(p.x,.05f,p.y),new Vector3(p.x,.18f,p.y),new Vector3(q.x,.18f,q.y),new Vector3(q.x,.05f,q.y));Face(8,new Vector3(q.x,.05f,q.y),new Vector3(q.x,.18f,q.y),new Vector3(p.x,.18f,p.y),new Vector3(p.x,.05f,p.y));}}
        }
        float ShallowWidth(Vector2 p){var e=plan.EcologyBlendAt(p.x,p.y);return 1.8f+1.2f*(e.Weight(ContinuousWorldPlan.EcologyZone.Forest)+e.Weight(ContinuousWorldPlan.EcologyZone.Hills))+7.2f*e.Weight(ContinuousWorldPlan.EcologyZone.Ocean);}
        void Band(List<Vector2> source,Func<Vector2,float> distance,float low,float high,int group,float y)
        {
            var polygon=Clip(source,p=>high-distance(p));
            if(!float.IsNegativeInfinity(low))polygon=Clip(polygon,p=>distance(p)-low);
            Surface(polygon,group,y);
        }
        void Surface(List<Vector2> polygon,int group,float y)
        {
            if(polygon.Count<3)return;
            int n=vertices[group].Count;foreach(var p in polygon)vertices[group].Add(new Vector3(p.x,y,p.y-index*64));
            for(int i=1;i<polygon.Count-1;i++){var a=polygon[i]-polygon[0];var b=polygon[i+1]-polygon[0];if(Mathf.Abs(a.x*b.y-a.y*b.x)>.00001f)triangles[group].AddRange(new[]{n,n+i,n+i+1});}
        }
        System.Collections.IEnumerator BuildCrossings()
        {
            foreach(var crossing in plan.crossings.Near((start+end)*.5f))
            {
                var c=crossing;if(!WorldCrossings.Envelope(c).Overlaps(WorldBounds)&&c.links.Count==0&&c.lots.Count==0)continue;float yaw=Mathf.Atan2(c.direction.x,c.direction.y)*Mathf.Rad2Deg;
                foreach(var link in c.links)
                {
                    BuildRoadLink(link,c.width);
                }
                foreach(var lot in c.lots)
                {
                    var p=lot.center;float u=Vector2.Dot(p-c.center,c.direction),v=Vector2.Dot(p-c.center,c.riverDirection);
                    CrossingStrip(c,u-6,u+6,v-5,v+5,4,.035f);
                    if(p.y<start||p.y>=end||p.x<minX||p.x>=maxX)continue;
                    var candidates=data.Rules.Where(r=>r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)&&r.Weight>0&&r.MaxPerChunk==0).Where(r=>{var f=data.Footprint(data.Prefabs[r.PrefabId]);return f.Modules.x*.25f<=9&&f.Modules.z*.25f<=10&&f.floors<=8;}).ToArray();
                    if(candidates.Length==0)continue;
                    var rule=candidates[plan.Hash((int)p.x,(int)p.y,2301)%candidates.Length];var prefab=data.Prefabs[rule.PrefabId];var fp=data.Footprint(prefab);
                    var obj=Instantiate(prefab,transform);obj.transform.localPosition=new Vector3(p.x,.06f,p.y-index*64);obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
                    float width=fp.Modules.x*.25f,depth=fp.Modules.z*.25f;
                    var extent=new Vector2(Mathf.Abs(c.riverDirection.x)*width+Mathf.Abs(c.direction.x)*depth,Mathf.Abs(c.riverDirection.y)*width+Mathf.Abs(c.direction.y)*depth);
                    RotatedFootprints.Add(new Rect(p-extent*.5f,extent));BuildingCount++;
                }
                var steps=BuildBridge(c);while(steps.MoveNext())yield return null;
            }
        }
        float DeckHeight(WorldCrossings.Crossing c,float u)
        {
            float height=c.natural?1.25f:6f;
            float ramp=Mathf.InverseLerp(c.natural?c.halfWater+2:c.halfLength-32,c.halfLength,Mathf.Abs(u));
            return Mathf.Lerp(height,.055f,ramp);
        }
        void BuildRoadLink(Vector4 link,float width)
        {
            var a=new Vector2(link.x,link.y);var b=new Vector2(link.z,link.w);float length=(b-a).magnitude;if(length<.01f)return;
            var direction=(b-a)/length;var frame=new WorldCrossings.Crossing{center=(a+b)*.5f,direction=direction,riverDirection=new Vector2(-direction.y,direction.x)};
            CrossingStrip(frame,-length*.5f-.35f,length*.5f+.35f,-width*.5f,width*.5f,5,.075f);
        }
        Vector3 BridgePoint(WorldCrossings.Crossing c,float u,float v,float y){var p=c.center+c.direction*u+c.riverDirection*v;return new Vector3(p.x,y,p.y);}
        System.Collections.IEnumerator BuildBridge(WorldCrossings.Crossing c)
        {
            var budget=System.Diagnostics.Stopwatch.StartNew();
            float half=c.width*.5f,thickness=c.natural?.23f:.5f;
            for(float u=-c.halfLength;u<c.halfLength;u+=2)
            {
                float v=Mathf.Min(u+2,c.halfLength),a=DeckHeight(c,u),b=DeckHeight(c,v);
                var pa=c.center+c.direction*u;var pb=c.center+c.direction*v;
                if(Mathf.Max(pa.x,pb.x)<minX-4||Mathf.Min(pa.x,pb.x)>maxX+4||Mathf.Max(pa.y,pb.y)<start-4||Mathf.Min(pa.y,pb.y)>end+4)continue;
                bool span=Mathf.Abs((u+v)*.5f)<c.halfWater+2;
                BridgeBox(c,u,v,-half,half,Mathf.Max(0,a-thickness),a,Mathf.Max(0,b-thickness),b,c.natural?10:span?16:8);
                DeckStrip(c,u,v,-half,half,c.natural?10:5,.012f);
                if(c.natural)TimberBridgeTiles++;
                else
                {
                    DeckStrip(c,u,v,-half,-half+.38f,7,.06f);DeckStrip(c,u,v,half-.38f,half,7,.06f);
                    DeckStrip(c,u,v,-half+.49f,-half+.58f,6,.025f);DeckStrip(c,u,v,half-.58f,half-.49f,6,.025f);
                    if(Mathf.Abs(((int)u/2)%3)!=0)DeckStrip(c,u,v,-.045f,.045f,6,.025f);
                }
                foreach(int side in new[]{-1,1})
                {
                    float edge=side*(half-.12f);
                    foreach(float rail in new[]{.42f,.82f})BridgeBox(c,u,v,edge-.055f,edge+.055f,a+rail,a+rail+.07f,b+rail,b+rail+.07f,c.natural?10:17);
                    if(((int)(u+c.halfLength)/2)%2==0)BridgeBox(c,u,u+.14f,edge-.075f,edge+.075f,a,a+.91f,a,a+.91f,c.natural?10:17);
                }
                if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
            }
            if(c.natural)yield break;
            // Concrete abutments and intermediate piers carry the painted longitudinal girders.
            foreach(int side in new[]{-1,1}){float u=side*(c.halfWater+1.5f);BridgeBox(c,u-.55f,u+.55f,-half-.12f,half+.12f,-.4f,5.5f,-.4f,5.5f,8);}
            for(float u=-c.halfLength+12;u<c.halfLength-10;u+=16)
            {
                float h=DeckHeight(c,u)-.5f;if(h<1)continue;
                BridgeBox(c,u-.5f,u+.5f,-.45f,.45f,0,h,0,h,8);
                BridgeBox(c,u-.6f,u+.6f,-half+.15f,half-.15f,h-.35f,h,h-.35f,h,8);
            }
            BuildSign(c,c.halfWater+22,DeckHeight(c,c.halfWater+22));
        }
        System.Collections.IEnumerator BuildHighway(WorldHighways.Route route)
        {
            bool touches=false;for(int k=1;k<route.points.Count;k++){var aa=route.points[k-1];var bb=route.points[k];if(Mathf.Max(aa.x,bb.x)>=minX-4&&Mathf.Min(aa.x,bb.x)<=maxX+4&&Mathf.Max(aa.y,bb.y)>=start-4&&Mathf.Min(aa.y,bb.y)<=end+4){touches=true;break;}}
            if(!touches)yield break;HighwayRoutes.Add(route);
            var budget=System.Diagnostics.Stopwatch.StartNew();
            var samples=new List<Vector2>();var distances=new List<float>();float walked=0;
            samples.Add(route.points[0]);distances.Add(0);
            for(int part=0;part<route.points.Count-1;part++){
                var a=route.points[part];var b=route.points[part+1];float length=Vector2.Distance(a,b);
                int steps=Mathf.CeilToInt(length/2);
                for(int n=1;n<=steps;n++){samples.Add(Vector2.Lerp(a,b,n/(float)steps));distances.Add(walked+length*n/steps);}
                walked+=length;
            }
            var offsets=new Vector2[samples.Count];
            for(int i=0;i<samples.Count;i++){
                Vector2 incoming=(samples[i]-samples[Mathf.Max(0,i-1)]).normalized,outgoing=(samples[Mathf.Min(i+1,samples.Count-1)]-samples[i]).normalized;
                if(i==0)incoming=outgoing;if(i==samples.Count-1)outgoing=incoming;
                Vector2 n1=new Vector2(-incoming.y,incoming.x),n2=new Vector2(-outgoing.y,outgoing.x);
                var miter=(n1+n2).normalized;offsets[i]=miter/Mathf.Max(.9f,Vector2.Dot(miter,n2));
            }
            int supportIndex=0;
            float nextSupport=route.independentLayer?(route.supports.Count>0?route.supports[0]:float.PositiveInfinity):16,nextSign=96,halfWidth=route.HalfWidth;
            for(int i=0;i<samples.Count-1;i++){
                var a=samples[i];var b=samples[i+1];float da=distances[i],db=distances[i+1];
                bool visible=Mathf.Max(a.y,b.y)>=start-4&&Mathf.Min(a.y,b.y)<=end+4&&Mathf.Max(a.x,b.x)>=minX-4&&Mathf.Min(a.x,b.x)<=maxX+4;
                float ha=route.Height(da),hb=route.Height(db);
                if(visible){
                    HighwayBox(a,b,offsets[i],offsets[i+1],-halfWidth,halfWidth,Mathf.Max(0,ha-.5f),ha,Mathf.Max(0,hb-.5f),hb,16);
                    HighwayStrip(a,b,offsets[i],offsets[i+1],-halfWidth,halfWidth,ha+.025f,hb+.025f,5);
                    // The corridor reserves and supplies its own broad ground road.
                    if(!route.upperTier)HighwayStrip(a,b,offsets[i],offsets[i+1],-3.1f,3.1f,.035f,.035f,5);
                    if(((int)(da/2))%3!=0)HighwayStrip(a,b,offsets[i],offsets[i+1],-.07f,.07f,ha+.07f,hb+.07f,6);
                    foreach(int side in new[]{-1,1}){
                        if(route.ramp&&(route.ascending?da<8:da>route.length-8))continue;
                        if(route.exits.Any(e=>side==Mathf.RoundToInt(e.y)&&da>=e.x&&da<=e.x+e.z))continue;
                        float edge=side*(halfWidth-.12f);
                        HighwayBox(a,b,offsets[i],offsets[i+1],edge-.1f,edge+.1f,ha+.12f,ha+.65f,hb+.12f,hb+.65f,17);
                        if(route.noiseBarrier&&da>128&&db<route.length-128)
                            HighwayBox(a,b,offsets[i],offsets[i+1],edge-.08f,edge+.08f,ha+.65f,ha+1.65f,hb+.65f,hb+1.65f,17);
                    }
                }
                while(nextSupport<=db){
                    float t=Mathf.InverseLerp(da,db,nextSupport);var p=Vector2.Lerp(a,b,t);float h=route.Height(nextSupport);
                    if(visible&&h>2&&(!route.upperTier||route.AllowsSupport(p))){
                        var direction=(b-a).normalized;var frame=new WorldCrossings.Crossing{center=p,direction=direction,riverDirection=new Vector2(-direction.y,direction.x),width=halfWidth*2};
                        float footing=route.independentLayer?Mathf.Max(-3,plan.Height(p.x,p.y)):0;
                        BridgeBox(frame,-.45f,.45f,-.38f,.38f,footing,h-.5f,footing,h-.5f,8);
                        BridgeBox(frame,-.5f,.5f,-halfWidth+.35f,halfWidth-.35f,h-.9f,h-.5f,h-.9f,h-.5f,8);
                    }
                    if(route.independentLayer){supportIndex++;nextSupport=supportIndex<route.supports.Count?route.supports[supportIndex]:float.PositiveInfinity;}
                    else nextSupport+=20;
                }
                while(nextSign<=db){
                    if(visible&&nextSign>128&&nextSign<route.length-128){
                        var direction=(b-a).normalized;var frame=new WorldCrossings.Crossing{center=Vector2.Lerp(a,b,Mathf.InverseLerp(da,db,nextSign)),direction=direction,riverDirection=new Vector2(-direction.y,direction.x),width=halfWidth*2};
                        BuildSign(frame,0,route.Height(nextSign));
                    }
                    nextSign+=192;
                }
                if(budget.Elapsed.TotalMilliseconds>2){yield return null;budget.Restart();}
            }
        }
        public static float HighwayHeight(float distance,float length)
        {
            // 144m for a 5.91m rise: max grade 4.56%, with smooth end transitions.
            float t=Mathf.Clamp01(Mathf.Min(distance,length-distance)/144f);
            float ease=t<.1f?t*t/.2f:t>.9f?.9f-(1-t)*(1-t)/.2f:t-.05f;
            return Mathf.Lerp(.09f,6,ease/.9f);
        }
        void HighwayStrip(Vector2 a,Vector2 b,Vector2 na,Vector2 nb,float left,float right,float ha,float hb,int group)
        {
            var p=new[]{HighwayPoint(a,na,left,ha),HighwayPoint(b,nb,left,hb),HighwayPoint(b,nb,right,hb),HighwayPoint(a,na,right,ha)};
            if(Vector3.Cross(p[1]-p[0],p[2]-p[0]).y<0)Array.Reverse(p);BridgeFace(group,p);
        }
        static Vector3 HighwayPoint(Vector2 p,Vector2 normal,float side,float height)=>new Vector3(p.x+normal.x*side,height,p.y+normal.y*side);
        void HighwayBox(Vector2 a,Vector2 b,Vector2 na,Vector2 nb,float left,float right,float la,float ha,float lb,float hb,int group)
        {
            var p=new[]{HighwayPoint(a,na,left,la),HighwayPoint(b,nb,left,lb),HighwayPoint(b,nb,right,lb),HighwayPoint(a,na,right,la),HighwayPoint(a,na,left,ha),HighwayPoint(b,nb,left,hb),HighwayPoint(b,nb,right,hb),HighwayPoint(a,na,right,ha)};
            var center=(p[0]+p[2]+p[4]+p[6])*.25f;
            foreach(var ids in new[]{new[]{0,1,2,3},new[]{4,5,6,7},new[]{0,4,5,1},new[]{1,5,6,2},new[]{2,6,7,3},new[]{3,7,4,0}}){
                var face=ids.Select(n=>p[n]).ToArray();if(Vector3.Dot(Vector3.Cross(face[1]-face[0],face[2]-face[0]),face[0]-center)<0)Array.Reverse(face);BridgeFace(group,face);
            }
        }
        void SlopedStrip(WorldCrossings.Crossing c,float u,float v,float left,float right,float ha,float hb,int group,float offset)
        {
            var p=new[]{BridgePoint(c,u,left,ha+offset),BridgePoint(c,v,left,hb+offset),BridgePoint(c,v,right,hb+offset),BridgePoint(c,u,right,ha+offset)};
            if(Vector3.Cross(p[1]-p[0],p[2]-p[0]).y<0)System.Array.Reverse(p);BridgeFace(group,p);
        }
        void BuildSign(WorldCrossings.Crossing c,float u,float h)
        {
            float sideScale=c.width/3.8f;
            BridgeBox(c,u-.07f,u+.07f,-1.85f*sideScale,-1.65f*sideScale,h,h+3.5f,h,h+3.5f,17);
            BridgeBox(c,u-.07f,u+.07f,1.65f*sideScale,1.85f*sideScale,h,h+3.5f,h,h+3.5f,17);
            BridgeBox(c,u-.12f,u+.12f,-2.05f*sideScale,2.05f*sideScale,h+2.1f,h+3.5f,h+2.1f,h+3.5f,18);
            // C1 route shield and a forward arrow, baked into the mesh instead of floating UI text.
            string[] glyph={"11100100100","10001101110","10000110101","10000100100","11101110100"};
            for(int row=0;row<5;row++)for(int col=0;col<11;col++)if(glyph[row][col]=='1')
            {float side=(-1.42f+col*.25f)*sideScale,y=h+3.2f-row*.20f;BridgeBox(c,u-.14f,u-.13f,side,side+.15f*sideScale,y,y+.13f,y,y+.13f,6);}
        }
        void DeckStrip(WorldCrossings.Crossing c,float u,float v,float left,float right,int group,float offset)
        {
            var p=new[]{BridgePoint(c,u,left,DeckHeight(c,u)+offset),BridgePoint(c,v,left,DeckHeight(c,v)+offset),BridgePoint(c,v,right,DeckHeight(c,v)+offset),BridgePoint(c,u,right,DeckHeight(c,u)+offset)};
            if(Vector3.Cross(p[1]-p[0],p[2]-p[0]).y<0)System.Array.Reverse(p);BridgeFace(group,p);
        }
        void BridgeBox(WorldCrossings.Crossing c,float u,float v,float left,float right,float lowA,float highA,float lowB,float highB,int group)
        {
            var p=new[]{BridgePoint(c,u,left,lowA),BridgePoint(c,v,left,lowB),BridgePoint(c,v,right,lowB),BridgePoint(c,u,right,lowA),BridgePoint(c,u,left,highA),BridgePoint(c,v,left,highB),BridgePoint(c,v,right,highB),BridgePoint(c,u,right,highA)};
            var center=(p[0]+p[2]+p[4]+p[6])*.25f;
            foreach(var ids in new[]{new[]{0,1,2,3},new[]{4,5,6,7},new[]{0,4,5,1},new[]{1,5,6,2},new[]{2,6,7,3},new[]{3,7,4,0}})
            {
                var face=ids.Select(i=>p[i]).ToArray();if(Vector3.Dot(Vector3.Cross(face[1]-face[0],face[2]-face[0]),face[0]-center)<0)System.Array.Reverse(face);BridgeFace(group,face);
            }
        }
        void BridgeFace(int group,Vector3[] points)
        {
            var poly=new List<Vector3>(points);
            foreach(int axis in new[]{0,1,2,3})
            {
                if(poly.Count<3)return;var result=new List<Vector3>();var a=poly[poly.Count-1];
                Func<Vector3,float> inside=p=>axis==0?p.z-start:axis==1?end-p.z:axis==2?p.x-minX:maxX-p.x;
                float da=inside(a);foreach(var b in poly){float db=inside(b);if((da>=0)!=(db>=0))result.Add(Vector3.Lerp(a,b,da/(da-db)));if(db>=0)result.Add(b);a=b;da=db;}poly=result;
            }
            int n=vertices[group].Count;foreach(var p in poly)vertices[group].Add(p-Vector3.forward*(index*64));for(int i=1;i<poly.Count-1;i++)triangles[group].AddRange(new[]{n,n+i,n+i+1});
        }
        void CrossingStrip(WorldCrossings.Crossing c,float u,float v,float left,float right,int group,float y)
        {
            var poly=new List<Vector2>{c.center+c.direction*u+c.riverDirection*left,c.center+c.direction*v+c.riverDirection*left,c.center+c.direction*v+c.riverDirection*right,c.center+c.direction*u+c.riverDirection*right};
            float area=0;for(int i=0;i<poly.Count;i++){var a=poly[i];var b=poly[(i+1)%poly.Count];area+=a.x*b.y-b.x*a.y;}if(area>0)poly.Reverse();
            poly=Clip(poly,p=>p.y-start);poly=Clip(poly,p=>end-p.y);poly=Clip(poly,p=>p.x-minX);poly=Clip(poly,p=>maxX-p.x);Surface(poly,group,y);
        }
        static List<Vector2> Clip(List<Vector2> polygon,Func<Vector2,float> inside)
        {
            var result=new List<Vector2>();if(polygon.Count==0)return result;
            var previous=polygon[polygon.Count-1];float a=inside(previous);
            foreach(var current in polygon){float b=inside(current);if((a>=0)!=(b>=0))result.Add(Vector2.Lerp(previous,current,a/(a-b)));if(b>=0)result.Add(current);previous=current;a=b;}return result;
        }
        // Choose one owning population region per contiguous city and asset, independent of streaming order.
        bool SignatureAllowed(SpawnRule rule,Rect parcel)
        {
            var signature=data.Prefabs[rule.PrefabId].GetComponent<CommercialSignature>();
            if(!signature)return true;
            float z=parcel.center.y;
            if(plan.ZoneRange(z,out float begin,out float finish)!=ContinuousWorldPlan.EcologyZone.City)return false;
            for(int i=0;i<128;i++){if(plan.ZoneRange(begin-1,out float previous,out _)!=ContinuousWorldPlan.EcologyZone.City)break;begin=previous;}
            for(int i=0;i<128;i++){if(plan.ZoneRange(finish+1,out _,out float next)!=ContinuousWorldPlan.EcologyZone.City)break;finish=next;}
            int code=0;foreach(char c in signature.assetCode)code=unchecked(code*31+c);
            int first=Mathf.CeilToInt((begin+48)/128),last=Mathf.FloorToInt((finish-48)/128);
            if(last<first)return false;
            int ownerZ=first+plan.Hash((int)begin,code,2813)%(last-first+1);
            int ownerX=Mathf.FloorToInt(plan.routeSlope*(ownerZ*128+64)/128)+(plan.Hash((int)begin,code,2819)%2==0?-1:0);
            return Mathf.FloorToInt(parcel.center.x/128)==ownerX&&Mathf.FloorToInt(z/128)==ownerZ;
        }
        System.Collections.IEnumerator Populate(ContinuousWorldPlan.Parcel parcel,Dictionary<string,int> counts)
        {
            var budget=System.Diagnostics.Stopwatch.StartNew();
            var bounds=parcel.bounds;var random=new System.Random(plan.Hash((int)(bounds.x*4),(int)(bounds.y*4),727));
            var rules=data.Rules.Where(r=>r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)&&r.Weight>0&&!data.Prefabs[r.PrefabId].GetComponent<BuildingStreetFrontage>()&&SignatureAllowed(r,parcel.bounds)).ToArray();
            var occupied=new List<Rect>();string previous=null;var diversity=new BuildingDiversityPicker();
            // Frontage strips can span a large parcel; internal access lanes are reserved every ~18 units.
            for(float z=bounds.yMin+.5f;z<bounds.yMax-3;)
            {
                float depth=Mathf.Min(16,bounds.yMax-.5f-z),x=bounds.xMin+.5f;
                int rowStart=occupied.Count;
                if(depth<3)break;
                while(x<bounds.xMax-3)
                {
                    if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
                    bool Fits(BuildingFootprint f,bool turn)
                    {
                        float w=(turn?f.Modules.z:f.Modules.x)*.25f,d=(turn?f.Modules.x:f.Modules.z)*.25f;
                        var lot=new Rect(x,z,w,d);
                        return w<=bounds.xMax-.5f-x&&d<=depth&&!streetReservations.Any(a=>a.Overlaps(lot))&&plan.AllowsBuilding(lot,f.floors);
                    }
                    var eligible=rules.Where(r=>{var f=data.Footprint(data.Prefabs[r.PrefabId]);return (Fits(f,false)||(f.allowQuarterTurn&&Fits(f,true)))&&r.DistrictWeight(parcel.district)>0&&(!counts.ContainsKey(r.Id)||r.MaxPerChunk==0||counts[r.Id]<r.MaxPerChunk);}).ToArray();
                    if(eligible.Length==0){x+=2;previous=null;continue;}
                    Func<SpawnRule,double> weight=r=>r.Weight*r.DistrictWeight(parcel.district)*(r.Family==previous?(previous=="House"?1.8:1.15):1);
                    var chosen=diversity.Choose(eligible,weight,random.NextDouble());
                    var prefab=data.Prefabs[chosen.PrefabId];var fp=data.Footprint(prefab);float width=fp.Modules.x*.25f,length=fp.Modules.z*.25f;
                    bool quarterTurn=fp.allowQuarterTurn&&!Fits(fp,false);
                    if(quarterTurn){float swap=width;width=length;length=swap;}
                    var rect=new Rect(x,z,width,length);occupied.Add(rect);counts[chosen.Id]=counts.TryGetValue(chosen.Id,out int n)?n+1:1;
                    recording.buildings.Add(new WorldPopulationCache.Building{prefab=prefab,bounds=rect,rotation=(quarterTurn?90:0)+random.Next(2)*180,tint=true,color=kit.facadePalette[plan.Hash((int)(x*4),(int)(z*4),98)%kit.facadePalette.Length]});
                    x+=width+.25f;previous=chosen.Family;
                }
                // Opposite row packs the unused rear space rather than spreading frontage gaps.
                float rear=z+depth;
                for(float xx=bounds.xMin+.5f;xx<bounds.xMax-4;)
                {
                    if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
                    var candidates=rules.Where(r=>{var f=data.Footprint(data.Prefabs[r.PrefabId]);var rr=new Rect(xx,rear-f.Modules.z*.25f,f.Modules.x*.25f,f.Modules.z*.25f);return f.floors<=4&&rr.xMax<=bounds.xMax-.5f&&rr.yMin>=z&&r.DistrictWeight(parcel.district)>0&&(!counts.ContainsKey(r.Id)||r.MaxPerChunk==0||counts[r.Id]<r.MaxPerChunk)&&!occupied.Any(a=>a.Overlaps(rr))&&!streetReservations.Any(a=>a.Overlaps(rr))&&plan.AllowsBuilding(rr,f.floors);}).ToArray();
                    if(candidates.Length==0){xx+=1;continue;}
                    var choice=diversity.Choose(candidates,r=>r.Weight*r.DistrictWeight(parcel.district),plan.Hash((int)(xx*4),(int)(z*4),91)/(double)int.MaxValue);var pf=data.Prefabs[choice.PrefabId];var fpp=data.Footprint(pf);var rr2=new Rect(xx,rear-fpp.Modules.z*.25f,fpp.Modules.x*.25f,fpp.Modules.z*.25f);
                    // Close the unused gap behind shallow front buildings, preserving a service gap.
                    float nearest=z;
                    foreach(var front in occupied)
                        if(front.xMin<rr2.xMax+.25f&&front.xMax>rr2.xMin-.25f&&front.yMax>z)
                            nearest=Mathf.Max(nearest,front.yMax+.25f);
                    var packed=new Rect(rr2.x,Mathf.Ceil(nearest*4)/4,rr2.width,rr2.height);
                    if(packed.yMax<=rear&&!streetReservations.Any(a=>a.Overlaps(packed))&&plan.AllowsBuilding(packed,fpp.floors))rr2=packed;
                    occupied.Add(rr2);counts[choice.Id]=counts.TryGetValue(choice.Id,out int rn)?rn+1:1;
                    recording.buildings.Add(new WorldPopulationCache.Building{prefab=pf,bounds=rr2,rotation=180});xx=rr2.xMax+.25f;
                }
                // A 16-unit strip is a fitting limit, not mandatory empty pavement behind each row.
                float usedDepth=0;
                for(int b=rowStart;b<occupied.Count;b++)usedDepth=Mathf.Max(usedDepth,occupied[b].yMax-z);
                z+=(usedDepth>0?usedDepth:depth)+2.5f;
                if(z<bounds.yMax-3)for(float xx=bounds.xMin;xx<bounds.xMax;xx+=2)if(plan.Sample(xx,z-1)==ContinuousWorldPlan.Land.Urban)
                {float width=Mathf.Min(2,bounds.xMax-xx);var road=new Rect(xx,z-2.5f,width,2.5f);if(!streetReservations.Any(a=>a.Overlaps(road))&&CanRoad(xx,z-2.5f,width,2.5f))recording.roads.Add(road);}
            }
            recording.choices+=diversity.Choices;recording.singleOptions+=diversity.SingleOptionChoices;
            recording.repeats+=diversity.Repeats;recording.forcedRepeats+=diversity.ForcedRepeats;
        }
        // Large authored buildings use complete existing parcels before narrow frontage rows.
        // Reserve only the actual footprint and entrance aprons; ordinary buildings fill the rest.
        System.Collections.IEnumerator PopulateStreetBuildings(ContinuousWorldPlan.Region region,Dictionary<string,int> counts)
        {
            var budget=System.Diagnostics.Stopwatch.StartNew();
            var roads=new List<Rect>(region.roads);
            var origin=region.roads[0].position;
            roads.Add(new Rect(origin.x+128,origin.y,4,128));
            roads.Add(new Rect(origin.x,origin.y+128,128,4));
            var random=new System.Random(plan.Hash((int)origin.x,(int)origin.y,3983));
            var rules=data.Rules.Where(r=>r.Weight>0&&r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)&&data.Prefabs[r.PrefabId].GetComponent<BuildingStreetFrontage>())
                .OrderBy(r=>-Math.Log(Math.Max(.000001,random.NextDouble()))/r.Weight).ToArray();
            foreach(var rule in rules)
            {
                var prefab=data.Prefabs[rule.PrefabId];var fp=data.Footprint(prefab);
                var street=prefab.GetComponent<BuildingStreetFrontage>();var candidates=new List<WorldPopulationCache.Building>();
                foreach(var parcel in region.parcels.OrderBy(p=>plan.Hash((int)(p.bounds.x*4),(int)(p.bounds.y*4),int.Parse(rule.Id))))
                {
                    if(rule.DistrictWeight(parcel.district)<=0||!SignatureAllowed(rule,parcel.bounds))continue;
                    int firstRotation=random.Next(4);
                    for(int q=0;q<4;q++)
                    {
                        int quarter=(firstRotation+q)%4,rotation=quarter*90;
                        float w=(quarter%2==0?fp.Modules.x:fp.Modules.z)*.25f,d=(quarter%2==0?fp.Modules.z:fp.Modules.x)*.25f;
                        var b=parcel.bounds;if(w>b.width-1||d>b.height-1)continue;
                        foreach(float x in new[]{b.xMin+.5f,b.xMax-.5f-w})foreach(float z in new[]{b.yMin+.5f,b.yMax-.5f-d})
                        {
                            if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
                            var lot=new Rect(x,z,w,d);
                            if(streetReservations.Any(a=>a.Overlaps(lot))||!street.CanPlace(lot,rotation,roads,recording.buildings.Select(a=>a.bounds))||!plan.AllowsBuilding(lot,fp.floors))continue;
                            // A planned road through water/forest may not be rendered: verify the facing road too.
                            if(street.Aprons(lot,rotation).Any(a=>!CanRoad(a.center.x-.5f,a.center.y-.5f,1,1)))continue;
                            candidates.Add(new WorldPopulationCache.Building{prefab=prefab,bounds=lot,rotation=rotation});
                        }
                    }
                }
                if(candidates.Count==0)continue;
                // Choose between valid facing/other placements, not between arbitrary rotations.
                // If geography only allows one group, use it without removing a spawn opportunity.
                var chosen=ChooseStreetCandidate(candidates,CommercialViewDirection,random.NextDouble(),random.NextDouble());
                recording.buildings.Add(chosen);streetReservations.Add(chosen.bounds);
                streetReservations.AddRange(street.Aprons(chosen.bounds,chosen.rotation));counts[rule.Id]=1;
            }
        }
        public static bool AdvertisementFacesView(GameObject prefab,int rotation,Vector3 viewDirection)
            =>AdvertisementFacesView(PrimaryAdvertisementNormal(prefab),rotation,viewDirection);
        static Vector3 PrimaryAdvertisementNormal(GameObject prefab)
        {
            var slots=prefab.GetComponent<BuildingDecorationSlots>();Vector3 normal=Vector3.back;
            if(slots){
                var facade=slots.slots.Where(s=>s.surface==BuildingDecorationSlots.Surface.Facade&&(s.allowed&(BuildingDecorationSlots.Kind.Sign|BuildingDecorationSlots.Kind.Screen))!=0).ToArray();
                // A large animated screen is the principal face; otherwise use the largest facade sign.
                var screens=facade.Where(s=>(s.allowed&BuildingDecorationSlots.Kind.Screen)!=0).ToArray();
                var signs=screens.Length>0?screens:facade;
                if(signs.Length>0)normal=signs.OrderByDescending(s=>s.maximumSize.x*s.maximumSize.y).First().outwardNormal;
            }
            return normal;
        }
        static bool AdvertisementFacesView(Vector3 normal,int rotation,Vector3 viewDirection)
        {
            normal=Quaternion.Euler(0,rotation,0)*normal;normal.y=0;viewDirection.y=0;
            if(viewDirection.sqrMagnitude<.001f)viewDirection=new Vector3(-1,0,-1);
            return Vector3.Dot(normal.normalized,viewDirection.normalized)>.25f;
        }
        public static WorldPopulationCache.Building ChooseStreetCandidate(IReadOnlyList<WorldPopulationCache.Building> candidates,Vector3 viewDirection,double facingRoll,double choiceRoll)
        {
            if(candidates.Count==0)throw new ArgumentException("No valid street placements",nameof(candidates));
            var facing=new List<WorldPopulationCache.Building>();var other=new List<WorldPopulationCache.Building>();
            var normals=candidates.Select(c=>c.prefab).Distinct().ToDictionary(p=>p,PrimaryAdvertisementNormal);
            foreach(var c in candidates)(AdvertisementFacesView(normals[c.prefab],c.rotation,viewDirection)?facing:other).Add(c);
            var group=facingRoll<CommercialFacingProbability?facing:other;
            if(group.Count==0)group=facing.Count>0?facing:other;
            return group[Math.Min(group.Count-1,(int)(Math.Max(0,choiceRoll)*group.Count))];
        }
        System.Collections.IEnumerator EnsurePopulation(ContinuousWorldPlan.Region region,WorldPopulationCache cache)
        {
            if(cache.TryGet(region,out var existing))yield break;
            var budget=System.Diagnostics.Stopwatch.StartNew();
            recording=new WorldPopulationCache.Region();streetReservations.Clear();var counts=new Dictionary<string,int>();
            foreach(var green in plan.greenSpaces.Prepare(region,kit)){recording.buildings.Add(green);streetReservations.Add(green.bounds);}
            var large=PopulateStreetBuildings(region,counts);while(large.MoveNext()){yield return null;budget.Restart();}
            foreach(var parcel in region.parcels)
            {
                var steps=Populate(parcel,counts);while(steps.MoveNext()){yield return null;budget.Restart();}
                if(budget.Elapsed.TotalMilliseconds>3){yield return null;budget.Restart();}
            }
            cache.Add(region,recording);recording=null;
        }
        void OnDestroy(){ownedPopulationCache?.palette.Dispose();foreach(var m in meshes)if(m)Destroy(m);foreach(var m in materials)if(m)Destroy(m);foreach(var t in terrainTextures)if(t)Destroy(t);}
    }
}

















