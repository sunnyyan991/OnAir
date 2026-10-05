using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Asset placement plans are separate from geography and from instantiated scene objects.
    public sealed class WorldPopulationCache
    {
        public struct Building {public GameObject prefab;public Rect bounds;public int rotation;public bool tint;public Color color;}
        public sealed class Region
        {
            public readonly List<Building> buildings=new List<Building>();public readonly List<Rect> roads=new List<Rect>();public int choices,singleOptions,repeats,forcedRepeats;
            Dictionary<Vector2Int,List<Building>> spatial;
            public List<Building> Near(Vector2 point)
            {
                if(spatial==null){spatial=new Dictionary<Vector2Int,List<Building>>();foreach(var b in buildings)
                    for(int z=Mathf.FloorToInt((b.bounds.yMin-2.6f)/16);z<=Mathf.FloorToInt((b.bounds.yMax+2.6f)/16);z++)
                    for(int x=Mathf.FloorToInt((b.bounds.xMin-2.6f)/16);x<=Mathf.FloorToInt((b.bounds.xMax+2.6f)/16);x++)
                    {var key=new Vector2Int(x,z);if(!spatial.TryGetValue(key,out var list))spatial[key]=list=new List<Building>();list.Add(b);}}
                spatial.TryGetValue(new Vector2Int(Mathf.FloorToInt(point.x/16),Mathf.FloorToInt(point.y/16)),out var found);return found;
            }
        }
        public readonly FacadePalette palette=new FacadePalette();
        // A cache belongs to one world revision. Coordinate keys survive geography-cache eviction.
        readonly Dictionary<Vector2Int,Region> entries=new Dictionary<Vector2Int,Region>();
        readonly Queue<Vector2Int> order=new Queue<Vector2Int>();
        public int Hits{get;private set;}public int Misses{get;private set;}public int Builds{get;private set;}
        public bool TryGet(ContinuousWorldPlan.Region key,out Region value){bool hit=entries.TryGetValue(key.coordinates,out value);if(hit)Hits++;else Misses++;return hit;}
        public void Add(ContinuousWorldPlan.Region key,Region value){if(!entries.ContainsKey(key.coordinates))order.Enqueue(key.coordinates);entries[key.coordinates]=value;Builds++;while(order.Count>256)entries.Remove(order.Dequeue());}
        public PopulationState[] CaptureState(BuildingData data,HashSet<Vector2Int> needed)
        {
            var saved=new List<PopulationState>();
            foreach(var pair in entries)
            {
                if(!needed.Contains(pair.Key))continue;
                var region=pair.Value;var buildings=new PlacementState[region.buildings.Count];
                for(int i=0;i<buildings.Length;i++){var b=region.buildings[i];buildings[i]=new PlacementState{buildingId=data.SaveId(b.prefab),bounds=b.bounds,rotation=b.rotation,tint=b.tint,color=b.color};}
                saved.Add(new PopulationState{coordinates=pair.Key,buildings=buildings,roads=region.roads.ToArray()});
            }
            saved.Sort((a,b)=>a.coordinates.y==b.coordinates.y?a.coordinates.x.CompareTo(b.coordinates.x):a.coordinates.y.CompareTo(b.coordinates.y));
            return saved.ToArray();
        }
        public void RestoreState(PopulationState[] saved,ContinuousWorldPlan plan,BuildingData data)
        {
            foreach(var item in saved)
            {
                var region=new Region();region.roads.AddRange(item.roads);
                foreach(var b in item.buildings)
                {
                    if(!data.TryResolveSaveId(b.buildingId,out var prefab))throw new System.InvalidOperationException("Saved building ID is missing: "+b.buildingId);
                    region.buildings.Add(new Building{prefab=prefab,bounds=b.bounds,rotation=b.rotation,tint=b.tint,color=b.color});
                }
                Add(plan.BuildRegion(item.coordinates.x,item.coordinates.y),region);
            }
        }
    }
}
