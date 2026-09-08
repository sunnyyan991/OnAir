using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEditor;
using OnAir;

public static class BuildingAssetTools
{
    const string Root="Assets";
    static Material stone,asphalt,glass,trim,roof,wood,metal,leaf,leafLight,leafDark,grass,mark,red;
    static int meshIndex;
    static Color C(string s){ColorUtility.TryParseHtmlString("#"+s,out var c);return c;}
    static Material Mat(string name,string color,bool noise=false)
    {
        var m=new Material(Shader.Find("Universal Render Pipeline/Lit"));m.SetColor("_BaseColor",C(color));m.SetFloat("_Smoothness",.12f);m.enableInstancing=false;
        if(noise)
        {
            var t=new Texture2D(64,64,TextureFormat.RGB24,false);var random=new System.Random(301);var pixels=new Color[4096];
            for(int i=0;i<pixels.Length;i++){float v=random.NextDouble()<.08?.86f:1f;pixels[i]=new Color(v,v,v);}
            t.SetPixels(pixels);t.Apply();string path=AssetDatabase.GenerateUniqueAssetPath(Root+"/Art/Sprites/Environment/"+name+".png");File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        AssetDatabase.CreateAsset(m,AssetDatabase.GenerateUniqueAssetPath(Root+"/Art/Materials/Shared/"+name+".mat"));return m;
    }
    static GameObject Box(Transform parent,string name,Vector3 pos,Vector3 size,Material mat)
    {
        var go=GameObject.CreatePrimitive(PrimitiveType.Cube);go.name=name;go.transform.SetParent(parent,false);go.transform.localPosition=pos;go.transform.localScale=size;
        UnityEngine.Object.DestroyImmediate(go.GetComponent<Collider>());go.GetComponent<MeshRenderer>().sharedMaterial=mat;return go;
    }
    static void B(Transform p,string n,float x,float y,float z,float w,float h,float d,Material m)=>Box(p,n,new Vector3(x,y,z),new Vector3(w,h,d),m);
    // Merge the authored pieces by material; prefab root and ground anchor remain independent.
    static GameObject Save(GameObject go,string name)
    {
        var groups=go.GetComponentsInChildren<MeshRenderer>().GroupBy(r=>r.sharedMaterial).ToArray();
        var old=go.GetComponentsInChildren<MeshFilter>().Select(f=>f.gameObject).Distinct().ToArray();
        foreach(var group in groups)
        {
            var combines=group.Select(r=>new CombineInstance{mesh=r.GetComponent<MeshFilter>().sharedMesh,transform=go.transform.worldToLocalMatrix*r.transform.localToWorldMatrix}).ToArray();
            var mesh=new Mesh{name=name+"_"+group.Key.name,indexFormat=IndexFormat.UInt32};mesh.CombineMeshes(combines,true,true);
            AssetDatabase.CreateAsset(mesh,AssetDatabase.GenerateUniqueAssetPath(Root+"/Art/Meshes/"+name+"_"+(meshIndex++)+".asset"));
            var child=new GameObject(group.Key.name);child.transform.SetParent(go.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var obj in old)if(obj)UnityEngine.Object.DestroyImmediate(obj);
        if(go.GetComponent<BuildingFootprint>()){var view=go.AddComponent<BuildingView>();view.windows=go.GetComponentsInChildren<MeshRenderer>().Where(r=>r.sharedMaterial==glass).ToArray();}
        var result=PrefabUtility.SaveAsPrefabAsset(go,AssetDatabase.GenerateUniqueAssetPath(Root+"/Prefabs/Buildings/"+name+".prefab"));UnityEngine.Object.DestroyImmediate(go);return result;
    }
    [MenuItem("OnAir/Tools/Create sample building prefabs")]
    public static void Generate()
    {
        foreach(string folder in new[]{"Art/Sprites/Environment","Art/Materials/Shared","Art/Meshes","Prefabs/Buildings"})Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        glass=Mat("Window glass","557D89");trim=Mat("Limestone trim","D0C8AF");roof=Mat("Roof","707B7D",true);
        wood=Mat("Warm wood","896544");metal=Mat("Iron","29363E");leafDark=Mat("Foliage dark","3F5744");mark=Mat("Road paint","DCD8C3");
        string[] keys={"apartment_03","office_05","corner_shop","apartment_04","house_02","mixed_03"};
        int[] floors={3,5,2,4,2,3};string[] walls={"B9B397","7A9098","A5745C","B5BCB5","D3C5A4","929775"};
        for(int i=0;i<keys.Length;i++)Building(keys[i],floors[i],Mat(keys[i]+" wall",walls[i],true),i);
        AssetDatabase.SaveAssets();Debug.Log("Created six independent building prefabs; assign their catalog keys and table rows to use them.");
    }
    static GameObject Building(string name,int floors,Material wall,int kind)
    {
        var g=new GameObject(name);var t=g.transform;var footprint=g.AddComponent<BuildingFootprint>();footprint.floors=floors;footprint.size=new Vector2(6.8f,6.8f);footprint.cornerShop=kind==2;
        float h=floors*3f;B(t,"Foundation",0,.16f,0,6.8f,.32f,6.8f,trim);B(t,"Walls",0,h/2+.32f,0,6.2f,h,6.2f,wall);
        for(int floor=0;floor<floors;floor++)
        {
            float y=floor*3+1.9f;
            B(t,"Floor band",0,floor*3+.4f,0,6.35f,.17f,6.35f,trim);
            for(int face=0;face<4;face++)for(int col=-1;col<=1;col++)
            {
                if(floor==0&&col==0&&face==0)continue;
                var q=Quaternion.Euler(0,90*face,0);var p=q*new Vector3(col*1.85f,y,-3.14f);
                var frame=Box(t,"Window frame",p,new Vector3(1.28f,1.52f,.15f),trim);frame.transform.localRotation=q;
                var window=Box(t,"Window",p+q*new Vector3(0,0,-.09f),new Vector3(1.05f,1.27f,.08f),glass);window.transform.localRotation=q;
                var mullion=Box(t,"Window mullion",p+q*new Vector3(0,0,-.15f),new Vector3(.07f,1.28f,.06f),metal);mullion.transform.localRotation=q;
            }
            if((kind==0||kind==3)&&floor>0)
            {
                B(t,"Balcony slab",0,y-.85f,-3.35f,5.6f,.2f,.75f,trim);B(t,"Balcony rail",0,y-.17f,-3.64f,5.6f,.08f,.08f,metal);
                for(int bar=-6;bar<=6;bar++)B(t,"Railing",bar*.43f,y-.5f,-3.64f,.055f,.7f,.06f,metal);
            }
        }
        B(t,"Entrance surround",0,1.45f,-3.16f,1.65f,2.6f,.22f,trim);B(t,"Entrance",0,1.4f,-3.31f,1.25f,2.35f,.08f,kind==4?wood:glass);
        B(t,"Door handle",.4f,1.35f,-3.37f,.08f,.35f,.06f,mark);B(t,"Entry step",0,.2f,-3.45f,2,.3f,.6f,trim);
        if(kind==2||kind==5){B(t,"Awning",0,2.95f,-3.35f,5.9f,.25f,1.2f,leafDark);B(t,"Shop sign",0,3.5f,-3.16f,4.8f,.55f,.17f,wood);B(t,"Sign inset",0,3.5f,-3.27f,4,.2f,.06f,trim);}
        if(kind==4)
        {
            var mesh=new Mesh();mesh.vertices=new[]{new Vector3(-3.4f,h+.35f,-3.4f),new Vector3(3.4f,h+.35f,-3.4f),new Vector3(0,h+2.7f,-3.4f),new Vector3(-3.4f,h+.35f,3.4f),new Vector3(3.4f,h+.35f,3.4f),new Vector3(0,h+2.7f,3.4f)};
            mesh.triangles=new[]{0,2,1,3,4,5,0,3,5,0,5,2,2,5,4,2,4,1};mesh.RecalculateNormals();
            var r=new GameObject("Gabled roof");r.transform.SetParent(t,false);r.AddComponent<MeshFilter>().sharedMesh=mesh;r.AddComponent<MeshRenderer>().sharedMaterial=wood;
            for(int tile=-3;tile<=3;tile++)B(t,"Ridge tile",0,h+2.73f,tile, .25f,.18f,.9f,wood);
        }
        else
        {
            B(t,"Roof surface",0,h+.4f,0,6.25f,.25f,6.25f,roof);
            B(t,"Parapet",0,h+.8f,-3.04f,6.4f,.8f,.24f,trim);B(t,"Parapet",0,h+.8f,3.04f,6.4f,.8f,.24f,trim);
            B(t,"Parapet",-3.04f,h+.8f,0,.24f,.8f,6.4f,trim);B(t,"Parapet",3.04f,h+.8f,0,.24f,.8f,6.4f,trim);
            B(t,"Water tank",1.25f,h+1.5f,1.1f,1.3f,1.8f,1.3f,trim);B(t,"AC unit",-1.3f,h+.95f,1.2f,1.3f,.75f,.9f,roof);
            for(int v=0;v<5;v++)B(t,"AC grille",-1.3f,h+.73f+v*.09f,.72f,1,.035f,.04f,metal);
        }
        var renderers=g.GetComponentsInChildren<Renderer>();var bounds=renderers[0].bounds;foreach(var renderer in renderers)bounds.Encapsulate(renderer.bounds);
        footprint.size=new Vector2(Mathf.Max(Mathf.Abs(bounds.min.x),Mathf.Abs(bounds.max.x))*2,Mathf.Max(Mathf.Abs(bounds.min.z),Mathf.Abs(bounds.max.z))*2);
        return Save(g,name);
    }
}
