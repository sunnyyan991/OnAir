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
        string buildings=TableSource.Read(game.buildingsTable);
        var data=new BuildingData(buildings,game.buildings);
        Require(data.Rules.Count>0,"Missing building rules.");
        Require(game.worldProfile&&game.worldProfile.kit&&game.worldProfile.segmentMeters==320,"World profile changed route settings.");
        string themes=TableSource.Read(game.themesTable);var themeRules=ThemeTable.Parse(themes);
        Require(themeRules[0].key=="japan"&&themeRules[0].zoneWeights.SequenceEqual(ContinuousWorldPlan.DefaultZoneWeights),"Japan theme no longer matches the original zone weights.");
        foreach(string bad in new[]{themes.Replace(",2,usa,",",1,usa,"),themes.Replace("City|CoastalTown|RiverPlain|Ocean,","City|Moon|RiverPlain|Ocean,"),themes.Replace(",85|5|5|5",",85|5|5"),themes.Replace(",85|5|5|5",",0|0|0|0")})
        {
            Require(bad!=themes,"Theme mutation did not apply.");
            bool rejected=false;try{ThemeTable.Parse(bad);}catch(FormatException){rejected=true;}Require(rejected,"Malformed theme config accepted.");
        }
        string firstId=data.Rules[0].Id,paddedId="00"+firstId;
        string paddedBuildings=buildings.Replace(","+firstId+",",","+paddedId+",");
        Require(paddedBuildings!=buildings&&BuildingRules.Parse(paddedBuildings)[0].Id==paddedId,"Building ID was converted to a number.");
        var csv=CsvReader.Read("a,b\r\n\"one, two\",\"line1\nline2\"\r\n");Require(csv[1][0]=="one, two"&&csv[1][1]=="line1\nline2","CSV quote/newline regression.");
        foreach(var entry in game.buildings.entries)
        {
            var view=entry.prefab.GetComponent<BuildingView>();Require(view&&view.windows!=null&&view.windows.All(r=>r),"Missing explicit window references: "+entry.id);
            var footprint=entry.prefab.GetComponent<BuildingFootprint>();
            Require(footprint&&SaveMigration.Finite(footprint.size.x)&&SaveMigration.Finite(footprint.size.y)&&footprint.size.x>0&&footprint.size.y>0&&footprint.floors>0&&SaveMigration.Finite(footprint.height)&&footprint.height>=0,"Invalid building footprint: "+entry.id);
            Require(footprint.Modules.x>0&&footprint.Modules.y>0&&footprint.Modules.z>0,"Invalid building module dimensions: "+entry.id);
        }
        var pipeline=GraphicsSettings.defaultRenderPipeline as UniversalRenderPipelineAsset;
        Require(pipeline&&(!QualitySettings.renderPipeline||QualitySettings.renderPipeline==pipeline),"Main and quality settings use different rendering pipelines.");
        Require(!ShaderUtil.ShaderHasError(game.weatherShader),"Weather shader errors.");
        WeatherValidation.Validate();Directory.CreateDirectory("Validation");
        File.WriteAllText("Validation/structure.txt","PASS: single Main scene and build entry; scene/prefab scripts; building rules; world profile; theme table; numeric-string IDs; malformed table rejection; shared CSV parser; explicit prefab windows; footprint; unified rendering and weather shader.");
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
