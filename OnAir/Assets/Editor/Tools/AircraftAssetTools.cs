using System;
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using OnAir;

public static class AircraftAssetTools
{
    const string PrefabPath="Assets/Prefabs/Characters/LowWing.prefab";
    const string MeshRoot="Assets/Art/Meshes/Aircraft";
    const string MaterialRoot="Assets/Art/Materials/Aircraft";
    static readonly List<Vector3> verts = new List<Vector3>();
    static readonly List<int> tris = new List<int>();
    static Material red, cream, glass, dark, metal;
    static int index;
    static Material Material(string name, string hex)
    {
        ColorUtility.TryParseHtmlString("#"+hex,out var c);
        var m = new Material(Shader.Find("Universal Render Pipeline/Lit"));
        m.name = name; m.SetColor("_BaseColor",c); m.SetFloat("_Smoothness",.08f);
        AssetDatabase.CreateAsset(m,MaterialRoot+"/"+name+".mat"); return m;
    }
    // Face-local vertices intentionally preserve flat-shaded facets.
    static void Face(Vector3[] p, Vector3 center)
    {
        if(Vector3.Dot(Vector3.Cross(p[1]-p[0],p[2]-p[0]),p[0]-center)<0) Array.Reverse(p);
        int first=verts.Count; verts.AddRange(p);
        for(int i=1;i<p.Length-1;i++){tris.Add(first);tris.Add(first+i);tris.Add(first+i+1);}
    }
    static GameObject SavePart(Transform parent,string name,Material mat)
    {
        var mesh = new Mesh { name=name }; mesh.SetVertices(verts);mesh.SetTriangles(tris,0);mesh.RecalculateNormals();mesh.RecalculateBounds();
        AssetDatabase.CreateAsset(mesh,MeshRoot+"/"+(index++)+"-"+name+".asset");
        var go=new GameObject(name);go.transform.SetParent(parent,false);go.AddComponent<MeshFilter>().sharedMesh=mesh;go.AddComponent<MeshRenderer>().sharedMaterial=mat;
        verts.Clear();tris.Clear();return go;
    }
    static void Hull(Transform parent,string name,float[] z,float[] widths,float[] heights,float[] y,Material mat,int from=0,int to=8)
    {
        Vector3 P(int ring,int side){float a=side*Mathf.PI/4;return new Vector3(Mathf.Sin(a)*widths[ring],y[ring]+Mathf.Cos(a)*heights[ring],z[ring]);}
        var center=new Vector3(0,0,(z[0]+z[z.Length-1])/2);
        for(int r=0;r<z.Length-1;r++)for(int s=from;s<to;s++)Face(new[]{P(r,s),P(r,s+1),P(r+1,s+1),P(r+1,s)},center);
        if(from==0&&to==8)foreach(int r in new[]{0,z.Length-1}){var face=new Vector3[8];for(int s=0;s<8;s++)face[s]=P(r,s);Face(face,center);}
        SavePart(parent,name,mat);
    }
    static void Prism(Transform parent,string name,Vector3[] polygon,Vector3 depth,Material mat)
    {
        var a=new Vector3[polygon.Length];var b=new Vector3[polygon.Length];Vector3 center=Vector3.zero;
        for(int i=0;i<a.Length;i++){a[i]=polygon[i]-depth/2;b[i]=polygon[i]+depth/2;center+=polygon[i]/a.Length;}
        Face((Vector3[])a.Clone(),center);Face((Vector3[])b.Clone(),center);
        for(int i=0;i<a.Length;i++){int j=(i+1)%a.Length;Face(new[]{a[i],a[j],b[j],b[i]},center);}
        SavePart(parent,name,mat);
    }
    static void Box(Transform p,string n,Vector3 pos,Vector3 size,Material m)
    {
        Prism(p,n,new[]{pos+new Vector3(-size.x/2,0,-size.z/2),pos+new Vector3(size.x/2,0,-size.z/2),pos+new Vector3(size.x/2,0,size.z/2),pos+new Vector3(-size.x/2,0,size.z/2)},Vector3.up*size.y,m);
    }
    [MenuItem("OnAir/Create low-poly aircraft")]
    public static void Generate()
    {
        if(File.Exists(PrefabPath))throw new InvalidOperationException("Aircraft already exists; preserving edited model.");
        Directory.CreateDirectory(Path.GetDirectoryName(PrefabPath));Directory.CreateDirectory(MeshRoot);Directory.CreateDirectory(MaterialRoot);AssetDatabase.Refresh();index=0;verts.Clear();tris.Clear();
        red=Material("Terracotta red","C45C48");cream=Material("Warm ivory","E6D8B5");glass=Material("Slate canopy","567887");dark=Material("Rubber and frame","303D43");metal=Material("Hub metal","9CA49C");
        var plane=new GameObject("LowWing");
        float[] z={-1.35f,-.9f,-.35f,.4f,.95f,1.2f};float[] w={.06f,.15f,.28f,.31f,.23f,.15f};float[] h={.09f,.16f,.24f,.26f,.21f,.15f};float[] y={.04f,0,0,0,0,0};
        // Top quarters are red, lower half ivory. Both colors share identical hull rings.
        Hull(plane.transform,"Upper fuselage right",z,w,h,y,red,0,2);Hull(plane.transform,"Belly",z,w,h,y,cream,2,6);Hull(plane.transform,"Upper fuselage left",z,w,h,y,red,6,8);
        Hull(plane.transform,"Nose",new[]{1.19f,1.32f},new[]{.15f,.08f},new[]{.15f,.08f},new[]{0f,0f},cream);
        Hull(plane.transform,"Tail cap",new[]{-1.4f,-1.35f},new[]{.005f,.06f},new[]{.005f,.09f},new[]{.04f,.04f},red);
        Hull(plane.transform,"Canopy",new[]{-.55f,-.3f,.3f,.52f},new[]{.12f,.225f,.22f,.09f},new[]{.07f,.23f,.22f,.03f},new[]{.2f,.23f,.24f,.2f},glass);
        foreach(int s in new[]{-1,1})
        {
            Prism(plane.transform,"Wing "+s,new[]{new Vector3(s*.18f,-.1f,.45f),new Vector3(s*1.5f,-.035f,.16f),new Vector3(s*1.65f,-.02f,-.28f),new Vector3(s*.18f,-.1f,-.4f)},Vector3.up*.08f,cream);
            Prism(plane.transform,"Red wingtip "+s,new[]{new Vector3(s*1.5f,-.035f,.16f),new Vector3(s*1.76f,-.02f,.09f),new Vector3(s*1.84f,-.01f,-.24f),new Vector3(s*1.65f,-.02f,-.28f)},Vector3.up*.085f,red);
            Prism(plane.transform,"Tailplane "+s,new[]{new Vector3(0,.12f,-.86f),new Vector3(s*.65f,.14f,-1.06f),new Vector3(s*.67f,.14f,-1.34f),new Vector3(0,.12f,-1.31f)},Vector3.up*.055f,cream);
            Box(plane.transform,"Landing strut "+s,new Vector3(s*.45f,-.33f,.25f),new Vector3(.05f,.4f,.055f),metal);
            var wheel=new GameObject("Wheel "+s);wheel.transform.SetParent(plane.transform,false);wheel.transform.localPosition=new Vector3(s*.45f,-.52f,.25f);wheel.transform.localRotation=Quaternion.Euler(0,90,0);
            Hull(wheel.transform,"Tyre "+s,new[]{-.06f,.06f},new[]{.13f,.13f},new[]{.13f,.13f},new[]{0f,0f},dark);
        }
        Prism(plane.transform,"Vertical tail",new[]{new Vector3(0,.12f,-.8f),new Vector3(0,.73f,-1.14f),new Vector3(0,.75f,-1.35f),new Vector3(0,.1f,-1.35f)},Vector3.right*.075f,red);
        var prop=new GameObject("Propeller");prop.transform.SetParent(plane.transform,false);prop.transform.localPosition=new Vector3(0,0,1.36f);
        Box(prop.transform,"Two blade prop",Vector3.zero,new Vector3(.075f,1.05f,.045f),dark);
        foreach(int s in new[]{-1,1})Box(prop.transform,"Blade tip "+s,new Vector3(0,s*.47f,0),new Vector3(.08f,.12f,.05f),cream);
        Hull(prop.transform,"Spinner",new[]{-.04f,.13f},new[]{.09f,.01f},new[]{.09f,.01f},new[]{0f,0f},metal);
        PrefabUtility.SaveAsPrefabAsset(plane,PrefabPath);UnityEngine.Object.DestroyImmediate(plane);AssetDatabase.SaveAssets();
        Debug.Log("AIRCRAFT_GENERATED");
    }
}
