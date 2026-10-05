using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed class BuildingData
    {
        public IReadOnlyList<SpawnRule> Rules {get;}
        public IReadOnlyDictionary<string,GameObject> Prefabs {get;}
        readonly Dictionary<GameObject,BuildingFootprint> footprints=new Dictionary<GameObject,BuildingFootprint>();
        public BuildingFootprint Footprint(GameObject prefab){if(!footprints.TryGetValue(prefab,out var fp))footprints[prefab]=fp=prefab.GetComponent<BuildingFootprint>();return fp;}
        public BuildingData(string csv,BuildingCatalog catalog)
        {
            Rules=BuildingRules.Parse(csv);Prefabs=catalog.Resolve();
            foreach(var row in Rules)
                if(!Prefabs.TryGetValue(row.PrefabId,out var prefab)||!prefab.GetComponent<BuildingFootprint>()||row.MinScale!=1||row.MaxScale!=1)
                    throw new InvalidOperationException("Building "+row.Id+": missing footprint/prefab or unsupported lot scale.");
            if(AircraftHeightPreview.Enabled)
            {
                var eligible=new List<SpawnRule>();
                foreach(var row in Rules)
                {
                    var prefab=Prefabs[row.PrefabId];
                    float top=prefab.GetComponent<BuildingFootprint>().height;
                    foreach(var renderer in prefab.GetComponentsInChildren<Renderer>(true))
                        top=Mathf.Max(top,renderer.bounds.max.y-prefab.transform.position.y);
                    if(top<=AircraftHeightPreview.MaximumWorldHeight)eligible.Add(row);
                }
                Rules=eligible;
            }
        }
    }
}
