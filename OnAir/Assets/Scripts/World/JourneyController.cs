using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Owns distance and the itinerary. Weather and rendering never advance or rebuild the itinerary.
    public sealed class JourneyController : MonoBehaviour
    {
        public sealed class Leg { public int index,seed; public TerrainDefinition terrain; public Weather entryWeather; public DayPeriod entryPeriod; }
        public GameSession context;
        [NonSerialized] public TerrainDefinition[] terrains;
        public int seed=2409;
        public int TeleportRevision {get;private set;}
        public double TotalMeters {get;private set;}
        public double SegmentMeters {get;private set;}
        public int Revision {get;private set;}
        public Leg Current => legs.Count>0?legs[0]:null;
        public float Progress=>Current==null?0:(float)(SegmentMeters/Current.terrain.distanceMeters);
        readonly List<Leg> legs=new List<Leg>();
        System.Random routeRandom,weatherRandom;
        public void Initialize()
        {
            if(Current!=null)return;
            if(!context||terrains==null||terrains.Length==0)throw new InvalidOperationException("Journey needs context and terrains.");
            var ids=new HashSet<string>();
            foreach(var t in terrains)
                if(t==null||!ids.Add(t.id)||string.IsNullOrWhiteSpace(t.id)||!t.profile||!t.profile.kit||!t.profile.ground||t.distanceMeters<=0||float.IsNaN(t.distanceMeters)||float.IsInfinity(t.distanceMeters)||t.selectionWeight<=0||float.IsNaN(t.selectionWeight)||t.weather==null||!Array.Exists(t.weather,w=>w.weight>0))throw new InvalidOperationException("Invalid terrain profile.");
            routeRandom=new System.Random(seed);weatherRandom=new System.Random(seed^9173);
            var first=Array.Find(terrains,t=>t.biome==context.biome)??terrains[0];
            legs.Add(Create(first,0));EnsureLookahead();EnterCurrent();
        }
        Leg Create(TerrainDefinition terrain,int index)=>new Leg{terrain=terrain,index=index,seed=routeRandom.Next(),entryWeather=terrain.PickWeather(weatherRandom),entryPeriod=context.period};
        void EnsureLookahead()
        {
            while(legs.Count<3)
            {
                var last=legs[legs.Count-1];double total=0;foreach(var t in terrains)if(terrains.Length==1||t!=last.terrain)total+=t.selectionWeight;
                double ticket=routeRandom.NextDouble()*total;TerrainDefinition chosen=null;
                foreach(var t in terrains)if(terrains.Length==1||t!=last.terrain){ticket-=t.selectionWeight;if(ticket<0){chosen=t;break;}}
                legs.Add(Create(chosen??terrains[0],last.index+1));
            }
        }
        public Leg Ahead(int offset){Initialize();return legs[offset];}
        void EnterCurrent()=>context.EnterTerrain(Current.terrain.biome,Current.entryWeather);
        public void Advance(double distance)
        {
            Initialize();if(context.paused||distance<=0||double.IsNaN(distance)||double.IsInfinity(distance))return;
            TotalMeters+=distance;SegmentMeters+=distance;
            while(SegmentMeters>=Current.terrain.distanceMeters){SegmentMeters-=Current.terrain.distanceMeters;legs.RemoveAt(0);EnsureLookahead();EnterCurrent();}
        }
        public void NextTerrain(){Initialize();TotalMeters+=Current.terrain.distanceMeters-SegmentMeters;SegmentMeters=0;legs.RemoveAt(0);EnsureLookahead();EnterCurrent();TeleportRevision++;}
        public void CycleWeather()
        {
            Initialize();var allowed=Array.FindAll(Current.terrain.weather,w=>w.weight>0);int i=Array.FindIndex(allowed,w=>w.weather==context.weather);context.SetWeather(allowed[(i+1)%allowed.Length].weather);
        }
        public void NewSeed(){seed++;legs.Clear();TotalMeters=SegmentMeters=0;Revision++;TeleportRevision++;Initialize();}
        public void ReplaceTerrains(TerrainDefinition[] definitions)
        {
            terrains=definitions;legs.Clear();TotalMeters=SegmentMeters=0;Revision++;TeleportRevision++;Initialize();
        }
    }
}
