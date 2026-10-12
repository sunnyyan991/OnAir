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
    static System.Diagnostics.Stopwatch validationWatch;
    static void Require(bool condition,string message){if(!condition)throw new Exception(message);}
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("FlightValidation",true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool("FlightValidation",false))EditorApplication.update+=Test;}
    static void Step(float seconds)
    {
        Require(validationWatch.Elapsed.TotalSeconds<45,"Flight validation exceeded its 45-second limit.");
        // Match runtime order: flight Update, camera LateUpdate, then world demand.
        flight.Tick(seconds);camera.Follow(seconds);world.Refresh();view.Refresh();
    }
    static void Test()
    {
        if(!EditorApplication.isPlaying||Time.time<2)return;
        EditorApplication.update-=Test;SessionState.SetBool("FlightValidation",false);
        try
        {
            ValidateFlight();EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    public static void ValidateFlight(bool captureImages=true)
    {
            validationWatch=System.Diagnostics.Stopwatch.StartNew();
            var game=UnityEngine.Object.FindFirstObjectByType<GameEntry>();
            Require(game&&!game.saves.ActiveJourney,"Flight validation requires a fresh title scene, not a player's active journey.");
            flight=game.flight;camera=game.cameraFollow;journey=game.journey;world=game.world;view=flight.GetComponent<FlightView>();
            game.BeginFlightPreparation();game.RevealFlight();
            flight.enabled=camera.enabled=world.enabled=view.enabled=false;journey.context.paused=journey.context.menuPaused=false;
            journey.Restart(1372);Step(0);
            var planeStart=flight.transform.position;var cameraStart=camera.transform.position;
            var chunk=world.Chunks.First();var groundStart=chunk.transform.position;
            for(int i=0;i<300;i++)Step(1f/60);
            Require(flight.transform.position.z>planeStart.z+10,"Plane did not move through the world.");
            Require(camera.transform.position.z>cameraStart.z+8,"Camera did not follow the plane.");
            Require(chunk.transform.position==groundStart,"Ground moved during cruise.");
            if(captureImages)Capture("flight-cruise.png");
            var viewport=game.sceneCamera.WorldToViewportPoint(flight.transform.position);
            Require(viewport.z>0&&viewport.x>.15f&&viewport.x<.8f&&viewport.y>.2f&&viewport.y<.9f,"Plane left the camera composition.");
            int[] ids=world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingFootprint>()).Select(b=>b.GetInstanceID()).ToArray();
            int generations=world.GenerationCount;
            journey.CycleWeather();journey.context.SetPeriod(DayPeriod.Night);Step(0);
            var windowed=world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingView>()).FirstOrDefault(building=>building.windows!=null&&building.windows.Length>0&&building.windows[0]);
            Require(windowed,"Fixed-seed flight fixture has no windowed building.");
            var window=windowed.windows[0];
            window.name="Renamed window renderer";game.environment.Refresh(0,true);
            var emission=window.sharedMaterial.GetColor("_EmissionColor");
            Require(emission.r+emission.g+emission.b>0,"Night windows did not light after renderer rename.");
            Require(generations==world.GenerationCount&&ids.SequenceEqual(world.Chunks.SelectMany(c=>c.GetComponentsInChildren<BuildingFootprint>()).Select(b=>b.GetInstanceID())),"Weather/time regenerated buildings.");
            journey.context.SetPeriod(DayPeriod.Day);
            game.environment.Refresh(0,true);
            Require(window.sharedMaterial.GetColor("_EmissionColor").r==0,"Day did not turn window emission off.");
            journey.context.paused=true;
            var paused=flight.transform.position;string pausedFlight=JsonUtility.ToJson(flight.CaptureState());double meters=journey.TotalMeters;Step(10);
            Require(flight.transform.position==paused&&pausedFlight==JsonUtility.ToJson(flight.CaptureState())&&meters==journey.TotalMeters,"Pause moved plane or route.");
            journey.context.paused=false;
            Step(.5f);Require(flight.transform.position.z>paused.z&&journey.TotalMeters>meters,"Resume did not continue forward flight.");
            // Move to just before a natural boundary, then check camera and an overlapping chunk in the same reference frame.
            journey.Restart(1372);Step(0);journey.Advance(journey.MetersPerSegment-.1);Step(0);
            for(int i=0;i<120;i++)camera.Follow(1f/60);
            chunk=world.Chunks.First();
            var relativeGround=chunk.transform.position-flight.transform.position;var relativeCamera=camera.transform.position-flight.transform.position;
            var previousLeg=journey.Current;Step(1f/60);
            Require(journey.Current!=previousLeg,"Boundary did not enter the next segment.");
            Require((chunk.transform.position-flight.transform.position-relativeGround).magnitude<.1f,"Ground popped at rebase.");
            Require((camera.transform.position-flight.transform.position-relativeCamera).magnitude<.1f,"Camera jumped at rebase.");
            double routeBefore=journey.TotalMeters;var dataBefore=world.buildings;var legBefore=journey.Current;
            var original=game.climateTable;string malformed=TableSource.Read(original).Replace(",Ocean,",",Moon,");
            Require(malformed!=TableSource.Read(original),"Climate mutation did not apply.");
            var invalid=new TextAsset(malformed);
            try{game.climateTable=invalid;Require(!game.ReloadTables(),"Invalid table reload succeeded.");}
            finally{game.climateTable=original;UnityEngine.Object.DestroyImmediate(invalid);}
            Require(world.buildings==dataBefore&&journey.Current==legBefore&&journey.TotalMeters==routeBefore,"Invalid reload changed live state.");
            Require(game.ReloadTables()&&journey.Current==legBefore&&journey.TotalMeters==routeBefore,"Unchanged table reload reset route.");
            File.WriteAllText("flight-validation.txt","PASS: fixed-seed forward flight; smooth camera follow; stationary ground during cruise; in-frame sprite; weather/time preserve buildings; pause and resume; continuous camera/ground at segment boundary; invalid climate reload rejected without changing source files.");
            Debug.Log("FLIGHT_VALIDATION_PASSED");
    }
    static void Capture(string path)
    {
        var cam=camera.GetComponent<Camera>();var previous=RenderTexture.active;var previousTarget=cam.targetTexture;
        var rt=new RenderTexture(1440,900,24);var texture=new Texture2D(1440,900,TextureFormat.RGB24,false);
        try{cam.targetTexture=rt;cam.Render();RenderTexture.active=rt;texture.ReadPixels(new Rect(0,0,1440,900),0,0);texture.Apply();File.WriteAllBytes(path,texture.EncodeToPNG());}
        finally{cam.targetTexture=previousTarget;RenderTexture.active=previous;UnityEngine.Object.Destroy(texture);UnityEngine.Object.Destroy(rt);}
    }
}
