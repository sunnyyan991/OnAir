using System;
using System.IO;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEditor;
using UnityEditor.SceneManagement;
using OnAir.Prototype;

public static class CityKitBuilder
{
    const string Root="Assets/OnAirPrototype/CityBlock";
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
            t.SetPixels(pixels);t.Apply();string path=Root+"/Art/"+name+".png";File.WriteAllBytes(path,t.EncodeToPNG());UnityEngine.Object.DestroyImmediate(t);AssetDatabase.ImportAsset(path);
            var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=true;importer.SaveAndReimport();
            m.SetTexture("_BaseMap",AssetDatabase.LoadAssetAtPath<Texture2D>(path));
        }
        AssetDatabase.CreateAsset(m,Root+"/Art/"+name+".mat");return m;
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
            AssetDatabase.CreateAsset(mesh,Root+"/Meshes/"+name+"_"+(meshIndex++)+".asset");
            var child=new GameObject(group.Key.name);child.transform.SetParent(go.transform,false);child.AddComponent<MeshFilter>().sharedMesh=mesh;child.AddComponent<MeshRenderer>().sharedMaterial=group.Key;
        }
        foreach(var obj in old)if(obj)UnityEngine.Object.DestroyImmediate(obj);
        var result=PrefabUtility.SaveAsPrefabAsset(go,Root+"/Prefabs/"+name+".prefab");UnityEngine.Object.DestroyImmediate(go);return result;
    }
    public static void Generate()
    {
        if(File.Exists(Root+"/Scenes/CityBlockPreview.unity"))throw new InvalidOperationException("City preview already exists; do not overwrite edited art.");
        foreach(string folder in new[]{"Art","Meshes","Prefabs","Data","Scenes"})Directory.CreateDirectory(Root+"/"+folder);AssetDatabase.Refresh();
        stone=Mat("Pavement","BDB99F",true);asphalt=Mat("Asphalt","535D61",true);glass=Mat("Window glass","557D89");trim=Mat("Limestone trim","D0C8AF");roof=Mat("Roof","707B7D",true);
        wood=Mat("Warm wood","896544");metal=Mat("Iron","29363E");leaf=Mat("Foliage","718360",true);leafLight=Mat("Foliage light","94A37B",true);leafDark=Mat("Foliage dark","3F5744");grass=Mat("Grass","85906C",true);mark=Mat("Road paint","DCD8C3");red=Mat("Plane red","C85A48");
        var kit=ScriptableObject.CreateInstance<CityBlockKit>();
        kit.roadStraight=Road("Road straight",5);kit.roadCorner=Road("Road corner",3);kit.roadT=Road("Road T",11);kit.roadCross=Road("Road cross",15);
        var pavement=new GameObject("Pavement");B(pavement.transform,"Lot",0,-.13f,0,8,.32f,8,stone);kit.pavement=Save(pavement,"Pavement");
        var park=new GameObject("Park");B(park.transform,"Soil",0,-.14f,0,8,.3f,8,grass);B(park.transform,"Path",0,.045f,0,1.25f,.1f,8,stone);B(park.transform,"Path",0,.04f,0,8,.1f,1.25f,stone);kit.park=Save(park,"Park cell");
        kit.tree=Tree("Broadleaf",false);kit.treeTall=Tree("Tall tree",true);kit.bench=Bench();kit.planter=Planter();kit.lamp=Lamp();
        var hedge=new GameObject("Hedge");B(hedge.transform,"Hedge",0,.7f,0,2.5f,1.3f,.7f,leaf);kit.hedge=Save(hedge,"Hedge");
        var catalog=ScriptableObject.CreateInstance<BuildingCatalog>();string[] keys={"apartment_03","office_05","corner_shop","apartment_04","house_02","mixed_03"};
        catalog.entries=new BuildingCatalog.Entry[6];
        int[] floors={3,5,2,4,2,3};string[] walls={"B9B397","7A9098","A5745C","B5BCB5","D3C5A4","929775"};
        for(int i=0;i<6;i++)catalog.entries[i]=new BuildingCatalog.Entry{id=keys[i],prefab=Building(keys[i],floors[i],Mat(keys[i]+" wall",walls[i],true),i)};
        AssetDatabase.CreateAsset(catalog,Root+"/Data/CityBuildingCatalog.asset");kit.buildings=catalog;
        string csv="##var,id,prefab_id,biomes,weather,periods,weight,min_scale,max_scale\n##type,string,string,\"(list#sep=|),Biome\",\"(list#sep=|),Weather\",\"(list#sep=|),DayPeriod\",float,float,float\n##,数字字符串编号,城市预制体编号,City 城市；* 不限,天气；* 不限,时段；* 不限,相对权重,静止街区保持原比例,静止街区保持原比例\n";
        for(int i=0;i<6;i++)csv+=","+(i+7)+","+keys[i]+",City,*,*,"+(i==2?4:3)+",1,1\n";
        File.WriteAllText(Root+"/Data/city_buildings.csv",csv,new System.Text.UTF8Encoding(true));AssetDatabase.Refresh();kit.rules=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/city_buildings.csv");
        AssetDatabase.CreateAsset(kit,Root+"/Data/CityBlockKit.asset");
        AssetDatabase.SaveAssets();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        kit=AssetDatabase.LoadAssetAtPath<CityBlockKit>(Root+"/Data/CityBlockKit.asset");
        var source=(UniversalRenderPipelineAsset)(QualitySettings.renderPipeline?QualitySettings.renderPipeline:GraphicsSettings.defaultRenderPipeline);
        var pipeline=UnityEngine.Object.Instantiate(source);pipeline.shadowDistance=180;pipeline.shadowCascadeCount=4;pipeline.useSRPBatcher=false;
        var pipelineSettings=new SerializedObject(pipeline);pipelineSettings.FindProperty("m_MainLightShadowmapResolution").intValue=4096;pipelineSettings.ApplyModifiedPropertiesWithoutUndo();
        AssetDatabase.CreateAsset(pipeline,Root+"/Art/CityPreviewPipeline.asset");
        var rendering=new GameObject("City preview rendering");rendering.SetActive(false);rendering.AddComponent<CityPreviewRendering>().pipeline=pipeline;rendering.SetActive(true);
        RenderSettings.ambientMode=AmbientMode.Trilight;RenderSettings.ambientSkyColor=C("BDD0DA");RenderSettings.ambientEquatorColor=C("8F9995");RenderSettings.ambientGroundColor=C("6E6A5D");RenderSettings.ambientIntensity=.7f;RenderSettings.skybox=null;
        var camera=new GameObject("Main Camera").AddComponent<Camera>();camera.tag="MainCamera";camera.orthographic=true;camera.orthographicSize=44;camera.transform.rotation=Quaternion.Euler(45,45,0);camera.transform.position=new Vector3(0,2,0)-camera.transform.forward*85;camera.nearClipPlane=.1f;camera.farClipPlane=220;camera.clearFlags=CameraClearFlags.SolidColor;camera.backgroundColor=C("26333F");camera.allowHDR=true;
        var light=new GameObject("Afternoon sun").AddComponent<Light>();light.type=LightType.Directional;light.color=C("FFE4BA");light.intensity=1.2f;light.shadows=LightShadows.Soft;light.shadowBias=.03f;light.shadowNormalBias=.25f;light.transform.rotation=Quaternion.Euler(48,-35,0);
        var baseObj=new GameObject("Block foundation");B(baseObj.transform,"Earth",0,-1.1f,0,64,1.8f,64,Mat("Foundation","606D66",true));
        var layout=new GameObject("City block layout").AddComponent<CityBlockLayout>();layout.kit=kit;layout.Rebuild();
        var plane=(GameObject)PrefabUtility.InstantiatePrefab(AssetDatabase.LoadAssetAtPath<GameObject>("Assets/OnAirPrototype/Prefabs/Characters/Plane.prefab"));plane.transform.position=new Vector3(5,21,-2);plane.transform.localScale=Vector3.one*2.7f;
        plane.transform.GetChild(0).rotation=camera.transform.rotation;
        var planeRenderer=plane.GetComponentInChildren<SpriteRenderer>();planeRenderer.sprite=PlaneSprite();
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/CityBlockPreview.unity");AssetDatabase.SaveAssets();
        Validate();Debug.Log("CITY_KIT_GENERATED");
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
    static GameObject Road(string name,int mask)
    {
        var g=new GameObject(name);var t=g.transform;B(t,"Road bed",0,-.21f,0,8,.4f,8,asphalt);
        // Four 1 m footpath strips stop wherever a connecting carriageway enters.
        for(int side=0;side<4;side++)
        {
            if((mask&(1<<side))!=0)continue;
            var q=Quaternion.Euler(0,side*90,0);var slab=Box(t,"Sidewalk",q*new Vector3(0,.06f,3.5f),new Vector3(8,.25f,1),stone);slab.transform.localRotation=q;
            for(int joint=-3;joint<=3;joint++){var jointObj=Box(t,"Paving seam",q*new Vector3(joint,.191f,3.5f),new Vector3(.025f,.008f,.95f),roof);jointObj.transform.localRotation=q;}
        }
        for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)B(t,"Corner paving",x*3.5f,.06f,z*3.5f,1,.25f,1,stone);
        int count=0;for(int k=0;k<4;k++)if((mask&(1<<k))!=0)count++;
        if(mask==5){for(int z=-3;z<=3;z+=2)B(t,"Lane dash",0,.001f,z,.12f,.015f,.9f,mark);}
        else for(int side=0;side<4;side++)if((mask&(1<<side))!=0)
        {
            var q=Quaternion.Euler(0,side*90,0);
            if(count>=3)for(int stripe=-2;stripe<=2;stripe++){var p=Box(t,"Crosswalk",q*new Vector3(stripe*.9f,.003f,2.8f),new Vector3(.48f,.02f,1.25f),mark);p.transform.localRotation=q;}
            else{var p=Box(t,"Lane dash",q*new Vector3(0,.003f,2.8f),new Vector3(.12f,.02f,1.1f),mark);p.transform.localRotation=q;}
        }
        return Save(g,name);
    }
    static GameObject Tree(string name,bool tall)
    {
        var g=new GameObject(name);B(g.transform,"Trunk",0,1.3f,0,.35f,2.6f,.35f,wood);
        var rng=new System.Random(tall?831:320);
        for(int i=0;i<16;i++){float x=(float)(rng.NextDouble()-.5)*1.9f,z=(float)(rng.NextDouble()-.5)*1.9f,y=2.6f+(float)rng.NextDouble()*(tall?2.5f:1.1f);float size=.8f+(float)rng.NextDouble()*.6f;B(g.transform,"Leaf cluster",x,y,z,size,tall?1.1f:size,size,i%3==0?leafLight:i%3==1?leaf:leafDark);}
        return Save(g,name);
    }
    static GameObject Bench()
    {
        var g=new GameObject("Bench");for(int i=0;i<4;i++)B(g.transform,"Seat slat",0,.65f,i*.16f-.24f,1.8f,.12f,.12f,wood);
        for(int i=0;i<3;i++)B(g.transform,"Back slat",0,.9f+i*.18f,.34f,1.8f,.13f,.1f,wood);
        B(g.transform,"Leg",-.65f,.32f,0,.12f,.65f,.6f,metal);B(g.transform,"Leg",.65f,.32f,0,.12f,.65f,.6f,metal);return Save(g,"Bench");
    }
    static GameObject Planter()
    {
        var g=new GameObject("Planter");B(g.transform,"Box",0,.3f,0,.8f,.6f,.8f,trim);B(g.transform,"Plant",0,.8f,0,.85f,.6f,.85f,leaf);B(g.transform,"Flower",.2f,1.12f,.1f,.18f,.1f,.18f,red);return Save(g,"Planter");
    }
    static GameObject Lamp()
    {
        var g=new GameObject("Lamp");B(g.transform,"Base",0,.18f,0,.35f,.35f,.35f,metal);B(g.transform,"Pole",0,2.4f,0,.12f,4.6f,.12f,metal);B(g.transform,"Arm",.35f,4.7f,0,.8f,.12f,.12f,metal);B(g.transform,"Lantern",.7f,4.42f,0,.45f,.45f,.45f,trim);B(g.transform,"Cap",.7f,4.7f,0,.6f,.15f,.6f,metal);return Save(g,"Street lamp");
    }
    static Sprite PlaneSprite()
    {
        var texture=new Texture2D(64,64,TextureFormat.RGBA32,false);var pixels=new Color[4096];texture.SetPixels(pixels);
        void R(int x,int y,int w,int h,Color c){for(int xx=x;xx<x+w;xx++)for(int yy=y;yy<y+h;yy++)texture.SetPixel(xx,yy,c);}
        R(5,25,54,7,C("29363E"));R(7,26,50,5,C("EDE5CD"));R(7,26,9,5,C("C85A48"));R(48,26,9,5,C("C85A48"));
        R(27,9,10,43,C("29363E"));R(29,11,6,40,C("EDE5CD"));R(29,40,6,11,C("C85A48"));R(30,51,4,6,C("C85A48"));R(30,33,4,7,C("557D89"));R(20,11,24,4,C("C85A48"));
        texture.Apply();string path=Root+"/Art/Plane-red-white.png";File.WriteAllBytes(path,texture.EncodeToPNG());UnityEngine.Object.DestroyImmediate(texture);AssetDatabase.ImportAsset(path);
        var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;importer.spritePixelsPerUnit=20;importer.filterMode=FilterMode.Point;importer.textureCompression=TextureImporterCompression.Uncompressed;importer.mipmapEnabled=false;importer.SaveAndReimport();return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    [MenuItem("OnAir/Validate city block")]
    public static void Validate()
    {
        var layout=UnityEngine.Object.FindObjectOfType<CityBlockLayout>();if(!layout)throw new Exception("Open CityBlockPreview first.");
        var buildings=layout.generated.GetComponentsInChildren<BuildingFootprint>();if(buildings.Length<12)throw new Exception("Missing buildings.");
        foreach(var b in buildings)if(b.size.x>7.95f||b.size.y>7.95f)throw new Exception("Lot overflow.");
        var visited=new HashSet<Vector2Int>();var queue=new Queue<Vector2Int>();queue.Enqueue(Vector2Int.zero);
        while(queue.Count>0){var p=queue.Dequeue();if(!CityBlockLayout.IsRoad(p.x,p.y)||!visited.Add(p))continue;foreach(var d in new[]{Vector2Int.up,Vector2Int.down,Vector2Int.left,Vector2Int.right})queue.Enqueue(p+d);}
        int roads=0;for(int x=0;x<8;x++)for(int z=0;z<8;z++)if(CityBlockLayout.IsRoad(x,z))roads++;
        if(visited.Count!=roads)throw new Exception("Road network disconnected.");
        Debug.Log("CITY_BLOCK_VALIDATED: "+roads+" connected road tiles; "+buildings.Length+" buildings; footprint and entrance checks passed.");
    }
    public static void Preview()
    {
        EditorSceneManager.OpenScene(Root+"/Scenes/CityBlockPreview.unity");
        SessionState.SetBool("CityPreview",true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool("CityPreview",false))EditorApplication.update+=Capture;}
    static void Capture()
    {
        if(!EditorApplication.isPlaying||Time.time<3)return;
        EditorApplication.update-=Capture;SessionState.SetBool("CityPreview",false);
        try{
            Validate();
            var layout=UnityEngine.Object.FindObjectOfType<CityBlockLayout>();
            var before=layout.generated.GetComponentsInChildren<BuildingFootprint>().Select(b=>b.gameObject.name).ToArray();
            layout.Rebuild();var after=layout.generated.GetComponentsInChildren<BuildingFootprint>().Select(b=>b.gameObject.name).ToArray();
            if(!before.SequenceEqual(after))throw new Exception("Seed did not reproduce layout.");
            Validate();var camera=Camera.main;var rt=new RenderTexture(1440,1080,24);camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;
            var texture=new Texture2D(1440,1080,TextureFormat.RGB24,false);texture.ReadPixels(new Rect(0,0,1440,1080),0,0);texture.Apply();File.WriteAllBytes("city-block-preview.png",texture.EncodeToPNG());
            camera.targetTexture=null;RenderTexture.active=null;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);Debug.Log("CITY_PREVIEW_RENDERED");EditorApplication.Exit(0);
        }catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
