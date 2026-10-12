using System;
using System.IO;
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using OnAir;

public static class WeatherValidation
{
    static readonly Weather[] Cases = { Weather.Rain, Weather.HeavyRain, Weather.Snow, Weather.Sandstorm, Weather.Clear };
    static int stage;
    static float started;
    static ParticleSystem pausedSystem;
    static int pausedCount;
    static float pausedLifetime;
    [MenuItem("OnAir/Validate weather table")]
    public static void Validate()
    {
        string csv = File.ReadAllText("Assets/Data/Tables/weather_fx.csv");
        if (WeatherFxTable.Parse(csv).Count != 6) throw new Exception("Expected six weather profiles.");
        foreach (string bad in new[] { csv.Replace(",500,",",NaN,"), csv.Replace(",0.2,",",2,"), csv.Replace("#C9E2EAB0","oops"), csv.Replace(",Snow,snow",",Unknown,snow"), csv.Replace(",Sandstorm,dust",",Snow,dust"), csv.Replace(",3.5,1.2,#",",3.5,99,#") })
        {
            if (bad == csv) continue;
            bool rejected = false; try { WeatherFxTable.Parse(bad); } catch (FormatException) { rejected = true; }
            if (!rejected) throw new Exception("Malformed weather config accepted.");
        }
        string climate = File.ReadAllText("Assets/Data/Tables/climate.csv");
        var zones = ClimateTable.Parse(climate);
        if (!zones["Hills"].Any(r => r.weather == Weather.Snow && r.weight > 0)) throw new Exception("Snow unreachable in Hills.");
        // Sandstorm remains a manual weather effect; Desert is no longer an ecology zone.
        var durationRow=CsvReader.Read(climate).First(row=>row.Length==6&&row[0]=="");
        string invalidDuration=climate.Replace(string.Join(",",durationRow),string.Join(",",durationRow.Take(4).Concat(new[]{"10","5"})));
        foreach (string bad in new[] { climate.Replace(",Hills,Snow,", ",Hills,Hail,"), invalidDuration, climate.Replace(",Ocean,", ",Moon,"), climate.Replace(",Town,Cloudy,", ",Town,Clear,") })
        {
            if (bad == climate) throw new Exception("Climate mutation did not apply.");
            bool rejected = false; try { ClimateTable.Parse(bad); } catch (FormatException) { rejected = true; }
            if (!rejected) throw new Exception("Malformed climate config accepted.");
        }
        BuildingRules.Parse(File.ReadAllText("Assets/Data/Tables/buildings.csv"));
        Debug.Log("WEATHER_TABLE_VALIDATION_PASS");
    }
    public static void Run()
    {
        Directory.CreateDirectory("Validation/Weather");
        Validate();
        EditorSceneManager.OpenScene("Assets/Scenes/Main.unity");
        SessionState.SetBool("OnAirWeatherSmoke", true);
        EditorApplication.EnterPlaymode();
    }
    [InitializeOnLoadMethod] static void Resume()
    {
        if (SessionState.GetBool("OnAirWeatherSmoke", false)) { stage = -1; EditorApplication.update += Tick; }
    }
    static void Tick()
    {
        if (!EditorApplication.isPlaying || Time.time < 1) return;
        try
        {
            var fx = UnityEngine.Object.FindObjectOfType<WeatherParticles>();
            if (!fx || !fx.enabled) throw new Exception("Weather integration missing.");
            if (stage == Cases.Length + 1)
            {
                if (Time.time - started < 1) return;
                var particles = new ParticleSystem.Particle[4100];
                int count = pausedSystem.GetParticles(particles);
                if (count != pausedCount || Mathf.Abs(particles[0].remainingLifetime - pausedLifetime) > .05f) throw new Exception("Pause did not freeze particles.");
                fx.context.paused = false;
                string tablePath = "Assets/Data/Tables/weather_fx.csv";
                string original = File.ReadAllText(tablePath);
                byte[] originalBytes = File.ReadAllBytes(tablePath);
                try { File.WriteAllText(tablePath, original.Replace(",500,",",NaN,")); if (fx.ReloadTable()) throw new Exception("Invalid reload accepted."); }
                finally { File.WriteAllBytes(tablePath, originalBytes); }
                if (!fx.ReloadTable()) throw new Exception("Valid reload failed.");
                if (ShaderUtil.ShaderHasError(fx.shader)) throw new Exception("Weather shader has errors.");
                SessionState.SetBool("OnAirWeatherSmoke",false); EditorApplication.update -= Tick;
                File.WriteAllText("Validation/Weather/validation.txt","PASS: weather CSV invalid values / enum / duplicate / color / lifetime checks; existing building-rule regression; runtime auto-install; Rain, HeavyRain, Snow, Sandstorm particle population; transition to Clear; pause; invalid reload rejection and valid reload recovery; shader compilation; five camera renders.\n");
                Debug.Log("WEATHER_FX_SMOKE_PASS"); EditorApplication.Exit(0); return;
            }
            if (stage == Cases.Length)
            {
                if (Time.time - started < 4) return;
                pausedSystem = fx.sceneCamera.GetComponentInChildren<ParticleSystem>();
                if (!pausedSystem) throw new Exception("Snow missing for pause test.");
                fx.context.paused = true;
                var particles = new ParticleSystem.Particle[4100]; pausedCount = pausedSystem.GetParticles(particles);
                if (pausedCount == 0) throw new Exception("Empty pause test.");
                pausedLifetime = particles[0].remainingLifetime;
                stage++; started = Time.time; return;
            }
            if (stage < 0) { stage = 0; fx.context.weather = Cases[stage]; started = Time.time; return; }
            if (Time.time - started < 4) return;
            if (Cases[stage] != Weather.Clear && fx.ParticleCount == 0) throw new Exception("No particles for " + Cases[stage]);
            if (Cases[stage] == Weather.Clear && fx.ParticleCount != 0) throw new Exception("Clear did not clear particles.");
            Directory.CreateDirectory("Validation/Weather/Previews");
            var camera = fx.sceneCamera;
            var target = new RenderTexture(1280,720,24);
            var previous = RenderTexture.active;
            var previousTarget = camera.targetTexture;
            var tex = new Texture2D(1280,720,TextureFormat.RGB24,false);
            try
            {
                camera.targetTexture = target; camera.Render(); RenderTexture.active = target;
                tex.ReadPixels(new Rect(0,0,1280,720),0,0); tex.Apply();
                File.WriteAllBytes("Validation/Weather/Previews/" + Cases[stage] + ".png",tex.EncodeToPNG());
            }
            finally { camera.targetTexture = previousTarget; RenderTexture.active = previous; UnityEngine.Object.Destroy(tex); target.Release(); UnityEngine.Object.Destroy(target); }
            stage++;
            if (stage < Cases.Length) { fx.context.weather = Cases[stage]; started = Time.time; return; }
            fx.context.weather = Weather.Snow; started = Time.time;
        }
        catch (Exception e) { SessionState.SetBool("OnAirWeatherSmoke",false); Debug.LogException(e); EditorApplication.Exit(1); }
    }
}
