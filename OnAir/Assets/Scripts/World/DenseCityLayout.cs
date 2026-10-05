using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
#if UNITY_EDITOR
using UnityEditor;
#endif
namespace OnAir
{
    // Connected spines have fixed boundary ports. Seeded side streets meet them at staggered T junctions.
    // All dimensions are scene units. Existing authored building footprints and scales are preserved.
    public sealed class DenseCityLayout : MonoBehaviour
    {
        public const float HalfWidth=96,HalfLength=32;
        public const float SideStreetWidth=3.5f;
        public static readonly float[] Spines={-60,-28,4,28,60};
        public readonly List<Rect> RoadBounds=new List<Rect>();
        public readonly List<Rect> BuildingBounds=new List<Rect>();
        public readonly List<Rect> ModuleBounds=new List<Rect>();
        public readonly List<Rect> EmptyPlots=new List<Rect>();
        public readonly List<float> FrontageGaps=new List<float>();
        public sealed class Placement { public string id,family; public int district,floors,row; public Rect bounds; }
        public readonly List<Placement> Placements=new List<Placement>();
        readonly Dictionary<string,int> counts=new Dictionary<string,int>();
        bool planOnly;int rowSequence;
        readonly List<Mesh> ownedMeshes=new List<Mesh>();
        public float RoadFraction {get;private set;}
        public int BuildingCount {get;private set;}
        static float Width(float x)=>x==4?5f:4f;
        static float SnapUp(float value)=>Mathf.Ceil(value/BuildingFootprint.ModuleUnit)*BuildingFootprint.ModuleUnit;
        static float SnapDown(float value)=>Mathf.Floor(value/BuildingFootprint.ModuleUnit)*BuildingFootprint.ModuleUnit;
        static GameObject Place(GameObject prefab,Transform parent,Vector3 position,float yaw)
        {
            GameObject item;
#if UNITY_EDITOR
            item=!Application.isPlaying?(GameObject)PrefabUtility.InstantiatePrefab(prefab,parent):Instantiate(prefab,parent);
#else
            item=Instantiate(prefab,parent);
#endif
            item.transform.localPosition=position;item.transform.localRotation=Quaternion.Euler(0,yaw,0);return item;
        }
        public static int Build(BlockGenerator block)
        {
            var layout=block.generated.gameObject.AddComponent<DenseCityLayout>();layout.Generate(block);return layout.BuildingCount;
        }
        public static DenseCityLayout Plan(BlockGenerator block)
        {
            var layout=block.gameObject.AddComponent<DenseCityLayout>();layout.planOnly=true;layout.Generate(block);return layout;
        }
        void Generate(BlockGenerator block)
        {
            var random=new System.Random(block.seed);
            var surfaces=new SurfaceBatch(this,block.kit.cityAsphalt,block.kit.cityPaving,block.kit.cityPaint);
            surfaces.Box(1,new Vector3(0,-.04f,0),new Vector3(192,.08f,64));
            foreach(float x in Spines)Road(surfaces,new Rect(x-Width(x)/2,-32,Width(x),64));
            // Half-width boundary collector strips join exactly to the neighboring chunk's half.
            Road(surfaces,new Rect(-96,-32,192,1.75f));Road(surfaces,new Rect(-96,30.25f,192,1.75f));
            var buildings=new GameObject("Dense building parcels").transform;buildings.SetParent(transform,false);
            var furniture=new GameObject("Street planting").transform;furniture.SetParent(transform,false);
            var rules=block.buildings.Rules.Where(r=>r.Matches(block.biome,block.weather,block.period)&&r.Weight>0).ToList();
            if(rules.Count==0)throw new InvalidOperationException("City needs active building rules.");
            string last=null;
            var districts=new System.Random(block.seed^38197);int district=District(districts);
            for(int band=0;band<=Spines.Length;band++)
            {
                if(band>0 && districts.NextDouble()>.65)district=District(districts);
                float left=band==0?-96:Spines[band-1]+Width(Spines[band-1])/2;
                float right=band==Spines.Length?96:Spines[band]-Width(Spines[band])/2;
                float a=-10.5f+random.Next(-6,7)*.25f,b=10.5f+random.Next(-6,7)*.25f;
                // Independent cross-street offsets give different block depths in adjacent bands.
                foreach(float z in new[]{a,b})
                {
                    Road(surfaces,new Rect(left,z-1.75f,right-left,SideStreetWidth));
                    if(band>0)Crosswalk(surfaces,left+1.15f,z);
                    // Single edge line, not motorway-style centre dashes on a residential lane.
                    surfaces.Box(2,new Vector3((left+right)/2,.029f,z-1.4f),new Vector3(Mathf.Max(1,right-left-4),.008f,.055f));
                }
                float[] lower={-30.25f,a+1.75f,b+1.75f},upper={a-1.75f,b-1.75f,30.25f};
                for(int row=0;row<3;row++)
                {
                    float inset=block.kit.streetSetbackModules*BuildingFootprint.ModuleUnit;
                    float x0=SnapUp(left+inset),x1=SnapDown(right-inset);
                    float z0=SnapUp(lower[row]+inset),z1=SnapDown(upper[row]-inset);
                    var parcel=Rect.MinMaxRect(x0,z0,x1,z1);
                    // Two street-facing rows. Pack the real module widths sequentially; never distribute spare space between buildings.
                    int localDistrict=districts.NextDouble()<.85?district:1;
                    PackFrontage(block,random,rules,buildings,parcel,false,localDistrict,ref last);
                    PackFrontage(block,random,rules,buildings,parcel,true,localDistrict,ref last);
                }
                // One small street tree per band, with explicit building clearance.
                var treePoint=new Vector3(left+.65f,.01f,a+2.65f);
                var clearance=new Rect(treePoint.x-.7f,treePoint.z-.7f,1.4f,1.4f);
                if(!planOnly&&!BuildingBounds.Any(r=>r.Overlaps(clearance)))Place(block.kit.tree,furniture,treePoint,0);
            }
            foreach(float x in Spines)for(float z=-26;z<30;z+=7)
            {
                if(x!=4)continue;
                bool nearJunction=RoadBounds.Any(r=>r.width>10 && z>=r.yMin-1&&z<=r.yMax+1);
                if(!nearJunction)surfaces.Box(2,new Vector3(x,.03f,z),new Vector3(.06f,.008f,2.1f));
            }
            if(!planOnly)surfaces.Save();
            // Conservative upper bound (collectors overlap spines); enough for regression monitoring.
            RoadFraction=RoadBounds.Sum(r=>r.width*r.height)/(192*64);
        }
        static int District(System.Random random){double v=random.NextDouble();return v<.45?0:v<.80?1:2;}
        void PackFrontage(BlockGenerator block,System.Random random,List<SpawnRule> rules,Transform parent,Rect parcel,bool north,int district,ref string last)
        {
            const float unit=BuildingFootprint.ModuleUnit;
            float gap=block.kit.buildingGapModules*unit,cursor=parcel.xMin;
            float previousEnd=float.NaN;int index=0;
            // Offset which end receives the leftover pocket without changing the amount of frontage gap.
            bool reverse=random.Next(2)==0;
            int rowId=rowSequence++;string previousFamily=null;
            while(cursor<parcel.xMax-unit)
            {
                var eligible=rules.Where(r=>{
                    var fp=block.buildings.Prefabs[r.PrefabId].GetComponent<BuildingFootprint>();var modules=fp.Modules;
                    float mw=modules.x*unit,md=modules.z*unit;
                    if(mw>parcel.xMax-cursor+.0001f||md>parcel.height||r.DistrictWeight(district)<=0)return false;
                    if(r.MaxPerChunk>0&&counts.TryGetValue(r.Id,out int count)&&count>=r.MaxPerChunk)return false;
                    float mx=reverse?parcel.xMax-(cursor-parcel.xMin)-mw:cursor;
                    var candidate=new Rect(mx,north?parcel.yMax-md:parcel.yMin,mw,md);
                    // Preserve a clear corridor for the existing low flight and its circling envelope.
                    if(fp.height>17&&candidate.xMin<16&&candidate.xMax>-16)return false;
                    if(fp.floors>=16&&candidate.xMin<42&&candidate.xMax>-42)return false;
                    return !ModuleBounds.Any(b=>b.Overlaps(candidate))&&!EmptyPlots.Any(b=>b.Overlaps(candidate));
                }).ToList();
                if(eligible.Count==0){cursor+=unit;previousEnd=float.NaN;previousFamily=null;continue;}
                bool empty=random.NextDouble()>block.buildingDensity|| (index>0&&random.NextDouble()<block.kit.emptyPlotChance);
                if(empty)
                {
                    float width=Mathf.Min(block.kit.emptyPlotModules*unit,parcel.xMax-cursor);
                    float x=reverse?parcel.xMax-(cursor-parcel.xMin)-width:cursor;
                    AddEmpty(new Rect(x,north?parcel.center.y:parcel.yMin,width,parcel.height/2));
                    cursor+=width;previousEnd=float.NaN;index++;continue;
                }
                string previousKey=last;
                Func<SpawnRule,double> weight=r=>r.Weight*r.DistrictWeight(district)*(r.Family==previousFamily?(r.Family=="House"?1.8:1.15):1)*(r.PrefabId==previousKey?.45:1);
                double total=eligible.Sum(weight);if(total<=0){cursor+=unit;continue;}
                double ticket=random.NextDouble()*total;var rule=eligible.Last();foreach(var option in eligible){ticket-=weight(option);if(ticket<0){rule=option;break;}}
                var prefab=block.buildings.Prefabs[rule.PrefabId];var fp=prefab.GetComponent<BuildingFootprint>();
                var modules=fp.Modules;float w=modules.x*unit,d=modules.z*unit;
                float xmin=reverse?parcel.xMax-(cursor-parcel.xMin)-w:cursor;
                var reserved=new Rect(xmin,north?parcel.yMax-d:parcel.yMin,w,d);
                var position=new Vector3(reserved.center.x,.01f,reserved.center.y);
                var actual=new Rect(position.x-fp.size.x/2,position.z-fp.size.y/2,fp.size.x,fp.size.y);
                if(ModuleBounds.Any(r=>r.Overlaps(reserved))||RoadBounds.Any(r=>r.Overlaps(reserved)))throw new InvalidOperationException("Module packing overlap");
                GameObject item=null;if(!planOnly){item=Place(prefab,parent,position,north?180:0);item.name="Modules "+modules.x+"x"+modules.y+"x"+modules.z+" / "+rule.Id+" / "+rule.PrefabId;}
                var view=prefab.GetComponent<BuildingView>();
                if(view && block.kit.facadePalette.Length>0)
                {
                    var color=block.kit.facadePalette[random.Next(block.kit.facadePalette.Length)];var props=new MaterialPropertyBlock();props.SetColor("_BaseColor",color);
                    if(item)foreach(var r in item.GetComponent<BuildingView>().facades)if(r)r.SetPropertyBlock(props);
                }
                if(!float.IsNaN(previousEnd))FrontageGaps.Add(cursor-previousEnd);
                previousEnd=cursor+w;cursor+=w+gap;index++;
                ModuleBounds.Add(reserved);BuildingBounds.Add(actual);BuildingCount++;last=rule.PrefabId;previousFamily=rule.Family;
                counts[rule.Id]=counts.TryGetValue(rule.Id,out int n)?n+1:1;
                Placements.Add(new Placement{id=rule.Id,family=rule.Family,district=district,floors=fp.floors,row=rowId,bounds=reserved});
            }
            if(cursor<parcel.xMax)
            {
                float width=parcel.xMax-cursor,x=reverse?parcel.xMin:cursor;
                AddEmpty(new Rect(x,north?parcel.center.y:parcel.yMin,width,parcel.height/2));
            }
        }
        void AddEmpty(Rect area)
        {
            // Split intentional empty frontage into modules where deep buildings occupy part of the opposite row.
            const float unit=BuildingFootprint.ModuleUnit;
            for(float x=area.xMin;x+unit<=area.xMax+.001f;x+=unit)
            {
                var strip=new Rect(x,area.yMin,unit,area.height);
                if(!ModuleBounds.Any(r=>r.Overlaps(strip))&&!EmptyPlots.Any(r=>r.Overlaps(strip)))EmptyPlots.Add(strip);
            }
        }
        void Road(SurfaceBatch surfaces,Rect r){RoadBounds.Add(r);surfaces.Box(0,new Vector3(r.center.x,.008f,r.center.y),new Vector3(r.width,.024f,r.height));}
        static void Crosswalk(SurfaceBatch surfaces,float x,float z)
        {
            for(int i=0;i<5;i++)surfaces.Box(2,new Vector3(x,.029f,z-1.1f+i*.55f),new Vector3(1.3f,.008f,.26f));
        }
        void OnDestroy(){foreach(var mesh in ownedMeshes)if(mesh){if(Application.isPlaying)Destroy(mesh);else DestroyImmediate(mesh);}}
        sealed class SurfaceBatch
        {
            readonly DenseCityLayout owner;readonly Material[] materials;
            readonly List<Vector3>[] vertices={new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
            readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>()};
            public SurfaceBatch(DenseCityLayout layout,params Material[] mats){owner=layout;materials=mats;}
            public void Box(int group,Vector3 center,Vector3 size)
            {
                // Horizontal top surface only: no hidden pavement faces or runtime colliders.
                int start=vertices[group].Count;float x=size.x/2,z=size.z/2,y=center.y+size.y/2;
                vertices[group].AddRange(new[]{new Vector3(center.x-x,y,center.z-z),new Vector3(center.x-x,y,center.z+z),new Vector3(center.x+x,y,center.z+z),new Vector3(center.x+x,y,center.z-z)});
                triangles[group].AddRange(new[]{start,start+1,start+2,start,start+2,start+3});
            }
            public void Save()
            {
                for(int i=0;i<3;i++)
                {
                    var mesh=new Mesh{name="City "+materials[i].name,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices[i]);mesh.SetTriangles(triangles[i],0);mesh.RecalculateNormals();mesh.RecalculateBounds();owner.ownedMeshes.Add(mesh);
                    var g=new GameObject(mesh.name);g.transform.SetParent(owner.transform,false);g.AddComponent<MeshFilter>().sharedMesh=mesh;
                    var renderer=g.AddComponent<MeshRenderer>();renderer.sharedMaterial=materials[i];renderer.shadowCastingMode=ShadowCastingMode.Off;
                }
            }
        }
    }
}
