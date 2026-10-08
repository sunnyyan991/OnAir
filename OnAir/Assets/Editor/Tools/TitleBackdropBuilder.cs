using System;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OnAir;

public static class TitleBackdropBuilder
{
    public const string PrefabPath="Assets/Prefabs/UI/TitleBackdrop.prefab";
    [MenuItem("OnAir/Setup/Build Fixed Title Backdrop")]
    public static void Build()
    {
        if(EditorApplication.isPlaying)throw new InvalidOperationException("Stop Play before authoring the title.");
        var scene=EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        var game=UnityEngine.Object.FindFirstObjectByType<GameEntry>();
        if(!game||game.flight.gameObject==game.gameObject||game.sceneCamera.gameObject==game.gameObject)
            throw new InvalidOperationException("Main needs separate flight and camera objects.");
        int layer=TitleLayer();
        if(!AssetDatabase.IsValidFolder("Assets/Prefabs/UI"))AssetDatabase.CreateFolder("Assets/Prefabs","UI");
        var root=new GameObject("Title backdrop");var backdrop=root.AddComponent<TitleBackdrop>();
        var cameraObject=new GameObject("Title camera",typeof(Camera),typeof(AudioListener));cameraObject.transform.SetParent(root.transform,false);
        var camera=cameraObject.GetComponent<Camera>();camera.CopyFrom(game.sceneCamera);
        camera.orthographic=true;camera.orthographicSize=68;camera.farClipPlane=600;
        camera.cullingMask=1<<layer;camera.clearFlags=CameraClearFlags.SolidColor;
        camera.backgroundColor=new Color(.53f,.64f,.69f);
        camera.transform.rotation=game.sceneCamera.transform.rotation;
        camera.transform.position=new Vector3(0,18,0)-camera.transform.forward*220;
        backdrop.sceneCamera=camera;
        var sunObject=new GameObject("Title daylight",typeof(Light));sunObject.transform.SetParent(root.transform,false);
        var sun=sunObject.GetComponent<Light>();sun.type=LightType.Directional;sun.color=new Color(1,.96f,.88f);sun.intensity=1.1f;
        sun.shadows=LightShadows.Soft;sun.cullingMask=1<<layer;sun.transform.rotation=Quaternion.Euler(58,-10,0);backdrop.sun=sun;
        Material grass=Mat("Grass"),asphalt=Mat("Asphalt"),paving=Mat("Pavement"),paint=Mat("Road paint");
        Box(root,"Fixed ground",new Vector3(0,-.5f,0),new Vector3(400,1,400),grass);
        foreach(float z in new[]{-48f,0f,48f})
        {
            Box(root,"Fixed street",new Vector3(0,.02f,z),new Vector3(400,.08f,10),asphalt);
            Box(root,"Sidewalk north",new Vector3(0,.1f,z+7),new Vector3(400,.15f,4),paving);
            Box(root,"Sidewalk south",new Vector3(0,.1f,z-7),new Vector3(400,.15f,4),paving);
            for(int x=-180;x<=180;x+=10)Box(root,"Lane marking",new Vector3(x,.07f,z),new Vector3(4,.015f,.2f),paint);
        }
        foreach(float x in new[]{-48f,48f})Box(root,"Fixed avenue",new Vector3(x,.04f,0),new Vector3(10,.08f,400),asphalt);
        string[] buildings={"Buildings/JP_001_Apartment","Buildings/Residential/JP_002_House","Buildings/Residential/JP_005_NarrowHouse","Buildings/Residential/JP_007_LongApartment","Buildings/Commercial/D01_Izakaya","Buildings/Commercial/D02_CornerConvenience"};
        // Author once in the Editor. The saved prefab contains these exact objects and poses.
        float[] columns={-82,-62,-22,22,62,82},rows={-82,-62,-22,22,62,82};
        for(int row=0;row<rows.Length;row++)for(int col=0;col<columns.Length;col++)
            Place(root,buildings[(row+col)%buildings.Length],new Vector3(columns[col],0,rows[row]),row%2==0?0:180);
        for(int i=0;i<12;i++)Place(root,"World/City_Broadleaf",new Vector3(106,0,-88+i*16),0);
        Place(root,"Characters/LowWing",new Vector3(18,43,12),0);
        foreach(var node in root.GetComponentsInChildren<Transform>(true))node.gameObject.layer=layer;
        var prefab=PrefabUtility.SaveAsPrefabAsset(root,PrefabPath);UnityEngine.Object.DestroyImmediate(root);
        if(!prefab)throw new InvalidOperationException("Could not save fixed title prefab.");
        if(game.titleBackdrop)UnityEngine.Object.DestroyImmediate(game.titleBackdrop.gameObject);
        var instance=(GameObject)PrefabUtility.InstantiatePrefab(prefab,scene);game.titleBackdrop=instance.GetComponent<TitleBackdrop>();
        game.flight.gameObject.SetActive(false);game.sceneCamera.gameObject.SetActive(false);game.sun.enabled=false;
        EditorUtility.SetDirty(game);EditorSceneManager.MarkSceneDirty(scene);EditorSceneManager.SaveScene(scene);AssetDatabase.SaveAssets();
        Debug.Log("ONAIR_FIXED_TITLE_AUTHORED buildings=36 prefab="+PrefabPath);
    }
    static int TitleLayer()
    {
        int existing=LayerMask.NameToLayer("TitleBackdrop");if(existing>=0)return existing;
        var manager=new SerializedObject(AssetDatabase.LoadAllAssetsAtPath("ProjectSettings/TagManager.asset")[0]);
        var layers=manager.FindProperty("layers");
        for(int i=31;i>=8;i--)if(string.IsNullOrEmpty(layers.GetArrayElementAtIndex(i).stringValue))
        {layers.GetArrayElementAtIndex(i).stringValue="TitleBackdrop";manager.ApplyModifiedPropertiesWithoutUndo();return i;}
        throw new InvalidOperationException("No free layer for fixed title camera.");
    }
    static Material Mat(string name)=>AssetDatabase.LoadAssetAtPath<Material>("Assets/Art/Materials/Shared/"+name+".mat")??throw new InvalidOperationException("Missing title material "+name);
    static void Box(GameObject root,string name,Vector3 position,Vector3 size,Material material)
    {
        var obj=GameObject.CreatePrimitive(PrimitiveType.Cube);obj.name=name;obj.transform.SetParent(root.transform,false);
        obj.transform.localPosition=position;obj.transform.localScale=size;obj.GetComponent<Renderer>().sharedMaterial=material;
        UnityEngine.Object.DestroyImmediate(obj.GetComponent<Collider>());
    }
    static void Place(GameObject root,string path,Vector3 position,float yaw)
    {
        var source=AssetDatabase.LoadAssetAtPath<GameObject>("Assets/Prefabs/"+path+".prefab")??throw new InvalidOperationException("Missing title prop "+path);
        var obj=(GameObject)PrefabUtility.InstantiatePrefab(source);obj.transform.SetParent(root.transform,false);
        obj.transform.localPosition=position;obj.transform.localRotation=Quaternion.Euler(0,yaw,0);
        PrefabUtility.UnpackPrefabInstance(obj,PrefabUnpackMode.Completely,InteractionMode.AutomatedAction);
        foreach(var pixels in obj.GetComponentsInChildren<PixelObjectView>(true))pixels.Configure();
        foreach(var script in obj.GetComponentsInChildren<MonoBehaviour>(true))UnityEngine.Object.DestroyImmediate(script);
    }
}
