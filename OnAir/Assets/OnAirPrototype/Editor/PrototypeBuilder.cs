using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using OnAir.Prototype;

public static class PrototypeBuilder
{
    const string Root = "Assets/OnAirPrototype";
    public const string Header = "id,prefab_id,biomes,weather,periods,weight,min_scale,max_scale";
    [MenuItem("OnAir/Create prototype assets (new project only)")]
    public static void Generate()
    {
        if (File.Exists(Root + "/Scenes/FlightPrototype.unity")) throw new InvalidOperationException("Prototype already exists. Existing prefabs and scene will not be overwritten.");
        foreach (string folder in new[]{"Art","Prefabs/Buildings","Prefabs/Characters","Data","Scenes"}) Directory.CreateDirectory(Root+"/"+folder);
        AssetDatabase.Refresh();
        var ground = new Material(Shader.Find("Universal Render Pipeline/Unlit")); ground.SetColor("_BaseColor",new Color(.32f,.46f,.41f)); AssetDatabase.CreateAsset(ground,Root+"/Art/Ground.mat");
        var sprites = new Material(Shader.Find("Sprites/Default")); AssetDatabase.CreateAsset(sprites,Root+"/Art/PixelSprites.mat");
        var catalog = ScriptableObject.CreateInstance<BuildingCatalog>();
        string[] names = {"townhouse","tower","warehouse","lighthouse","adobe","night_kiosk"};
        Color[] colors = {Hex("DA956F"),Hex("719BAB"),Hex("B9AE87"),Hex("E0D8BE"),Hex("D3AA74"),Hex("988AC4")};
        catalog.entries = new BuildingCatalog.Entry[names.Length];
        for (int i=0;i<names.Length;i++)
        {
            var sprite = MakeSprite(names[i],i,colors[i]);
            var root = new GameObject(names[i]);
            AddSprite(root.transform,"Visual",sprite,sprites);
            var prefab = PrefabUtility.SaveAsPrefabAsset(root,Root+"/Prefabs/Buildings/"+names[i]+".prefab");
            catalog.entries[i]=new BuildingCatalog.Entry{id=names[i],prefab=prefab}; UnityEngine.Object.DestroyImmediate(root);
        }
        AssetDatabase.CreateAsset(catalog,Root+"/Data/BuildingCatalog.asset");
        var planeRoot = new GameObject("Plane");
        var planeVisual = AddSprite(planeRoot.transform,"Visual",MakeSprite("plane",6,Hex("EDE3C8")),sprites);
        planeRoot.AddComponent<PlaneView>().Configure(planeVisual);
        var planePrefab=PrefabUtility.SaveAsPrefabAsset(planeRoot,Root+"/Prefabs/Characters/Plane.prefab"); UnityEngine.Object.DestroyImmediate(planeRoot);
        string csv="##var,id,prefab_id,biomes,weather,periods,weight,min_scale,max_scale\n##type,string,string,\"(list#sep=|),Biome\",\"(list#sep=|),Weather\",\"(list#sep=|),DayPeriod\",float,float,float\n##,数字编号；string；唯一；保留前导零,预制体查找键；对应 BuildingCatalog,City 城市 / Coast 海岸 / Desert 沙漠；| 多选；* 不限,Clear 晴 / Rain 雨 / Snow 雪；| 多选；* 不限,Dawn 清晨 / Day 白天 / Dusk 黄昏 / Night 夜晚；| 多选；* 不限,相对权重；0 不生成,缩放下限；大于 0,缩放上限；不小于下限；不超过 1.5\n,1,townhouse,City|Coast,*,*,5,0.85,1.1\n,2,tower,City,Clear|Rain,Day|Dusk|Night,2,0.9,1.15\n,3,warehouse,City|Coast,*,*,3,0.85,1.05\n,4,lighthouse,Coast,*,*,4,0.9,1.15\n,5,adobe,Desert,Clear|Rain,*,5,0.85,1.15\n,6,night_kiosk,City|Coast,Clear|Rain,Dusk|Night,4,0.9,1.1\n";
        File.WriteAllText(Root+"/Data/buildings.csv",csv,new System.Text.UTF8Encoding(true)); AssetDatabase.Refresh();
        var scene=EditorSceneManager.NewScene(NewSceneSetup.EmptyScene,NewSceneMode.Single);
        var cam=new GameObject("Main Camera").AddComponent<Camera>(); cam.tag="MainCamera";
        cam.transform.position=new Vector3(0,22,-22); cam.transform.rotation=Quaternion.Euler(45,0,0);
        cam.orthographic=true; cam.orthographicSize=12; cam.nearClipPlane=.1f; cam.farClipPlane=150;
        cam.clearFlags=CameraClearFlags.SolidColor; cam.backgroundColor=Hex("233C50");
        cam.transparencySortMode=TransparencySortMode.CustomAxis; cam.transparencySortAxis=new Vector3(0,1,1);
        var context=new GameObject("World Context").AddComponent<WorldContext>();
        var spawner=new GameObject("World - pooled ground and buildings").AddComponent<BuildingSpawner>();
        spawner.context=context;spawner.catalog=catalog;spawner.rulesTable=AssetDatabase.LoadAssetAtPath<TextAsset>(Root+"/Data/buildings.csv");spawner.groundMaterial=ground;
        var plane=(GameObject)PrefabUtility.InstantiatePrefab(planePrefab);plane.transform.position=new Vector3(0,4,0);
        var env=new GameObject("Environment").AddComponent<EnvironmentView>();env.context=context;env.sceneCamera=cam;env.world=spawner;
        var panel=new GameObject("Prototype Controls").AddComponent<PrototypePanel>();panel.context=context;panel.world=spawner;
        EditorSceneManager.SaveScene(scene,Root+"/Scenes/FlightPrototype.unity");
        AssetDatabase.SaveAssets();
        CheckRules(csv);
        Debug.Log("ONAIR_GENERATED_AND_RULE_TESTS_PASSED");
    }
    static Transform AddSprite(Transform parent,string name,Sprite sprite,Material material)
    {
        var child=new GameObject(name);child.transform.SetParent(parent,false);child.transform.localRotation=Quaternion.Euler(45,0,0);
        var renderer=child.AddComponent<SpriteRenderer>();renderer.sprite=sprite;renderer.sharedMaterial=material;return child.transform;
    }
    static Color Hex(string value) { ColorUtility.TryParseHtmlString("#"+value,out var color);return color; }
    static Sprite MakeSprite(string name,int kind,Color color)
    {
        const int size=64; var tex=new Texture2D(size,size,TextureFormat.RGBA32,false);tex.SetPixels(new Color[size*size]);
        void Rect(int x,int y,int w,int h,Color c) { for(int xx=x;xx<x+w;xx++)for(int yy=y;yy<y+h;yy++)if(xx>=0&&yy>=0&&xx<size&&yy<size)tex.SetPixel(xx,yy,c); }
        Color outline=Hex("263745"),shade=color*.68f;shade.a=1;
        if(kind==6)
        {
            // Pixel silhouette: wings, fuselage, tail and cockpit. Replace the sprite without changing flight logic.
            Rect(5,24,54,8,outline);Rect(8,26,48,4,color);Rect(27,9,10,43,outline);Rect(29,11,6,39,color);
            Rect(30,51,4,7,color);Rect(18,11,28,5,outline);Rect(20,12,24,2,Hex("D97658"));Rect(29,34,6,9,Hex("579EBD"));
            Rect(10,28,8,2,Hex("D97658"));Rect(46,28,8,2,Hex("D97658"));
        }
        else
        {
            int width=kind==2?48:kind==3?18:kind==1?26:38;
            int height=kind==1?49:kind==3?43:kind==5?23:30;
            int x=(size-width)/2;
            Rect(x-1,1,width+2,height+2,outline);Rect(x,2,width,height,color);Rect(x+width-8,2,8,height,shade);
            Rect(x-3,height+1,width+6,5,outline);Rect(x-1,height+3,width+2,4,kind==0?Hex("AF6155"):shade);
            Rect(x+width/2-3,2,6,10,outline);
            for(int yy=15;yy<height-3;yy+=9)for(int xx=x+4;xx<x+width-9;xx+=9)
            { Rect(xx,yy,5,6,outline);Rect(xx+1,yy+1,3,4,kind==5?Hex("FFCE79"):Hex("BDE4DF")); }
            if(kind==3) { Rect(x-5,height+3,width+10,9,outline);Rect(x-3,height+5,width+6,5,Hex("FFDC8E"));Rect(x-6,height+12,width+12,3,Hex("C96F58")); }
            if(kind==5) Rect(x+3,height-2,width-6,4,Hex("F8BE76"));
        }
        tex.Apply();string path=Root+"/Art/"+name+".png";File.WriteAllBytes(path,tex.EncodeToPNG());UnityEngine.Object.DestroyImmediate(tex);
        AssetDatabase.ImportAsset(path);var importer=(TextureImporter)AssetImporter.GetAtPath(path);importer.textureType=TextureImporterType.Sprite;
        importer.spriteImportMode=SpriteImportMode.Single;importer.spritePixelsPerUnit=20;importer.filterMode=FilterMode.Point;importer.mipmapEnabled=false;importer.alphaIsTransparency=true;
        importer.textureCompression=TextureImporterCompression.Uncompressed;
        var settings=new TextureImporterSettings();importer.ReadTextureSettings(settings);settings.spriteAlignment=(int)SpriteAlignment.Custom;settings.spritePivot=kind==6?new Vector2(.5f,.5f):new Vector2(.5f,0);importer.SetTextureSettings(settings);importer.SaveAndReimport();
        return AssetDatabase.LoadAssetAtPath<Sprite>(path);
    }
    public static void CheckRules(string csv)
    {
        var rules=SpawnRules.Parse(csv);var rng=new System.Random(42);
        foreach(Biome b in Enum.GetValues(typeof(Biome)))foreach(Weather w in Enum.GetValues(typeof(Weather)))foreach(DayPeriod p in Enum.GetValues(typeof(DayPeriod)))
            for(int i=0;i<100;i++){var rule=SpawnRules.Pick(rules,b,w,p,rng);if(rule!=null&&!rule.Matches(b,w,p))throw new Exception("Filter violation");}
        if(SpawnRules.Pick(rules,Biome.Desert,Weather.Snow,DayPeriod.Day,rng)!=null)throw new Exception("No-match fallback violated");
        var leadingZero=SpawnRules.Parse(Header+"\n001,townhouse,*,*,*,1,1,1\n");
        if(leadingZero[0].Id != "001")throw new Exception("String id lost leading zeros");
        var a=new System.Random(7);var bRandom=new System.Random(7);
        for(int i=0;i<100;i++)if(SpawnRules.Pick(rules,Biome.City,Weather.Clear,DayPeriod.Night,a).Id!=SpawnRules.Pick(rules,Biome.City,Weather.Clear,DayPeriod.Night,bRandom).Id)throw new Exception("Seed mismatch");
        foreach(string bad in new[]{csv+",1,townhouse,*,*,*,1,1,1\n",csv.Replace("City|Coast","Typo"),csv.Replace(",5,",",NaN,"),csv.Replace("0.85,1.1","2,1")})
        {bool rejected=false;try{SpawnRules.Parse(bad);}catch(FormatException){rejected=true;}if(!rejected)throw new Exception("Invalid table accepted");}
        var quoted=SpawnRules.Parse(Header+"\r\n\"001\",townhouse,*,*,*,0,1,1\r\n");
        if(SpawnRules.Pick(quoted,Biome.City,Weather.Clear,DayPeriod.Day,rng)!=null)throw new Exception("Zero weight selected");
    }
    [MenuItem("OnAir/Validate building table")]
    public static void ValidateTable()
    {
        var rules=SpawnRules.Parse(File.ReadAllText(Root+"/Data/buildings.csv"));
        var map=AssetDatabase.LoadAssetAtPath<BuildingCatalog>(Root+"/Data/BuildingCatalog.asset").Resolve();
        foreach(var r in rules)if(!map.ContainsKey(r.PrefabId))throw new FormatException("Unknown prefab_id: "+r.PrefabId);
        Debug.Log("OnAir: valid table ("+rules.Count+" rows).");
    }
    public static void Smoke()
    {
        CheckRules(File.ReadAllText(Root+"/Data/buildings.csv"));
        ValidateTable();
        EditorSceneManager.OpenScene(Root+"/Scenes/FlightPrototype.unity");
        SessionState.SetBool("OnAirSmoke",true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void ResumeSmoke()
    {
        if(!SessionState.GetBool("OnAirSmoke",false))return;
        EditorApplication.update+=CaptureWhenReady;
    }
    static void CaptureWhenReady()
    {
        if(!EditorApplication.isPlaying||Time.time<2)return;
        EditorApplication.update-=CaptureWhenReady;SessionState.SetBool("OnAirSmoke",false);
        try
        {
            var world=UnityEngine.Object.FindObjectOfType<BuildingSpawner>();
            if(world.ActiveBuildingCount<10)throw new Exception("No populated world");
            var context=world.context;
            context.biome=Biome.Desert;context.weather=Weather.Snow;world.Rebuild();
            if(world.ActiveBuildingCount!=0)throw new Exception("No-match scene is not empty");
            context.biome=Biome.City;context.weather=Weather.Clear;world.Rebuild();
            if(world.ActiveBuildingCount<10)throw new Exception("Pool failed to restore buildings");
            var rt=new RenderTexture(1280,800,24);Camera.main.targetTexture=rt;Camera.main.Render();
            RenderTexture.active=rt;var tex=new Texture2D(1280,800,TextureFormat.RGB24,false);tex.ReadPixels(new Rect(0,0,1280,800),0,0);tex.Apply();
            File.WriteAllBytes("prototype-preview.png",tex.EncodeToPNG());Camera.main.targetTexture=null;RenderTexture.active=null;
            UnityEngine.Object.Destroy(tex);UnityEngine.Object.Destroy(rt);
            File.WriteAllText("smoke-result.txt","PASS: compile; 3600 filter draws; seed repeatability; invalid tables; play mode; prefab population; no-match; pool reuse; camera render.");
            Debug.Log("ONAIR_SMOKE_PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
}
