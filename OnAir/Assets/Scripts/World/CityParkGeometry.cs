using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // One mesh per material per tile; no Update or individual bench/lamp GameObjects.
    public sealed class CityParkGeometry:MonoBehaviour
    {
        readonly List<Mesh> meshes=new List<Mesh>();readonly List<Material> materials=new List<Material>();
        static readonly HashSet<Material> lampMaterials=new HashSet<Material>();static Color emission;
        public static void RefreshLights(Color colour){if(colour==emission)return;emission=colour;foreach(var mat in lampMaterials)if(mat)mat.SetColor("_EmissionColor",emission);}
        readonly List<Vector3>[] vertices={new List<Vector3>(),new List<Vector3>(),new List<Vector3>(),new List<Vector3>()};
        readonly List<int>[] triangles={new List<int>(),new List<int>(),new List<int>(),new List<int>()};
        public int PathPieces{get;private set;}public int Benches{get;private set;}public int Lamps{get;private set;}
        public void Build(IReadOnlyList<WorldGreenSpaces.ParkPatch> parks,Rect tile,float origin,Material template)
        {
            foreach(var park in parks)
            {
                foreach(var path in park.paths)
                {
                    float x=Mathf.Max(path.xMin,tile.xMin),z=Mathf.Max(path.yMin,tile.yMin),w=Mathf.Min(path.xMax,tile.xMax)-x,d=Mathf.Min(path.yMax,tile.yMax)-z;
                    if(w<=0||d<=0)continue;Gravel(new Rect(x,z,w,d),origin);PathPieces++;
                }
                foreach(var f in park.furniture)
                {
                    if(!tile.Contains(f.position))continue;var at=new Vector3(f.position.x,0,f.position.y-origin);var q=Quaternion.Euler(0,f.yaw,0);
                    if(f.lamp){Box(2,new Vector3(0,1.4f,0),new Vector3(.18f,2.8f,.18f),at,q);Box(2,new Vector3(0,.15f,0),new Vector3(.45f,.3f,.45f),at,q);Box(3,new Vector3(0,2.85f,0),new Vector3(.5f,.5f,.5f),at,q);Box(2,new Vector3(0,3.2f,0),new Vector3(.75f,.18f,.75f),at,q);Lamps++;}
                    else{foreach(float side in new[]{-.65f,.65f})Box(2,new Vector3(side,.35f,0),new Vector3(.22f,.7f,.45f),at,q);Box(1,new Vector3(0,.68f,0),new Vector3(1.9f,.18f,.6f),at,q);Box(1,new Vector3(0,1.05f,.27f),new Vector3(1.9f,.6f,.15f),at,q);Benches++;}
                }
            }
            var colours=new[]{new Color(.675f,.66f,.59f),new Color(.445f,.35f,.27f),new Color(.36f,.42f,.41f),new Color(.93f,.79f,.48f)};
            for(int i=0;i<4;i++)if(vertices[i].Count>0)
            {
                var mesh=new Mesh{name="ENV-PARK-001 procedural "+i,indexFormat=IndexFormat.UInt32};mesh.SetVertices(vertices[i]);mesh.SetTriangles(triangles[i],0);mesh.RecalculateNormals();mesh.RecalculateBounds();meshes.Add(mesh);
                var mat=new Material(template){name="Park "+i,enableInstancing=true};mat.SetColor("_BaseColor",colours[i]);materials.Add(mat);
                var o=new GameObject(i==0?"Unequal-width park paths":i==1?"Park bench timber":i==2?"Park furniture frames":"Park lantern glass");o.transform.SetParent(transform,false);o.AddComponent<MeshFilter>().sharedMesh=mesh;var renderer=o.AddComponent<MeshRenderer>();renderer.sharedMaterial=mat;renderer.shadowCastingMode=i==0||i==3?ShadowCastingMode.Off:ShadowCastingMode.On;
                if(i==3){mat.EnableKeyword("_EMISSION");mat.SetColor("_EmissionColor",emission);lampMaterials.Add(mat);}
                vertices[i].Clear();triangles[i].Clear();
            }
        }
        void Box(int group,Vector3 centre,Vector3 size,Vector3 at,Quaternion rotation)
        {
            var p=new Vector3[8];for(int i=0;i<8;i++)p[i]=at+rotation*(centre+new Vector3((i&1)==0?-size.x/2:size.x/2,(i&2)==0?-size.y/2:size.y/2,(i&4)==0?-size.z/2:size.z/2));
            int[] faces={0,4,6,2,1,3,7,5,0,1,5,4,2,6,7,3,0,2,3,1,4,5,7,6};
            for(int f=0;f<6;f++){int n=vertices[group].Count;for(int k=0;k<4;k++)vertices[group].Add(p[faces[f*4+k]]);triangles[group].AddRange(new[]{n,n+1,n+2,n,n+2,n+3});}
        }
        static float Noise(int x,int z,int salt){unchecked{uint v=(uint)(x*73856093^z*19349663^salt);v^=v>>13;v*=1274126177;return (v&65535)/65535f;}}
        void Gravel(Rect area,float origin)
        {
            // Stable world-grid stones with irregular outlines and narrow grass gaps.
            // Clip to the reserved path and tile, so adjacent tiles meet without overlap.
            const float step=.65f;
            for(int iz=Mathf.FloorToInt(area.yMin/step);iz*step<area.yMax;iz++)for(int ix=Mathf.FloorToInt(area.xMin/step);ix*step<area.xMax;ix++)
            {
                float cx=(ix+.5f)*step+(Noise(ix,iz,1)-.5f)*.08f,cz=(iz+.5f)*step+(Noise(ix,iz,2)-.5f)*.08f;
                int n=vertices[0].Count;var ring=new List<Vector3>();
                for(int k=0;k<6;k++){float angle=k*Mathf.PI/3;float radius=.29f+Noise(ix,iz,10+k)*.075f;ring.Add(new Vector3(Mathf.Clamp(cx+Mathf.Cos(angle)*radius,area.xMin,area.xMax),.06f+Noise(ix,iz,3)*.025f,Mathf.Clamp(cz+Mathf.Sin(angle)*radius,area.yMin,area.yMax)-origin));}
                vertices[0].AddRange(ring);for(int k=1;k<5;k++)triangles[0].AddRange(new[]{n,n+k+1,n+k});
            }
        }
        void OnDestroy(){foreach(var m in meshes)if(m)Destroy(m);foreach(var m in materials)if(m){lampMaterials.Remove(m);Destroy(m);}}
    }
}
