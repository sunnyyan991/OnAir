using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;

namespace OnAir.Prototype
{
    // Camera-relative foreground precipitation: independent from buildings and their random seed.
    public sealed class WeatherParticles : MonoBehaviour
    {
        public WorldContext context;
        public Camera sceneCamera;
        public TextAsset table;
        public string Status { get; private set; } = "Weather FX: not loaded";
        public int ParticleCount { get { int n = 0; foreach (var l in layers) n += l.System.particleCount; return n; } }
        sealed class Layer { public ParticleSystem System; public Material Material; public WeatherFxRule Rule; public float Mix, Target, Share; }
        readonly List<Layer> layers = new List<Layer>();
        Dictionary<Weather, WeatherFxRule> rules;
        Weather selected = (Weather)(-1);
        Shader shader;
        void Start()
        {
            if (!context || !sceneCamera) { Status = "Weather FX: missing context/camera"; enabled = false; return; }
            shader = Resources.Load<Shader>("OnAirWeather/PixelWeather");
            if (!shader) { Status = "Weather FX: missing shader"; enabled = false; return; }
            if (!table) table = Resources.Load<TextAsset>("OnAirWeather/weather_fx");
            ReloadTable();
        }
        public bool ReloadTable()
        {
            try
            {
                if (!table) table = Resources.Load<TextAsset>("OnAirWeather/weather_fx");
                if (!table) throw new InvalidOperationException("Missing weather_fx.csv");
                string csv = table.text;
                // Editor reads the saved source; player optionally reads a user-editable override.
                string path = Path.Combine(Application.persistentDataPath, "weather_fx.csv");
#if UNITY_EDITOR
                path = UnityEditor.AssetDatabase.GetAssetPath(table);
#endif
                if (File.Exists(path)) csv = File.ReadAllText(path);
                var parsed = WeatherFxTable.Parse(csv);
                rules = parsed;
                selected = (Weather)(-1);
                Status = "Weather FX: loaded " + parsed.Count + " profiles";
                return true;
            }
            catch (Exception e) { Status = "Weather FX: " + e.Message; Debug.LogWarning(Status, this); return false; }
        }
        void Update()
        {
            if (!context || !sceneCamera || rules == null || !shader) return;
            foreach (var layer in layers) { var main = layer.System.main; main.simulationSpeed = context.paused ? 0 : 1; }
            if (context.paused) return;
            if (selected != context.weather)
            {
                selected = context.weather;
                foreach (var layer in layers) layer.Target = 0;
                if (rules.TryGetValue(selected, out var rule) && rule.Shape != "none" && rule.Rate > 0)
                {
                    // Bound rapid toggling/reloads to two generations of two layers.
                    while (layers.Count > 2) Remove(layers[0]);
                    Add(rule, false); Add(rule, true);
                }
            }
            for (int i = layers.Count - 1; i >= 0; i--)
            {
                var l = layers[i];
                l.Mix = Mathf.MoveTowards(l.Mix, l.Target, Time.deltaTime / l.Rule.Transition);
                var emission = l.System.emission; emission.rateOverTime = l.Rule.Rate * l.Share * l.Mix;
                float light = context.period == DayPeriod.Night ? .6f : 1;
                l.Material.SetColor("_Tint", new Color(light,light,light,l.Mix));
                float distance = l.System.transform.localPosition.z;
                float halfHeight = sceneCamera.orthographic ? sceneCamera.orthographicSize : Mathf.Tan(sceneCamera.fieldOfView * .5f * Mathf.Deg2Rad) * distance;
                var shape = l.System.shape; shape.scale = new Vector3(halfHeight * 2 * sceneCamera.aspect + 4, halfHeight * 2 + 4, .1f);
                if (l.Target == 0 && l.Mix == 0) Remove(l);
            }
        }
        void Add(WeatherFxRule rule, bool near)
        {
            float share = near ? rule.NearRatio : 1 - rule.NearRatio;
            if (share <= 0) return;
            var go = new GameObject("Weather " + rule.Weather + (near ? " near" : " far"));
            go.transform.SetParent(sceneCamera.transform, false);
            go.transform.localPosition = new Vector3(0,0,Mathf.Max(sceneCamera.nearClipPlane + 1, near ? 4 : 8));
            var ps = go.AddComponent<ParticleSystem>(); ps.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            float scale = near ? 1.25f : .7f;
            var main = ps.main; main.playOnAwake = false; main.loop = true; main.duration = 12;
            main.simulationSpace = ParticleSystemSimulationSpace.Local; main.maxParticles = 4100;
            main.startLifetime = rule.Lifetime; main.startSpeed = 0;
            main.startSize3D = true; main.startSizeX = rule.Size * scale; main.startSizeY = rule.Length * scale; main.startSizeZ = 1;
            main.startColor = rule.Color;
            main.startRotation = rule.Shape == "rain" ? -Mathf.Atan2(rule.Wind, Mathf.Max(.01f,rule.Speed)) : 0;
            main.cullingMode = ParticleSystemCullingMode.AlwaysSimulate;
            var emission = ps.emission; emission.rateOverTime = 0;
            var shape = ps.shape; shape.shapeType = ParticleSystemShapeType.Box; shape.scale = new Vector3(40,28,.1f);
            var velocity = ps.velocityOverLifetime; velocity.enabled = true; velocity.space = ParticleSystemSimulationSpace.Local;
            velocity.x = rule.Wind * scale; velocity.y = -rule.Speed * scale;
            var noise = ps.noise; noise.enabled = rule.Sway > 0; noise.strength = rule.Sway; noise.frequency = .3f; noise.scrollSpeed = .25f;
            var color = ps.colorOverLifetime; color.enabled = true;
            var gradient = new Gradient(); gradient.SetKeys(new[] { new GradientColorKey(Color.white,0), new GradientColorKey(Color.white,1) }, new[] { new GradientAlphaKey(0,0), new GradientAlphaKey(1,.12f), new GradientAlphaKey(1,.75f), new GradientAlphaKey(0,1) }); color.color = gradient;
            var material = new Material(shader); material.SetFloat("_Shape", rule.Shape == "snow" ? 1 : rule.Shape == "dust" ? 2 : 0);
            material.SetColor("_Tint", new Color(1,1,1,0));
            var renderer = ps.GetComponent<ParticleSystemRenderer>(); renderer.sharedMaterial = material; renderer.renderMode = ParticleSystemRenderMode.Billboard;
            renderer.alignment = ParticleSystemRenderSpace.Local; renderer.sortingOrder = near ? 101 : 100;
            renderer.maxParticleSize = .1f;
            layers.Add(new Layer { System = ps, Material = material, Rule = rule, Mix = 0, Target = 1, Share = share });
            ps.Play();
        }
        void Remove(Layer layer) { layers.Remove(layer); if (layer.System) Destroy(layer.System.gameObject); if (layer.Material) Destroy(layer.Material); }
        void OnDisable() { foreach (var l in layers) if (l.System) l.System.Pause(); }
        void OnEnable() { foreach (var l in layers) if (l.System) l.System.Play(); }
        void OnDestroy() { while (layers.Count > 0) Remove(layers[0]); }
    }
}
