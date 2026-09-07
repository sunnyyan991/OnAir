using System;
using System.Collections.Generic;
using UnityEngine;

namespace OnAir.Prototype
{
    [CreateAssetMenu(menuName = "OnAir/Building Catalog")]
    public sealed class BuildingCatalog : ScriptableObject
    {
        [Serializable] public struct Entry { public string id; public GameObject prefab; }
        public Entry[] entries = Array.Empty<Entry>();
        public Dictionary<string, GameObject> Resolve()
        {
            var map = new Dictionary<string, GameObject>(StringComparer.OrdinalIgnoreCase);
            foreach (var entry in entries)
            {
                if (string.IsNullOrWhiteSpace(entry.id) || entry.prefab == null || map.ContainsKey(entry.id))
                    throw new InvalidOperationException("Building catalog contains a missing prefab or empty/duplicate id.");
                map.Add(entry.id, entry.prefab);
            }
            return map;
        }
    }
}
