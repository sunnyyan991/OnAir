using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.Build.Reporting;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using OnAir;
public static class ProjectValidation
{
    static void Require(bool ok,string message){if(!ok)throw new Exception(message);}
    [MenuItem("OnAir/Validate project")]
    static void ValidateMenu()
    {
        if(EditorSceneManager.SaveCurrentModifiedScenesIfUserWantsTo())Validate();
    }
    public static void Validate()
    {
        var scenes=AssetDatabase.FindAssets("t:Scene",new[]{"Assets"}).Select(AssetDatabase.GUIDToAssetPath).ToArray();
        Require(scenes.Length==1&&scenes[0]=="Assets/Scenes/Main.unity","Expected only Main scene.");
        Require(EditorBuildSettings.scenes.Length==1&&EditorBuildSettings.scenes[0].enabled&&EditorBuildSettings.scenes[0].path==scenes[0],"Wrong build entry point.");
        EditorSceneManager.OpenScene(scenes[0]);
        foreach(var root in EditorSceneManager.GetActiveScene().GetRootGameObjects())CheckScripts(root);
        foreach(var guid in AssetDatabase.FindAssets("t:Prefab",new[]{"Assets"}))CheckScripts(AssetDatabase.LoadAssetAtPath<GameObject>(AssetDatabase.GUIDToAssetPath(guid)));
        var game=UnityEngine.Object.FindObjectOfType<GameEntry>();Require(game&&game.flight&&game.weatherShader,"Missing game references.");
        Require(game.flight.GetComponent<FlightView>().modelPrefab,"Missing low-wing model reference.");
        string buildings=TableSource.Read(game.buildingsTable),terrains=TableSource.Read(game.terrainsTable);
        var data=new BuildingData(buildings,game.buildings);var rules=TerrainTable.Parse(terrains);var definitions=game.terrains.Resolve(rules);
        Require(data.Rules.Count==18&&definitions.Length==3,"Missing migrated table rows.");
        foreach(var terrain in definitions)
        {
            Require(data.Rules.Count(b=>b.Matches(terrain.biome,Weather.Clear,DayPeriod.Day))==6,"Wrong per-terrain building selection.");
            Require(terrain.distanceMeters==320&&terrain.selectionWeight==1,"Migration changed route settings.");
            var so=new SerializedObject(terrain.profile);Require(so.FindProperty("distanceMeters")==null&&so.FindProperty("weather")==null,"Duplicated editable terrain rules remain in profile.");
        }
        Require(BuildingRules.Parse(buildings.Replace(",101,",",00101,"))[0].Id=="00101","Building ID was converted to a number.");
        Require(TerrainTable.Parse(terrains.Replace(",1,City,",",001,City,"))[0].id=="001","Terrain ID was converted to a number.");
        foreach(string bad in new[]{terrains.Replace(",320,",",0,"),terrains.Replace(",320,",",NaN,"),terrains.Replace(",City,",",Unknown,"),terrains.Replace("5|1|1|1|1","5|1"),terrains.Replace(",2,Coast,",",1,Coast,"),terrains.Replace("5|1|1|1|1","0|0|0|0|0")})
        {
            bool rejected=false;try{TerrainTable.Parse(bad);}catch(FormatException){rejected=true;}Require(rejected,"Malformed terrain config accepted.");
        }
        var csv=CsvReader.Read("a,b\r\n\"one, two\",\"line1\nline2\"\r\n");Require(csv[1][0]=="one, two"&&csv[1][1]=="line1\nline2","CSV quote/newline regression.");
        foreach(var entry in game.buildings.entries)
        {
            var view=entry.prefab.GetComponent<BuildingView>();Require(view&&view.windows.Length>0&&view.windows.All(r=>r),"Missing explicit window references: "+entry.id);
            Require(entry.prefab.GetComponent<BuildingFootprint>().size.x<=7.95f,"Building footprint overflow.");
        }
        var pipeline=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        Require(pipeline&&!pipeline.useSRPBatcher&&QualitySettings.renderPipeline==pipeline,"Main rendering configuration changed.");
        Require(!ShaderUtil.ShaderHasError(game.weatherShader),"Weather shader errors.");
        WeatherValidation.Validate();Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/structure.txt","PASS: single Main scene and build entry; scene/prefab scripts; 18 building rules / 3 terrains; preserved per-terrain settings; numeric-string IDs; malformed table rejection; shared CSV parser; explicit prefab windows; footprint; unified rendering and weather shader.");
        Debug.Log("PROJECT_STRUCTURE_VALIDATED");
    }
    static void CheckScripts(GameObject root)
    {
        foreach(var t in root.GetComponentsInChildren<Transform>(true))Require(GameObjectUtility.GetMonoBehavioursWithMissingScriptCount(t.gameObject)==0,"Missing script: "+root.name+"/"+t.name);
    }
    public static void RunFlight(){Validate();FlightValidation.Run();}
    public static void Build()
    {
        Validate();Directory.CreateDirectory("Build");
        var report=BuildPipeline.BuildPlayer(new BuildPlayerOptions{scenes=new[]{"Assets/Scenes/Main.unity"},locationPathName="Build/OnAir.exe",target=BuildTarget.StandaloneWindows64,options=BuildOptions.Development});
        Require(report.summary.result==BuildResult.Succeeded,"Player build failed: "+report.summary.result);
        File.WriteAllText("Validation/build.txt","PASS: Windows player build, Main scene, "+report.summary.totalErrors+" errors.");Debug.Log("ONAIR_PLAYER_BUILD_PASSED");
    }
}
