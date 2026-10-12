using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Owns distance and the itinerary. Weather and rendering never advance or rebuild the itinerary.
    public sealed class JourneyController : MonoBehaviour
    {
        // A leg is one 64-unit floating-origin segment of the continuous world.
        public sealed class Leg { public int index,seed; }
        public GameSession context;
        public WorldProfile profile;
        public float MetersPerSegment=>profile.segmentMeters;
        // Country/region themes from themes.csv. The theme is part of the world identity, like the seed.
        [NonSerialized] public ThemeRule[] themes;
        public ThemeRule Theme{get;private set;}
        public float[] ZoneWeights=>Theme.zoneWeights;
        public int seed=2409;
        public static int FreshSeed(int previous)
        {
            int next;do{next=BitConverter.ToInt32(Guid.NewGuid().ToByteArray(),0)&int.MaxValue;}while(next==previous);return next;
        }
        public int TeleportRevision {get;private set;}
        public double TotalMeters {get;private set;}
        public double SegmentMeters {get;private set;}
        public int Revision {get;private set;}
        public Leg Current => legs.Count>0?legs[0]:null;
        public float Progress=>Current==null?0:(float)(SegmentMeters/MetersPerSegment);
        readonly List<Leg> legs=new List<Leg>();
        RouteRandom routeRandom;
        public event Action BeforeNewSeed,JourneyChanged;
        // An explicit small RNG state keeps restore constant-time even after a long flight.
        sealed class RouteRandom
        {
            uint state;
            public int State=>unchecked((int)state);
            public RouteRandom(int value,bool restored=false){state=unchecked((uint)value);if(!restored)state^=0x9e3779b9u;if(state==0)state=1;}
            uint Step(){state^=state<<13;state^=state>>17;state^=state<<5;return state;}
            public int Next()=>(int)(Step()&int.MaxValue);
            public double NextDouble()=>Step()/4294967296d;
        }
        public JourneyState CaptureState()
        {
            Initialize();var saved=new LegState[legs.Count];
            for(int i=0;i<legs.Count;i++){var leg=legs[i];saved[i]=new LegState{index=leg.index,seed=leg.seed};}
            return new JourneyState{seed=seed,randomState=routeRandom.State,theme=Theme.id,totalMeters=TotalMeters,segmentMeters=SegmentMeters,legs=saved};
        }
        public void RestoreState(JourneyState saved)
        {
            Theme=Array.Find(themes,t=>t.id==saved.theme)??throw new InvalidOperationException("Saved theme is missing.");
            seed=saved.seed;TotalMeters=saved.totalMeters;SegmentMeters=saved.segmentMeters;
            routeRandom=new RouteRandom(saved.randomState,true);legs.Clear();
            foreach(var leg in saved.legs)legs.Add(new Leg{index=leg.index,seed=leg.seed});
            Revision++;TeleportRevision++;
        }
        public void Initialize()
        {
            if(Current!=null)return;
            if(!context||themes==null||themes.Length==0||!profile||!profile.kit||!(profile.segmentMeters>0)||float.IsInfinity(profile.segmentMeters))throw new InvalidOperationException("Journey needs context and a valid world profile.");
            if(Theme==null||Array.IndexOf(themes,Theme)<0)Theme=themes[0];
            routeRandom=new RouteRandom(seed);
            legs.Add(Create(0));EnsureLookahead();
        }
        Leg Create(int index)=>new Leg{index=index,seed=routeRandom.Next()};
        void EnsureLookahead(){while(legs.Count<3)legs.Add(Create(legs[legs.Count-1].index+1));}
        public void Advance(double distance)
        {
            Initialize();if(context.SimulationPaused||distance<=0||double.IsNaN(distance)||double.IsInfinity(distance))return;
            TotalMeters+=distance;SegmentMeters+=distance;
            while(SegmentMeters>=MetersPerSegment){SegmentMeters-=MetersPerSegment;legs.RemoveAt(0);EnsureLookahead();}
        }
        public void JumpForwardWorld(float worldZ)
        {
            Initialize();float current=(Current.index+Progress)*64;if(worldZ<=current)return;
            bool paused=context.paused;var weather=context.weather;context.paused=false;Advance((worldZ-current)*MetersPerSegment/64);context.paused=paused;context.SetWeather(weather);TeleportRevision++;JourneyChanged?.Invoke();
        }
        public void CycleWeather()
        {
            Initialize();if(context.automaticWeather){context.automaticWeather=false;context.SetWeather(Weather.Clear);return;}if(context.weather==Weather.Sandstorm){context.automaticWeather=true;return;}context.SetWeather(context.weather==Weather.Clear?Weather.Cloudy:context.weather==Weather.Cloudy?Weather.Rain:context.weather==Weather.Rain?Weather.HeavyRain:context.weather==Weather.HeavyRain?Weather.Snow:context.weather==Weather.Snow?Weather.Sandstorm:Weather.Clear);
        }
        // Takes effect on the next Restart; a theme change always starts a new world.
        public void SelectTheme(ThemeRule theme){if(theme==null||Array.IndexOf(themes,theme)<0)throw new ArgumentException("Unknown theme.");Theme=theme;}
        // Table reload: keep the same theme by id, or fall back to the first theme if it was removed.
        public void ReplaceThemes(ThemeRule[] next){var id=Theme?.id;themes=next;Theme=Array.Find(next,t=>t.id==id)??next[0];}
        public void NewSeed()=>Restart(FreshSeed(seed));
        public void Restart(int value){BeforeNewSeed?.Invoke();seed=value;legs.Clear();TotalMeters=SegmentMeters=0;Revision++;TeleportRevision++;Initialize();JourneyChanged?.Invoke();}
    }
}

