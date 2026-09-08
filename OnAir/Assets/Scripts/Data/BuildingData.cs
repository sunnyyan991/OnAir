using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed class BuildingData
    {
        public IReadOnlyList<SpawnRule> Rules {get;}
        public IReadOnlyDictionary<string,GameObject> Prefabs {get;}
        public BuildingData(string csv,BuildingCatalog catalog)
        {
            Rules=BuildingRules.Parse(csv);Prefabs=catalog.Resolve();
            foreach(var row in Rules)
                if(!Prefabs.TryGetValue(row.PrefabId,out var prefab)||!prefab.GetComponent<BuildingFootprint>()||row.MinScale!=1||row.MaxScale!=1)
                    throw new InvalidOperationException("Building "+row.Id+": missing footprint/prefab or unsupported lot scale.");
        }
    }
}
