using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir.Prototype
{
    public sealed class BuildingSpawner : MonoBehaviour
    {
        public WorldContext context;
        public TextAsset rulesTable;
        public BuildingCatalog catalog;
        public int seed = 2409;
        [Min(0)] public float scrollSpeed = 3;
        public Material groundMaterial;
        const int RowCount = 12;
        const float RowSpacing = 5, StartZ = -25;
        readonly List<Transform> rows = new List<Transform>();
        readonly Dictionary<GameObject, Stack<GameObject>> pool = new Dictionary<GameObject, Stack<GameObject>>();
        readonly Dictionary<GameObject, GameObject> sources = new Dictionary<GameObject, GameObject>();
        List<SpawnRule> rules; Dictionary<string, GameObject> prefabs; System.Random random;
        Transform poolRoot;
        public string Status { get; private set; } = "Not loaded";
        public int EligibleCount { get; private set; }
        public int ActiveBuildingCount => sources.Count;
        public void Initialize()
        {
            if (rows.Count > 0) return;
            if (!context || !groundMaterial) { Status = "Assign Context and Ground Material."; Debug.LogError(Status,this); enabled=false; return; }
            poolRoot = new GameObject("Building pool").transform; poolRoot.SetParent(transform); poolRoot.gameObject.SetActive(false);
            for (int i = 0; i < RowCount; i++)
            {
                var row = new GameObject("Ground row " + i).transform; row.SetParent(transform); row.localPosition = new Vector3(0,0,StartZ + i * RowSpacing);
                var ground = GameObject.CreatePrimitive(PrimitiveType.Cube); ground.name = "Ground"; ground.transform.SetParent(row, false);
                ground.transform.localPosition = new Vector3(0,-.2f,0); ground.transform.localScale = new Vector3(100,.4f,RowSpacing + .02f);
                ground.GetComponent<Renderer>().sharedMaterial = groundMaterial; Destroy(ground.GetComponent<Collider>());
                rows.Add(row);
            }
            ReloadTable();
        }
        void Start() => Initialize();
        public bool ReloadTable()
        {
            try
            {
                if (!context || !rulesTable || !catalog) throw new InvalidOperationException("Assign Context, Rules Table and Catalog.");
                var parsed = SpawnRules.Parse(rulesTable.text); var resolved = catalog.Resolve();
                foreach (var rule in parsed) if (!resolved.ContainsKey(rule.PrefabId)) throw new FormatException("Unknown prefab_id: " + rule.PrefabId);
                // Commit only after the entire table passes validation.
                rules = parsed; prefabs = resolved; Rebuild(); Status = "Loaded " + rules.Count + " rules"; return true;
            }
            catch (Exception e) { Status = e.Message; Debug.LogError("OnAir: " + Status, this); return false; }
        }
        public void Rebuild()
        {
            if (rules == null) return;
            random = new System.Random(seed);
            CountEligible();
            for (int i = 0; i < rows.Count; i++) { rows[i].localPosition = new Vector3(0,0,StartZ+i*RowSpacing); Populate(rows[i]); }
        }
        void Update()
        {
            if (!context || context.paused || rules == null) return;
            foreach (var row in rows)
            {
                row.localPosition += Vector3.back * (Mathf.Max(0,scrollSpeed) * Mathf.Min(Time.deltaTime,.1f));
                if (row.localPosition.z < StartZ - RowSpacing)
                {
                    row.localPosition += Vector3.forward * (RowSpacing * RowCount); Populate(row);
                }
            }
        }
        void Populate(Transform row)
        {
            CountEligible();
            for (int i = row.childCount - 1; i >= 1; i--)
            {
                var instance = row.GetChild(i).gameObject; var source = sources[instance]; sources.Remove(instance);
                instance.SetActive(false); instance.transform.SetParent(poolRoot, false); pool[source].Push(instance);
            }
            for (int lane = -6; lane <= 6; lane++)
            {
                if (Math.Abs(lane) < 1 || random.NextDouble() < .24) continue;
                var rule = SpawnRules.Pick(rules,context.biome,context.weather,context.period,random); if (rule == null) continue;
                var prefab = prefabs[rule.PrefabId]; if (!pool.TryGetValue(prefab, out var stack)) pool.Add(prefab,stack = new Stack<GameObject>());
                var building = stack.Count > 0 ? stack.Pop() : Instantiate(prefab);
                sources.Add(building,prefab); building.name = rule.Id; building.transform.SetParent(row,false);
                building.transform.localPosition = new Vector3(lane * 4f + ((float)random.NextDouble()-.5f)*.6f,0,((float)random.NextDouble()-.5f)*.4f);
                building.transform.localRotation = Quaternion.identity;
                building.transform.localScale = prefab.transform.localScale * Mathf.Lerp(rule.MinScale,rule.MaxScale,(float)random.NextDouble());
                building.SetActive(true);
            }
        }
        void CountEligible()
        {
            EligibleCount = 0;
            foreach (var rule in rules) if (rule.Weight > 0 && rule.Matches(context.biome, context.weather, context.period)) EligibleCount++;
        }
    }
}
