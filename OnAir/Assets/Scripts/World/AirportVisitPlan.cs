using System.Collections.Generic;
using UnityEngine;

namespace OnAir
{
    // Airport is a City district. One visit per ~25 ordinary ecology sections,
    // plus the airport's own length; coordinate-based and independent of tile order.
    public sealed class AirportVisitPlan
    {
        public const float Interval = 14400, ApproachLength = 240;
        public const float BuildingGap = 3, NeighbourhoodMargin = 96, AccessSearchRadius = 192;
        public const float AccessFallbackRadius = 256;
        public static readonly Rect Fence = new Rect(-45,-165,210,330);
        public sealed class Site
        {
            public int id, runway, bay;
            public bool reverse;
            public float start, threshold, length, exit, runwayX, side;
            public Vector3 assetPosition;
            public Quaternion assetRotation;
            public Rect bounds;
            public Rect CoreBounds => TransformRect(this,Fence);
            public Vector3 Entrance => SmallAirportNetwork.World(this,new Vector3(165,0,-54));
            // Keep an elevated view over the threshold, then use the runway's
            // first third for descent and a substantial middle section for rollout.
            public float LandingZ => threshold + 50;
            public float TurnoffZ => SmallAirportNetwork.World(this,SmallAirportNetwork.RunwayPoint(runway,0,SmallAirportNetwork.ExitOffset(runway,reverse))).z;
            public Vector3 Hangar => SmallAirportNetwork.World(this,SmallAirportNetwork.Bays[bay].centre);
            public Vector3 HangarFront => SmallAirportNetwork.World(this,SmallAirportNetwork.Bays[bay].Front);
            public Vector3 HangarForward => assetRotation*SmallAirportNetwork.Bays[bay].forward;
            public Vector3 Departure => SmallAirportNetwork.Departure(this);
            public string RunwayName => reverse ? "30" : "12";
        }
        static readonly Vector3[] centres = SmallAirportNetwork.RunwayCentres;
        static readonly float[] lengths = SmallAirportNetwork.RunwayLengths, angles = SmallAirportNetwork.RunwayAngles;
        readonly Dictionary<int,Site> sites = new Dictionary<int,Site>();
        readonly int seed;
        readonly float slope;
        readonly AirportFlightLayout layout;
        public AirportVisitPlan(int seed,float slope=0) { this.seed=seed;this.slope=slope;layout=Resources.Load<AirportFlightLayout>("Airport/FlightLayout"); }
        public bool Available => layout && layout.airport;
        public static Rect Expand(Rect rect,float margin)=>Rect.MinMaxRect(rect.xMin-margin,rect.yMin-margin,rect.xMax+margin,rect.yMax+margin);
        static Rect TransformRect(Site site,Rect source)
        {
            var a=SmallAirportNetwork.World(site,new Vector3(source.xMin,0,source.yMin));
            var b=SmallAirportNetwork.World(site,new Vector3(source.xMax,0,source.yMax));
            return Rect.MinMaxRect(Mathf.Min(a.x,b.x),Mathf.Min(a.z,b.z),Mathf.Max(a.x,b.x),Mathf.Max(a.z,b.z));
        }
        public static bool RunwayClearance(Site site,Vector3 global,float margin)
        {
            var source=Quaternion.Inverse(site.assetRotation)*(global-site.assetPosition);
            for(int i=0;i<centres.Length;i++)
            {
                var p=Quaternion.Euler(0,-angles[i],0)*(source-centres[i]);
                if(Mathf.Abs(p.x)<25+margin&&Mathf.Abs(p.z)<lengths[i]/2+40+margin)return true;
            }
            return false;
        }
        int Hash(int n,int salt) { unchecked { uint h=(uint)(seed ^ salt);h^=(uint)n*374761393u;h=(h^(h>>13))*1274126177u;return (int)((h^(h>>16))&0x7fffffff); } }
        public Site Get(int id)
        {
            if(!Available||id<0)return null;
            if(sites.TryGetValue(id,out var saved))return saved;
            int runway=0;
            bool reverse=(id+Hash(0,18373))%2==0;
            float start=12000+id*Interval+Hash(id,18379)%961-480;
            float threshold=start+ApproachLength;
            var rotation=Quaternion.Euler(0,-angles[runway]+(reverse?180:0),0);
            float runwayX=slope*threshold+layout.cruiseVisualOffset.x;
            var position=new Vector3(runwayX,0,threshold+lengths[runway]/2)-rotation*centres[runway];
            var source=layout.bounds;Vector3 low=Vector3.one*float.MaxValue,high=Vector3.one*float.MinValue;
            for(int x=-1;x<=1;x+=2)for(int z=-1;z<=1;z+=2)
            {var p=position+rotation*(source.center+Vector3.Scale(source.extents,new Vector3(x,0,z)));low=Vector3.Min(low,p);high=Vector3.Max(high,p);}
            // Service districts are authored in the airport's local coordinates.
            var centre=position+rotation*SmallAirportNetwork.Bays[0].centre;
            float side=centre.x<runwayX?1:-1;
            var site=new Site { id=id,runway=runway,reverse=reverse,start=start,threshold=threshold,length=lengths[runway],exit=threshold+lengths[runway]+350,runwayX=runwayX,side=side,assetPosition=position,assetRotation=rotation,
                bounds=Rect.MinMaxRect(Mathf.Min(low.x,runwayX-70)-24,Mathf.Min(low.z,start)-24,Mathf.Max(high.x,runwayX+70)+24,Mathf.Max(high.z,threshold+lengths[runway]+50)+24) };
            site.bay=SmallAirportNetwork.SelectBay(site);
            // Loading extent can include a bounded connector search; geography
            // reservations below deliberately use the fence, never this extent.
            var entrance=site.Entrance;var loaded=site.bounds;
            site.bounds=Rect.MinMaxRect(Mathf.Min(loaded.xMin,entrance.x-AccessFallbackRadius-8),Mathf.Min(loaded.yMin,entrance.z-AccessFallbackRadius-8),Mathf.Max(loaded.xMax,entrance.x+AccessFallbackRadius+8),Mathf.Max(loaded.yMax,entrance.z+AccessFallbackRadius+8));
            if(sites.Count>12)sites.Clear();sites[id]=site;return site;
        }
        public Site Near(float z,float margin=0)
        {
            if(!Available)return null;
            int group=Mathf.FloorToInt((z-12000)/Interval);
            for(int i=Mathf.Max(0,group-1);i<=group+1;i++){var s=Get(i);if(s!=null&&z>=s.bounds.yMin-margin&&z<=Mathf.Max(s.bounds.yMax,s.exit)+margin)return s;}
            return null;
        }
        public Site Next(float z)
        {
            if(!Available)return null;
            int group=Mathf.Max(0,Mathf.FloorToInt((z-12000)/Interval)-1);
            for(int i=group;i<group+5;i++){var s=Get(i);if(s.start>z+128)return s;}
            return null;
        }
        public bool Reserved(float x,float z,float margin=0)
        {
            var s=Near(z,margin);if(s==null)return false;
            var p=SmallAirportNetwork.Source(s,new Vector3(x,0,z));
            return Expand(Fence,margin).Contains(new Vector2(p.x,p.z));
        }
        public bool Neighbourhood(float x,float z)
        {
            var s=Near(z,NeighbourhoodMargin);if(s==null)return false;
            var p=SmallAirportNetwork.Source(s,new Vector3(x,0,z));
            return Expand(Fence,NeighbourhoodMargin).Contains(new Vector2(p.x,p.z))&&!Fence.Contains(new Vector2(p.x,p.z));
        }
        public bool Neighbourhood(Rect lot)
        {
            var s=Near(lot.center.y,NeighbourhoodMargin+lot.height*.5f);
            return s!=null&&Expand(s.CoreBounds,NeighbourhoodMargin).Overlaps(lot);
        }
        public bool EntirelyReserved(Rect lot,float margin=0)
        {
            var s=Near(lot.center.y,lot.height*.5f+margin);if(s==null)return false;
            var core=Expand(s.CoreBounds,margin);
            return lot.xMin>=core.xMin&&lot.xMax<=core.xMax&&lot.yMin>=core.yMin&&lot.yMax<=core.yMax;
        }
        public bool ApproachClear(float x,float z,float margin=0)
        {
            var s=Near(z,64);return s!=null&&z>=s.start-40-margin&&z<=s.threshold+margin&&Mathf.Abs(x-s.runwayX)<=8+margin;
        }
        public int BuildingFloorLimit(Rect lot)
        {
            var s=Near(lot.center.y,NeighbourhoodMargin+lot.height*.5f);if(s==null)return 20;
            if(Expand(s.CoreBounds,BuildingGap).Overlaps(lot))return 0;
            // Keep only the aircraft's narrow final-approach corridor empty.
            // Adjacent houses remain possible instead of clearing a 220m strip.
            var flight=new Rect(s.runwayX-8,s.start-40,16,s.threshold-s.start+40);
            if(flight.Overlaps(lot))return 0;
            return Expand(s.CoreBounds,NeighbourhoodMargin).Overlaps(lot)?4:20;
        }
        public bool District(float z,out float begin,out float end)
        {
            var s=Near(z);begin=end=0;if(s==null||z<s.start-64||z>s.threshold+s.length+80)return false;
            begin=s.start-64;end=s.threshold+s.length+80;return true;
        }
    }
}
