using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    public sealed class TerrainStreamer : MonoBehaviour
    {
        public const float SegmentLength=64;
        public JourneyController journey;
        public BuildingData buildings;
        public int ActiveBuildingCount {get;private set;}
        readonly Dictionary<int,GameObject> chunks=new Dictionary<int,GameObject>();
        readonly List<int> expired=new List<int>(4);
        int revision=-1;
        public int GenerationCount {get;private set;}
        public IEnumerable<GameObject> Chunks=>chunks.Values;
        void Start()=>Refresh();
        void Update()=>Refresh();
        void ClearChunks(){foreach(var item in chunks.Values){item.SetActive(false);Destroy(item);}chunks.Clear();}
        public void RebuildAll(){ClearChunks();revision=-1;Refresh();}
        public void Refresh()
        {
            journey.Initialize();
            if(revision!=journey.Revision){ClearChunks();revision=journey.Revision;}
            int current=journey.Current.index;
            if(chunks.Count==0)Create(new JourneyController.Leg{index=current-1,seed=journey.Current.seed^77,terrain=journey.Current.terrain,entryWeather=journey.Current.entryWeather,entryPeriod=journey.Current.entryPeriod});
            for(int i=0;i<3;i++){var leg=journey.Ahead(i);if(!chunks.ContainsKey(leg.index))Create(leg);}
            expired.Clear();ActiveBuildingCount=0;
            foreach(var pair in chunks)
            {
                if(pair.Key<current-1||pair.Key>current+2){expired.Add(pair.Key);continue;}
                // Ground stays still during flight. At boundaries the whole world rebases by one segment.
                pair.Value.transform.localPosition=new Vector3(0,0,(pair.Key-current)*SegmentLength);
                // Count is stored by the layout; do not search the full hierarchy each frame.
                ActiveBuildingCount+=pair.Value.GetComponent<BlockGenerator>().BuildingCount;
            }
            foreach(int key in expired){chunks[key].SetActive(false);Destroy(chunks[key]);chunks.Remove(key);}
        }
        void Create(JourneyController.Leg leg)
        {
            var terrain=leg.terrain;var go=new GameObject("Terrain "+leg.index+" / "+terrain.biome);go.transform.SetParent(transform,false);
            var layout=go.AddComponent<BlockGenerator>();layout.kit=terrain.profile.kit;layout.buildings=buildings;layout.seed=leg.seed;layout.biome=terrain.biome;layout.weather=leg.entryWeather;layout.period=leg.entryPeriod;layout.buildingDensity=terrain.buildingDensity;layout.streamingRoads=true;layout.Rebuild();
            var ground=GameObject.CreatePrimitive(PrimitiveType.Cube);ground.name="Terrain base";ground.transform.SetParent(go.transform,false);ground.transform.localPosition=new Vector3(0,-.7f,0);ground.transform.localScale=new Vector3(192,1,64.02f);ground.GetComponent<Renderer>().sharedMaterial=terrain.profile.ground;Destroy(ground.GetComponent<Collider>());
            if(terrain.profile.scenery)for(int i=0;i<5;i++){var prop=Instantiate(terrain.profile.scenery,go.transform);prop.transform.localPosition=new Vector3(i%2==0?-38:38,0,-24+i*12);}
            chunks.Add(leg.index,go);GenerationCount++;
        }
    }
}
