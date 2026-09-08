using System;
using System.IO;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OnAir;
public static class AircraftValidation
{
    public static void Run()
    {
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("AircraftPreview",true);EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod]static void Resume(){if(SessionState.GetBool("AircraftPreview",false))EditorApplication.update+=Capture;}
    static void Capture()
    {
        if(!EditorApplication.isPlaying||Time.time<4)return;EditorApplication.update-=Capture;SessionState.SetBool("AircraftPreview",false);
        try
        {
            var plane=UnityEngine.Object.FindObjectOfType<FlightView>();var meshes=plane.GetComponentsInChildren<MeshFilter>();
            if(meshes.Length<8||plane.GetComponentsInChildren<SpriteRenderer>().Length!=0)throw new Exception("Mesh replacement failed.");
            int triangles=0;foreach(var f in meshes)triangles+=f.sharedMesh.triangles.Length/3;
            if(Vector3.Dot(plane.visual.forward,plane.flight.Heading)<.999f)throw new Exception("Aircraft heading mismatch.");
            var context=UnityEngine.Object.FindObjectOfType<GameSession>();if(context){context.paused=true;context.weather=Weather.Clear;context.period=DayPeriod.Day;}
            var cam=Camera.main;Directory.CreateDirectory("Validation/Aircraft");
            Render(cam,"Validation/Aircraft/in-scene.png");
            cam.transform.position=plane.transform.position-cam.transform.forward*22;cam.orthographicSize=6;
            Render(cam,"Validation/Aircraft/close-up.png");
            var visualRotation=plane.visual.rotation;plane.flight.BeginCircle();
            if(context)context.paused=false;
            plane.flight.BeginCircle();plane.flight.Tick(1);plane.Refresh();
            if(Vector3.Dot(plane.visual.forward,plane.flight.Heading)<.999f)throw new Exception("Turn heading mismatch.");
            if(context)context.paused=true;
            var prop=plane.visual.Find("Propeller");var before=prop.localRotation;plane.Refresh();
            if(Quaternion.Angle(before,prop.localRotation)>.01f)throw new Exception("Paused propeller moved.");
            File.WriteAllText("Validation/Aircraft/validation.txt","PASS: compilation; Main scene; 3D replacement; no active SpriteRenderer; cruise and turn heading; paused propeller; "+triangles+" triangles; scene and close-up renders.");
            Debug.Log("AIRCRAFT_PREVIEW_PASS");EditorApplication.Exit(0);
        }
        catch(Exception e){Debug.LogException(e);EditorApplication.Exit(1);}
    }
    static void Render(Camera camera,string path)
    {
        var old=camera.targetTexture;var active=RenderTexture.active;var rt=new RenderTexture(1440,960,24);var tex=new Texture2D(1440,960,TextureFormat.RGB24,false);
        try{camera.targetTexture=rt;camera.Render();RenderTexture.active=rt;tex.ReadPixels(new Rect(0,0,1440,960),0,0);tex.Apply();File.WriteAllBytes(path,tex.EncodeToPNG());}
        finally{camera.targetTexture=old;RenderTexture.active=active;rt.Release();UnityEngine.Object.Destroy(rt);UnityEngine.Object.Destroy(tex);}
    }
}
