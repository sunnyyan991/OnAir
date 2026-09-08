using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OnAir;
public static class FlightValidation
{
    static FlightController flight;
    static CameraFollow camera;
    static JourneyController journey;
    static TerrainStreamer world;
    static FlightView view;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("FlightValidation",true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool("FlightValidation",false))EditorApplication.update+=Test;}
    static void Step(float seconds)
    {
        flight.Tick(seconds);world.Refresh();camera.Follow(seconds);view.Refresh();
    }
    static void Test()
    {
        if(!EditorApplication.isPlaying||Time.time<2)return;
        EditorApplication.update-=Test;SessionState.SetBool("FlightValidation",false);
        try
        {
            flight=UnityEngine.Object.FindObjectOfType<FlightController>();camera=Camera.main.GetComponent<CameraFollow>();
            journey=flight.journey;world=UnityEngine.Object.FindObjectOfType<TerrainStreamer>();view=flight.GetComponent<FlightView>();
            flight.enabled=camera.enabled=world.enabled=view.enabled=false;flight.autoCircle=false;journey.context.paused=false;
            journey.NewSeed();Step(0);
            var planeStart=flight.transform.position;var cameraStart=camera.transform.position;
            var chunk=world.Chunks.First(c=>c.name.StartsWith("Terrain 0 /"));var groundStart=chunk.transform.position;
            for(int i=0;i<300;i++)Step(1f/60);
            Require(flight.transform.position.z>planeStart.z+10,"Plane did not move through the world.");
            Require(camera.transform.position.z>cameraStart.z+8,"Camera did not follow the plane.");
            Require(chunk.transform.position==groundStart,"Ground moved during cruise.");
            Capture("flight-cruise.png");
            var viewport=Camera.main.WorldToViewportPoint(flight.transform.position);
            Require(viewport.z>0&&viewport.x>.15f&&viewport.x<.8f&&viewport.y>.2f&&viewport.y<.9f,"Plane left the camera composition.");
            int[] ids=world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingFootprint>()).Select(b=>b.GetInstanceID()).ToArray();
            int generations=world.GenerationCount;
            journey.CycleWeather();journey.context.period=DayPeriod.Night;Step(0);
            var game=UnityEngine.Object.FindObjectOfType<GameEntry>();
            var window=world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingView>()).First().windows[0];
            window.name="Renamed window renderer";game.environment.Refresh(1f/60);
            var properties=new MaterialPropertyBlock();window.GetPropertyBlock(properties);
            var emission=properties.GetColor("_EmissionColor");
            Require(emission.r+emission.g+emission.b>0,"Night windows did not light after renderer rename.");
            Require(generations==world.GenerationCount&&ids.SequenceEqual(world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingFootprint>()).Select(b=>b.GetInstanceID())),"Weather/time regenerated buildings.");
            journey.context.period=DayPeriod.Day;
            game.environment.Refresh(1f/60);window.GetPropertyBlock(properties);
            Require(properties.GetColor("_EmissionColor").r==0,"Day did not turn window emission off.");
            var circleStart=flight.transform.position;var direction=flight.Heading;double meters=journey.TotalMeters;
            Require(flight.BeginCircle(),"Could not start circle.");
            Require(!flight.BeginCircle(),"Circle restarted while already circling.");
            bool quarter=false,half=false;float maxRadius=0,maxBank=0;
            for(int i=0;i<1200&&flight.IsCircling;i++)
            {
                Step(1f/60);maxRadius=Mathf.Max(maxRadius,Vector3.Distance(circleStart,flight.transform.position));maxBank=Mathf.Max(maxBank,Mathf.Abs(flight.Bank));
                if(!quarter&&flight.CircleProgress>=.25f){quarter=true;Require(Vector3.Dot(direction,flight.Heading)<.1f,"Circle heading did not turn.");Capture("flight-quarter-turn.png");}
                if(!half&&flight.CircleProgress>=.5f){half=true;Require(Vector3.Dot(direction,flight.Heading)<-.9f,"Plane did not reverse heading halfway through circle.");Capture("flight-half-turn.png");}
                if(flight.IsCircling)Require(journey.TotalMeters==meters,"Circling advanced terrain progression.");
            }
            Require(quarter&&half&&!flight.IsCircling&&maxRadius>8&&maxBank>20,"Full orbit or banking failed.");
            var horizontal=flight.transform.position-circleStart;horizontal.y=0;
            Require(horizontal.magnitude<.05f&&Vector3.Dot(direction,flight.Heading)>.999f,"Circle did not return smoothly to the route.");
            flight.BeginCircle();Step(.5f);journey.context.paused=true;
            var paused=flight.transform.position;float phase=flight.CircleProgress;meters=journey.TotalMeters;Step(10);
            Require(flight.transform.position==paused&&phase==flight.CircleProgress&&meters==journey.TotalMeters,"Pause moved plane or route.");
            journey.context.paused=false;journey.NextTerrain();Step(0);Require(!flight.IsCircling,"Terrain skip did not cancel circle.");
            // Move to just before a natural boundary, then check camera and an overlapping chunk in the same reference frame.
            journey.NewSeed();Step(0);journey.Advance(journey.Current.terrain.distanceMeters-.1);Step(0);
            for(int i=0;i<120;i++)camera.Follow(1f/60);
            chunk=world.Chunks.First(c=>c.name.StartsWith("Terrain 1 /"));
            var relativeGround=chunk.transform.position-flight.transform.position;var relativeCamera=camera.transform.position-flight.transform.position;
            var previousTerrain=journey.Current.terrain;Step(1f/60);
            Require(journey.Current.terrain!=previousTerrain,"Boundary did not enter another terrain.");
            Require((chunk.transform.position-flight.transform.position-relativeGround).magnitude<.1f,"Ground popped at rebase.");
            Require((camera.transform.position-flight.transform.position-relativeCamera).magnitude<.1f,"Camera jumped at rebase.");
            for(int i=0;i<30;i++){journey.NextTerrain();Step(0);Require(world.Chunks.Count()==4&&journey.Current.terrain.Allows(journey.context.weather),"Chunk bound or weather failed.");}
            journey.NewSeed();Step(0);flight.autoCircle=true;flight.firstCircleAfter=.1f;journey.NewSeed();Step(.2f);Require(flight.IsCircling,"Automatic circle did not start.");
            double routeBefore=journey.TotalMeters;var dataBefore=world.buildings;var legBefore=journey.Current;
            var terrainPath=AssetDatabase.GetAssetPath(game.terrainsTable);byte[] originalBytes=File.ReadAllBytes(terrainPath);string original=File.ReadAllText(terrainPath);
            try{File.WriteAllText(terrainPath,original.Replace(",320,",",NaN,"));Require(!game.ReloadTables(),"Invalid table reload succeeded.");}
            finally{File.WriteAllBytes(terrainPath,originalBytes);}
            Require(world.buildings==dataBefore&&journey.Current==legBefore&&journey.TotalMeters==routeBefore,"Invalid reload changed live state.");
            Require(game.ReloadTables()&&journey.Current==legBefore&&journey.TotalMeters==routeBefore,"Unchanged table reload reset route.");
            int seedBefore=journey.seed;
            try
            {
                File.WriteAllText(terrainPath,original.Replace(",320,",",400,"));
                Require(game.ReloadTables()&&journey.Current.terrain.distanceMeters==400&&journey.TotalMeters==0&&journey.seed==seedBefore,"Changed terrain table did not apply cleanly.");
                Step(0);Require(!flight.IsCircling,"Terrain rule reload did not reset flight pose.");
            }
            finally{File.WriteAllBytes(terrainPath,originalBytes);game.ReloadTables();}
            File.WriteAllText("flight-validation.txt","PASS: plane world movement; smooth camera follow; stationary ground during cruise; in-frame sprite; weather/time preserve buildings; full circle and heading; return to route; banking; pause mid-circle; terrain skip/reset; continuous camera/ground at natural boundary; 30 terrain changes with four bounded chunks and valid weather; automatic circling.");
            Debug.Log("FLIGHT_VALIDATION_PASSED");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Capture(string path)
    {
        var cam=Camera.main;var previous=RenderTexture.active;var previousTarget=cam.targetTexture;
        var rt=new RenderTexture(1440,900,24);var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{cam.targetTexture=previousTarget;RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);}
    }
}
