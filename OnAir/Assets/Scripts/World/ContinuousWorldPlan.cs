using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Global coordinates define geography; streaming slices are only ownership containers.
    public sealed class ContinuousWorldPlan
    {
        public const int RegionSize=128;
        public const int GeneratorVersion=20;
        public readonly int seed;
        public enum EcologyZone { City, Town, CoastalTown, Forest, RiverPlain, Hills, Ocean, Airport }
        const float EcologyCycle=2400,EcologyBlend=48;
        sealed class ZoneSchedule { public EcologyZone[] zones=new EcologyZone[5]; public float[] ends=new float[5]; }
        readonly Dictionary<long,Land> landSamples=new Dictionary<long,Land>();
        readonly Dictionary<long,float> waterSamples=new Dictionary<long,float>();
        readonly Dictionary<long,float> heightSamples=new Dictionary<long,float>();
        static bool SampleKey(float x,float z,out long key){int ix=Mathf.RoundToInt(x*4),iz=Mathf.RoundToInt(z*4);key=((long)ix<<32)|(uint)iz;return Mathf.Abs(ix-x*4)<.0001f&&Mathf.Abs(iz-z*4)<.0001f;}
        readonly Dictionary<int,ZoneSchedule> schedules=new Dictionary<int,ZoneSchedule>();
        readonly Dictionary<long,Region> regionCache=new Dictionary<long,Region>();
        readonly Dictionary<long,bool> farms=new Dictionary<long,bool>();
        readonly Dictionary<Rect,bool> dryLots=new Dictionary<Rect,bool>();
        public enum Land { Urban, Park, Forest, Bank, Water }
        public sealed class Parcel { public Rect bounds; public int district; }
        public sealed class Region { public Vector2Int coordinates;public readonly List<Rect> roads=new List<Rect>(); public readonly List<Vector4> diagonalRoads=new List<Vector4>();public readonly List<Parcel> parcels=new List<Parcel>(); }
        public readonly WorldHydrology hydrology;
        public readonly WorldCrossings crossings;
        public readonly WorldHighways highways;
        public readonly WorldUpperHighways upperHighways;
        public readonly WorldLayerHighways layerHighways;
        public readonly WorldGreenSpaces greenSpaces;
        public readonly AirportVisitPlan airports;
        public readonly float routeSlope;
        // Relative weight per EcologyZone, from the journey's theme. Shared by reference so caches can detect theme changes.
        public readonly float[] zoneWeights;
        public static readonly float[] DefaultZoneWeights={40,14,10,14,7,9,6,0};
        public readonly CityAssetLoadout cityAssets;
        readonly Dictionary<int,CityDistrict> cityDistrictCache=new Dictionary<int,CityDistrict>();
        readonly Dictionary<int,AirportAccessRoad> airportAccess=new Dictionary<int,AirportAccessRoad>();
        public ContinuousWorldPlan(int seed,float routeSlope=0,CityAssetLoadout cityAssets=null,float[] zoneWeights=null){this.seed=seed;this.routeSlope=routeSlope;this.cityAssets=cityAssets;this.zoneWeights=zoneWeights??DefaultZoneWeights;airports=new AirportVisitPlan(seed,routeSlope);hydrology=new WorldHydrology(this);crossings=new WorldCrossings(this);highways=new WorldHighways(this);upperHighways=new WorldUpperHighways(this);layerHighways=new WorldLayerHighways(this);greenSpaces=new WorldGreenSpaces(this);}
        public CityDistrict CityDistrictAt(float x,float z)
        {
            if(cityAssets==null)return CityDistrict.Legacy;
            if(airports.Reserved(x,z))return CityDistrict.Airport;
            if(airports.Neighbourhood(x,z))return CityDistrict.Residence;
            var zone=Zone(EcologyCoordinate(x,z),out float begin,out _);
            if(zone==EcologyZone.Airport)return CityDistrict.Airport;
            if(zone!=EcologyZone.City)return CityDistrict.Legacy;
            int key=Mathf.RoundToInt(begin*100);
            if(cityDistrictCache.TryGetValue(key,out var found))return found;
            for(int i=0;i<128;i++){if(Zone(begin-1,out float previous,out _)!=EcologyZone.City)break;begin=previous;}
            var district=Hash(Mathf.RoundToInt(begin*100),0,47131)%2==0?CityDistrict.CBD:CityDistrict.Residence;
            if(cityDistrictCache.Count>256)cityDistrictCache.Clear();cityDistrictCache[key]=district;return district;
        }
        public void TrimCaches(){if(landSamples.Count>250000)landSamples.Clear();if(waterSamples.Count>250000)waterSamples.Clear();if(heightSamples.Count>250000)heightSamples.Clear();if(regionCache.Count>256)regionCache.Clear();if(farms.Count>8192)farms.Clear();if(schedules.Count>64)schedules.Clear();hydrology.TrimCache();crossings.TrimCache();highways.TrimCache();upperHighways.TrimCache();}
        public int Hash(int x,int z,int salt=0){unchecked{uint h=(uint)(seed^salt);h^=(uint)x*374761393u;h=(h^(h>>13))*1274126177u;h^=(uint)z*668265263u;return (int)((h^(h>>16))&0x7fffffff);}}
        float Noise(float x,float z,float scale,int salt)=>Mathf.PerlinNoise(x/scale+(Hash(0,0,salt)%10000),z/scale+(Hash(1,1,salt)%10000));
        // Broad straight reaches linked by exact +/-45 degree segments in plan view.
        // Independent endpoints allow random access without replaying the river from its source.
        float AngularPath(float z,int interval,int salt,float origin,int choices)
        {
            int section=Mathf.FloorToInt(z/interval);float local=z-section*interval;
            float a=origin+32*(Hash(section,0,salt)%choices),b=origin+32*(Hash(section+1,0,salt)%choices);
            float length=Mathf.Abs(b-a),begin=(interval-length)*.5f;
            return a+Mathf.Sign(b-a)*Mathf.Clamp(local-begin,0,length);
        }
        public float River(float z)=>AngularPath(z,256,261,12,4);
        public float RiverWidth(float z)=>16+28*EcologyBlendAt(0,z).Weight(EcologyZone.RiverPlain);
        public float Coast(float z)=>AngularPath(z,512,671,156,3)-112*EcologyBlendAt(0,z).Weight(EcologyZone.CoastalTown);
        ZoneSchedule Schedule(int cycle)
        {
            if(schedules.TryGetValue(cycle,out var saved))return saved;
            var schedule=new ZoneSchedule();float sum=0;double total=0;foreach(float w in zoneWeights)total+=Mathf.Max(0,w);
            for(int i=0;i<5;i++)
            {
                // Weighted by theme; no fixed hourly quota or repeating biome order.
                // Weights summing to 100 reproduce the original integer thresholds exactly.
                double ticket=Hash(cycle,i,873)%100*total/100;var zone=EcologyZone.City;double cumulative=0;
                for(int z=0;z<zoneWeights.Length;z++){if(zoneWeights[z]<=0)continue;zone=(EcologyZone)z;cumulative+=zoneWeights[z];if(ticket<cumulative)break;}
                sum+=320+Hash(cycle,i,729)%321;schedule.zones[i]=zone;schedule.ends[i]=sum;
            }
            for(int i=0;i<5;i++)schedule.ends[i]=schedule.ends[i]/sum*EcologyCycle;
            schedule.ends[4]=EcologyCycle;
            // Rare regional airport: both neighbours already qualify as urban areas.
            // One eligible cycle in eight, never inserted into forest/ocean chains.
            if(!airports.Available&&Hash(cycle,0,47011)%8==0&&UrbanNeighbour(schedule.zones[0])&&UrbanNeighbour(schedule.zones[4]))
                for(int k=1;k<=3;k++)schedule.zones[k]=EcologyZone.Airport;
            schedules.Add(cycle,schedule);return schedule;
        }
        EcologyZone Zone(float z,out float begin,out float end)
        {
            int cycle=Mathf.FloorToInt(z/EcologyCycle);float local=z-cycle*EcologyCycle;var schedule=Schedule(cycle);int i=0;while(i<4&&local>=schedule.ends[i])i++;
            int first=i,last=i;if(schedule.zones[i]==EcologyZone.Airport){while(first>0&&schedule.zones[first-1]==EcologyZone.Airport)first--;while(last<4&&schedule.zones[last+1]==EcologyZone.Airport)last++;}
            begin=cycle*EcologyCycle+(first==0?0:schedule.ends[first-1]);end=cycle*EcologyCycle+schedule.ends[last];return schedule.zones[i];
        }
        static bool UrbanNeighbour(EcologyZone z)=>z==EcologyZone.City||z==EcologyZone.Town||z==EcologyZone.CoastalTown;
        public bool AirportAt(float z,out AirportGeometry.Site site)
        {
            site=default;if(airports.Available)return false;if(Zone(z,out float begin,out float end)!=EcologyZone.Airport)return false;
            site.bounds=new Rect(routeSlope*((begin+end)/2)-180,begin+32,360,end-begin-64);return true;
        }
        public bool AirportReserved(float x,float z,float margin=0)=>airports.Reserved(x,z,margin)||(AirportAt(z,out var a)&&x>=a.bounds.xMin-margin&&x<=a.bounds.xMax+margin&&z>=a.bounds.yMin-margin&&z<=a.bounds.yMax+margin);
        public EcologyZone ZoneRange(float z,out float begin,out float end)=>Zone(z,out begin,out end);
        public EcologyZone NaturalZoneAt(float x,float z)=>Zone(EcologyCoordinate(x,z),out _,out _);
        public float WaterDistance(float x,float z){if(AirportReserved(x,z))return 100;if(!SampleKey(x,z,out long key))return hydrology.Distance(x,z);if(waterSamples.TryGetValue(key,out float value))return value;return waterSamples[key]=hydrology.Distance(x,z);}
        public struct EcoBlend
        {
            public EcologyZone a,b;public float t;
            public float Weight(EcologyZone zone)=>(a==zone?1-t:0)+(b==zone?t:0);
        }
        float EcologyCoordinate(float x,float z){float s=z+40*Mathf.Sin(x/105f)+18*Mathf.Sin(x/43f);return Zone(z,out _,out _)==EcologyZone.Airport||Zone(s,out _,out _)==EcologyZone.Airport?z:s;}
        public EcologyZone ZoneAt(float x,float z)
        {
            if(airports.Reserved(x,z))return EcologyZone.Airport;
            if(airports.Neighbourhood(x,z))return EcologyZone.City;
            return Zone(EcologyCoordinate(x,z),out _,out _);
        }
        public EcoBlend EcologyBlendAt(float x,float z)
        {
            if(airports.Reserved(x,z))return new EcoBlend{a=EcologyZone.Airport,b=EcologyZone.Airport};
            if(airports.Neighbourhood(x,z))return new EcoBlend{a=EcologyZone.City,b=EcologyZone.Town,t=.3f};
            float s=EcologyCoordinate(x,z);var current=Zone(s,out float begin,out float end);
            if(s-begin<EcologyBlend)return new EcoBlend{a=Zone(begin-1,out _,out _),b=current,t=Mathf.SmoothStep(0,1,(s-begin+EcologyBlend)/(2*EcologyBlend))};
            if(end-s<EcologyBlend)return new EcoBlend{a=current,b=Zone(end+1,out _,out _),t=Mathf.SmoothStep(0,1,(s-end+EcologyBlend)/(2*EcologyBlend))};
            return new EcoBlend{a=current,b=current,t=0};
        }
        public Vector3 Ecology(float x,float z)
        {
            var e=EcologyBlendAt(x,z);return new Vector3(e.Weight(EcologyZone.City),e.Weight(EcologyZone.Town)+e.Weight(EcologyZone.CoastalTown),e.Weight(EcologyZone.Forest)+e.Weight(EcologyZone.Hills));
        }
        public float Height(float x,float z)
        {
            if(AirportReserved(x,z,8))return 0;
            // ComputeHeight is constant throughout a 4-unit terrain cell. Cache that cell,
            // not hundreds of independent quarter-unit building/road sample positions.
            long key=((long)Mathf.FloorToInt(x/4)<<32)|(uint)Mathf.FloorToInt(z/4);
            if(heightSamples.TryGetValue(key,out float value))return value;
            value=ComputeHeight(x,z);
            if(AircraftHeightPreview.Enabled)value=Mathf.Min(value,AircraftHeightPreview.MaximumWorldHeight);
            return heightSamples[key]=value;
        }
        float ComputeHeight(float x,float z)
        {
            // Shared block centers retain voxel silhouettes without independent per-cell noise.
            x=Mathf.Floor(x/4)*4+2;z=Mathf.Floor(z/4)*4+2;
            float water=WaterDistance(x,z);if(water<8)return 0;
            var ecology=EcologyBlendAt(x,z);float hills=ecology.Weight(EcologyZone.Hills),forest=ecology.Weight(EcologyZone.Forest);
            float mountain=0;int area=Mathf.FloorToInt(z/160);
            for(int i=area-2;(hills>0||ecology.Weight(EcologyZone.City)>0)&&i<=area+2;i++)
            {
                float cz=i*160+32+Hash(i,0,1941)%96,cx=routeSlope*cz-128+Hash(i,0,1943)%257;
                if(hills>0)
                {
                float radius=44+Hash(i,0,1949)%72;
                float wx=x+12*Mathf.Sin(z/23)+7*Mathf.Sin(z/53),wz=z+10*Mathf.Sin(x/29);
                float angle=(Hash(i,0,1953)%180)*Mathf.Deg2Rad,aspect=.55f+(Hash(i,0,1957)%140)/100f;
                float px=wx-cx,pz=wz-cz;
                float dx=(px*Mathf.Cos(angle)+pz*Mathf.Sin(angle))/radius,dz=(-px*Mathf.Sin(angle)+pz*Mathf.Cos(angle))/(radius*aspect);
                float cone=Mathf.Clamp01(1-Mathf.Sqrt(dx*dx+dz*dz)+(Noise(x,z,48,1961)-.5f)*.48f);
                mountain=Mathf.Max(mountain,cone*(44+Hash(i,0,1951)%24)*hills);
                }
                if(Hash(i,0,1987)%4==0){float knoll=Mathf.Clamp01(1-Vector2.Distance(new Vector2(x,z),new Vector2(cx,cz))/(28+Hash(i,0,1989)%29));mountain=Mathf.Max(mountain,Mathf.SmoothStep(0,1,knoll)*14*ecology.Weight(EcologyZone.City));}
            }
            if(forest>0)mountain=Mathf.Max(mountain,forest*12*Noise(x,z,62,481));
            mountain=Mathf.Max(mountain,Mathf.Min(12,water*.35f)*ecology.Weight(EcologyZone.Ocean));
            return Mathf.Floor(mountain*Mathf.SmoothStep(0,1,(water-8)/36)/4)*4;
        }
        public bool AllowsBuilding(Rect bounds,int floors,float height=0)
        {
            var ecology=Ecology(bounds.center.x,bounds.center.y);
            int airportLimit=airports.BuildingFloorLimit(bounds);
            return airportLimit>0&&floors<=Mathf.Min(airportLimit,ecology.y>ecology.x?4:20)
                &&(!airports.Neighbourhood(bounds)||height<=16)&&!AirportAccessOverlaps(bounds,.75f)&&Dry(bounds);
        }
        public int TreeSpacing(float x,float z)
        {
            float patch=Noise(x,z,36,2141);return patch<.37f?37:patch>.63f?5:11;
        }
        public bool Farmland(float x,float z)
        {
            if(AirportReserved(x,z,8))return false;
            int ix=Mathf.FloorToInt(x/24),iz=Mathf.FloorToInt(z/24);long key=((long)ix<<32)|(uint)iz;
            if(!farms.TryGetValue(key,out bool valid))
            {
                float cx=ix*24+12,cz=iz*24+12;valid=EcologyBlendAt(cx,cz).Weight(EcologyZone.Town)>.8f&&Hash(ix,iz,2153)%3==0;
                if(valid)foreach(var p in new[]{new Vector2(cx-10,cz-10),new Vector2(cx+10,cz-10),new Vector2(cx-10,cz+10),new Vector2(cx+10,cz+10)})if(WaterDistance(p.x,p.y)<12||Height(p.x,p.y)>0)valid=false;
                if(valid){var r=new Rect(cx-10,cz-10,20,20);var region=BuildRegion(Mathf.FloorToInt(cx/128),Mathf.FloorToInt(cz/128));foreach(var road in region.roads)if(road.Overlaps(r))valid=false;}
                farms[key]=valid;
            }
            float localX=x-ix*24,localZ=z-iz*24;return valid&&localX>=2&&localX<22&&localZ>=2&&localZ<22;
        }
        // Suitability metadata only: these do not yet reserve land or instantiate industrial models.
        public string WaterfrontUse(float x,float z)
        {
            if((ZoneAt(x,z)==EcologyZone.Forest||ZoneAt(x,z)==EcologyZone.Hills))return "NaturalBank";
            if(Mathf.Abs(x-Coast(z))<18&&Mathf.Abs(Coast(z-16)-Coast(z+16))<.01f)return "CoastalCargoCandidate";
            if(Mathf.Abs(Mathf.Abs(x-River(z))-RiverWidth(z))<18&&Mathf.Abs(River(z-16)-River(z+16))<.01f)return "RiverServiceCandidate";
            return "Promenade";
        }
        public Land Sample(float x,float z,bool reservations=true){if(!reservations||!SampleKey(x,z,out long key))return ComputeLand(x,z,reservations);if(landSamples.TryGetValue(key,out var land))return land;return landSamples[key]=ComputeLand(x,z,true);}
        Land ComputeLand(float x,float z,bool reservations)
        {
            if(AirportReserved(x,z))return Land.Park;
            float river=WaterDistance(x,z),coast=river;
            if(river<0||coast<0)return Land.Water;
            if(river<7||coast<7)return Land.Bank;
            if(Height(x,z)>.3f)return Land.Forest;
            if(ZoneAt(x,z)==EcologyZone.Ocean)return Land.Park;
            if(Farmland(x,z))return Land.Park;
            var ecology=Ecology(x,z);
            if(EcologyBlendAt(x,z).Weight(EcologyZone.RiverPlain)>.4f&&river<26)return Land.Park;
            if(ecology.z+Noise(x,z,80,158)*.3f>.65f)return Land.Forest;
            if(ecology.y>.25f&&Noise(x,z,48,186)>.62f-ecology.y*.12f)return Land.Park;
            float wilderness=Noise(x,z,170,53)+Mathf.Clamp01((-(x-routeSlope*z)-65)/140f)*.35f;
            if(wilderness>.76f)return Land.Forest;
            if(Noise(x,z,65,87)>.69f)return Land.Park;
            return Land.Urban;
        }
        public bool Dry(Rect r,bool reservations=true)
        {
            if(reservations&&dryLots.TryGetValue(r,out bool cached))return cached;
            bool dry=true;
            for(float z=r.yMin;dry&&z<=r.yMax+1;z+=2)for(float x=r.xMin;x<=r.xMax+1;x+=2)
                if(!Buildable(Mathf.Min(x,r.xMax),Mathf.Min(z,r.yMax),reservations)){dry=false;break;}
            dry=dry&&Buildable(r.xMax,r.yMax,reservations);
            if(reservations){if(dryLots.Count>60000)dryLots.Clear();dryLots[r]=dry;}
            return dry;
        }
        bool Buildable(float x,float z,bool reservations)=>Sample(x,z,reservations)==Land.Urban&&!DiagonalReserved(x,z)&&(!reservations||!crossings.Reserved(x,z)&&!highways.Reserved(x,z));
        public Region BuildRegion(int rx,int rz)
        {
            long key=((long)rx<<32)|(uint)rz;if(regionCache.TryGetValue(key,out var cached))return cached;
            var region=new Region{coordinates=new Vector2Int(rx,rz)};regionCache[key]=region;float x=rx*RegionSize,z=rz*RegionSize;
            // Shared collector edges use the same global position in either neighboring region.
            region.roads.Add(new Rect(x,z,4,128));region.roads.Add(new Rect(x,z,128,4));
            var random=new System.Random(Hash(rx,rz,131));
            Split(region,new Rect(x+4,z+4,124,124),random,0);
            return region;
        }
        public bool DiagonalReserved(float x,float z)
        {
            for(int rz=Mathf.FloorToInt((z-6)/128);rz<=Mathf.FloorToInt((z+6)/128);rz++)for(int rx=Mathf.FloorToInt((x-6)/128);rx<=Mathf.FloorToInt((x+6)/128);rx++)
                foreach(var line in BuildRegion(rx,rz).diagonalRoads)if(WorldCrossings.LinkDistance(new Vector2(x,z),line,7)<0)return true;return false;
        }
        void Split(Region region,Rect area,System.Random random,int depth)
        {
            float central=Noise(area.center.x,area.center.y,240,391);
            int district=central>.56f?2:central>.43f?1:0;
            if(Ecology(area.center.x,area.center.y).y>.5f)district=0;
            // Keep the existing road splitting decisions. Only building allocation changes.
            int allocation=cityAssets==null?district:cityAssets.ParcelDistrict(CityDistrictAt(area.center.x,area.center.y),Hash((int)(area.center.x*4),(int)(area.center.y*4),47137)/(float)int.MaxValue,district);
            bool finish=depth>=5||(depth>=2&&area.width<=65&&area.height<=65&&random.NextDouble()<(district==2?.42:.15));
            if(finish){region.parcels.Add(new Parcel{bounds=area,district=allocation});return;}
            bool vertical=area.width>area.height*1.25f||(area.width>=area.height*.8f&&random.Next(2)==0);
            float length=vertical?area.width:area.height;
            if(length<27){region.parcels.Add(new Parcel{bounds=area,district=allocation});return;}
            float road=depth<2?4:2.5f;
            float cut=Mathf.Round((length*(.3f+(float)random.NextDouble()*.4f))/2)*2;
            cut=Mathf.Clamp(cut,11,length-road-11);
            if(vertical){region.roads.Add(new Rect(area.x+cut,area.y,road,area.height));Split(region,new Rect(area.x,area.y,cut,area.height),random,depth+1);Split(region,new Rect(area.x+cut+road,area.y,length-cut-road,area.height),random,depth+1);}
            else{region.roads.Add(new Rect(area.x,area.y+cut,area.width,road));Split(region,new Rect(area.x,area.y,area.width,cut),random,depth+1);Split(region,new Rect(area.x,area.y+cut+road,area.width,length-cut-road),random,depth+1);}
        }
        public bool Bridge(float z)=>Mathf.Abs(z-Mathf.Round(z/128)*128-2)<2.01f;

        public sealed class AirportAccessRoad
        {
            public sealed class Span { public float begin,end; }
            public AirportVisitPlan.Site site;
            public readonly List<Vector2> points=new List<Vector2>();
            public readonly List<Span> bridges=new List<Span>();
            public Rect bounds,existingRoad;
            public float length;
            public bool fallback;
            public bool Connected=>points.Count>1;
            public float Height(float distance)
            {
                float height=Mathf.Lerp(.2f,.09f,Mathf.Clamp01(distance/4));
                foreach(var span in bridges)
                {
                    float rise=Mathf.Clamp01(Mathf.Min((distance-span.begin+12)/12,(span.end+12-distance)/12));
                    if(rise>0)height=Mathf.Max(height,Mathf.Lerp(.09f,.85f,Mathf.SmoothStep(0,1,rise)));
                }
                height=Mathf.Lerp(.09f,height,Mathf.SmoothStep(0,1,Mathf.Clamp01((length-distance)/16)));
                // The prefab's entrance is at .2m: never introduce a raised
                // bridge step at the fence when the stream abuts that fence.
                return Mathf.Lerp(.2f,height,Mathf.Clamp01(distance/Mathf.Max(.01f,Mathf.Min(8,length))));
            }
        }
        struct AccessCandidate { public Vector2 point;public Rect road;public float distance; }
        public AirportAccessRoad AirportAccess(AirportVisitPlan.Site site)
        {
            if(site==null)return null;
            if(airportAccess.TryGetValue(site.id,out var existing))return existing;
            var result=new AirportAccessRoad{site=site};airportAccess[site.id]=result;
            var gate=new Vector2(site.Entrance.x,site.Entrance.z);
            var outward=site.assetRotation*Vector3.right;var exit=gate+new Vector2(outward.x,outward.z)*10;
            var candidates=new List<AccessCandidate>();
            int lowX=Mathf.FloorToInt((gate.x-AirportVisitPlan.AccessSearchRadius)/128),highX=Mathf.FloorToInt((gate.x+AirportVisitPlan.AccessSearchRadius)/128);
            int lowZ=Mathf.FloorToInt((gate.y-AirportVisitPlan.AccessSearchRadius)/128),highZ=Mathf.FloorToInt((gate.y+AirportVisitPlan.AccessSearchRadius)/128);
            for(int rz=lowZ;rz<=highZ;rz++)for(int rx=lowX;rx<=highX;rx++)foreach(var road in BuildRegion(rx,rz).roads)
            {
                var raw=new Vector2(Mathf.Clamp(exit.x,road.xMin+.25f,road.xMax-.25f),Mathf.Clamp(exit.y,road.yMin+.25f,road.yMax-.25f));
                var point=new Vector2(Mathf.Floor(raw.x/2)*2+1,Mathf.Floor(raw.y/2)*2+1);
                float distance=Mathf.Abs(point.x-gate.x)+Mathf.Abs(point.y-gate.y);
                if(distance>AirportVisitPlan.AccessSearchRadius||!road.Contains(point)||!ExistingAirportStreet(point))continue;
                var axis=road.width>road.height?Vector2.right:Vector2.up;
                if(!ExistingAirportStreet(point+axis*4)&&!ExistingAirportStreet(point-axis*4))continue;
                candidates.Add(new AccessCandidate{point=point,road=road,distance=distance});
            }
            candidates.Sort((a,b)=>a.distance.CompareTo(b.distance));
            float best=float.MaxValue;List<Vector2> chosen=null;List<AirportAccessRoad.Span> chosenSpans=null;Rect landing=default;
            // Fixed local budget, independent of how many city regions have been
            // streamed. Straight/L connections reuse existing BSP streets.
            for(int n=0;n<Mathf.Min(48,candidates.Count);n++)foreach(bool horizontal in new[]{true,false})
            {
                var candidate=candidates[n];var bend=horizontal?new Vector2(candidate.point.x,exit.y):new Vector2(exit.x,candidate.point.y);
                var points=new List<Vector2>{gate};foreach(var p in new[]{exit,bend,candidate.point})if((p-points[points.Count-1]).sqrMagnitude>.001f)points.Add(p);
                for(int i=1;i<points.Count-1;)
                {
                    var incoming=points[i]-points[i-1];var outgoing=points[i+1]-points[i];
                    if(Mathf.Abs(incoming.x*outgoing.y-incoming.y*outgoing.x)<.001f)points.RemoveAt(i);else i++;
                }
                float length=0;for(int i=1;i<points.Count;i++)length+=Vector2.Distance(points[i-1],points[i]);
                if(length>AirportVisitPlan.AccessSearchRadius||length>=best)continue;
                if(!ClearAirportAccess(site,points,out var spans))continue;
                float wet=0;foreach(var span in spans)wet+=span.end-span.begin;
                float score=length+wet*2;if(score>=best)continue;
                best=score;chosen=points;chosenSpans=spans;landing=candidate.road;
            }
            if(chosen==null)
            {
                // Coastal gates can open directly onto a channel. Keep the
                // water intact and use one bounded, supported bridge corridor;
                // extend the search once, never paint an unconnected road stub.
                foreach(float radius in new[]{AirportVisitPlan.AccessSearchRadius,AirportVisitPlan.AccessFallbackRadius})
                {
                    var fallbackCandidates=FallbackAccessCandidates(gate,exit,radius);
                    for(int n=0;n<Mathf.Min(96,fallbackCandidates.Count);n++)foreach(float distance in new[]{24f,32f,48f,64f,96f})foreach(bool horizontal in new[]{true,false})
                    {
                        var candidate=fallbackCandidates[n];var reach=gate+new Vector2(outward.x,outward.z)*distance;
                        var bend=horizontal?new Vector2(candidate.point.x,reach.y):new Vector2(reach.x,candidate.point.y);
                        var points=RoundedAccess(new List<Vector2>{gate,reach,bend,candidate.point});if(points==null)continue;
                        float length=0;for(int i=1;i<points.Count;i++)length+=Vector2.Distance(points[i-1],points[i]);
                        if(length>radius||length>=best||!ClearAirportAccess(site,points,out var spans,true))continue;
                        float wet=0;foreach(var span in spans)wet+=span.end-span.begin;
                        float score=length+wet*2;if(score>=best)continue;
                        best=score;chosen=points;chosenSpans=spans;landing=candidate.road;
                    }
                    if(chosen!=null){result.fallback=true;break;}
                }
            }
            if(chosen==null)return result;
            result.points.AddRange(chosen);result.bridges.AddRange(chosenSpans);result.existingRoad=landing;
            var low=chosen[0];var high=low;
            for(int i=0;i<chosen.Count;i++){low=Vector2.Min(low,chosen[i]);high=Vector2.Max(high,chosen[i]);if(i>0)result.length+=Vector2.Distance(chosen[i-1],chosen[i]);}
            result.bounds=Rect.MinMaxRect(low.x-3,low.y-3,high.x+3,high.y+3);return result;
        }
        List<AccessCandidate> FallbackAccessCandidates(Vector2 gate,Vector2 exit,float radius)
        {
            var candidates=new List<AccessCandidate>();var seen=new HashSet<Vector2>();
            for(int rz=Mathf.FloorToInt((gate.y-radius)/128);rz<=Mathf.FloorToInt((gate.y+radius)/128);rz++)
            for(int rx=Mathf.FloorToInt((gate.x-radius)/128);rx<=Mathf.FloorToInt((gate.x+radius)/128);rx++)foreach(var road in BuildRegion(rx,rz).roads)
            {
                var nearest=new Vector2(Mathf.Clamp(exit.x,road.xMin+.25f,road.xMax-.25f),Mathf.Clamp(exit.y,road.yMin+.25f,road.yMax-.25f));
                var axis=road.width>road.height?Vector2.right:Vector2.up;
                foreach(float offset in new[]{0f,-16f,16f,-32f,32f})
                {
                    var raw=nearest+axis*offset;var point=new Vector2(Mathf.Floor(raw.x/2)*2+1,Mathf.Floor(raw.y/2)*2+1);
                    float distance=Mathf.Abs(point.x-gate.x)+Mathf.Abs(point.y-gate.y);
                    if(distance>radius||!road.Contains(point)||!seen.Add(point)||!ExistingAirportStreet(point))continue;
                    if(!ExistingAirportStreet(point+axis*4)&&!ExistingAirportStreet(point-axis*4))continue;
                    candidates.Add(new AccessCandidate{point=point,road=road,distance=distance});
                }
            }
            candidates.Sort((a,b)=>a.distance.CompareTo(b.distance));return candidates;
        }
        List<Vector2> RoundedAccess(List<Vector2> corners)
        {
            for(int i=corners.Count-1;i>0;i--)if((corners[i]-corners[i-1]).sqrMagnitude<.001f)corners.RemoveAt(i);
            for(int i=1;i<corners.Count-1;)
            {
                var a=corners[i]-corners[i-1];var b=corners[i+1]-corners[i];
                if(Mathf.Abs(a.x*b.y-a.y*b.x)<.001f)corners.RemoveAt(i);else i++;
            }
            if(corners.Count<2)return null;
            var result=new List<Vector2>{corners[0]};
            for(int i=1;i<corners.Count-1;i++)
            {
                var corner=corners[i];var incoming=(corner-corners[i-1]).normalized;var outgoing=(corners[i+1]-corner).normalized;
                float radius=Mathf.Min(14,Vector2.Distance(corner,corners[i-1])*.5f,Vector2.Distance(corner,corners[i+1])*.5f);
                if(radius<12)
                {
                    if(WaterDistance(corner.x,corner.y)<10)return null;
                    result.Add(corner);continue;
                }
                var entry=corner-incoming*radius;var centre=entry+outgoing*radius;var radial=entry-centre;
                float angle=Vector2.SignedAngle(incoming,outgoing)*Mathf.Deg2Rad;
                result.Add(entry);
                for(int step=1;step<=30;step++)
                {
                    float turn=angle*step/30;
                    result.Add(centre+new Vector2(radial.x*Mathf.Cos(turn)-radial.y*Mathf.Sin(turn),radial.x*Mathf.Sin(turn)+radial.y*Mathf.Cos(turn)));
                }
            }
            result.Add(corners[corners.Count-1]);
            for(int i=result.Count-1;i>0;i--)if((result[i]-result[i-1]).sqrMagnitude<.001f)result.RemoveAt(i);
            return result;
        }
        public bool ExistingAirportStreet(Vector2 point)
        {
            float x=Mathf.Floor(point.x/2)*2,z=Mathf.Floor(point.y/2)*2;var centre=new Vector2(x+1,z+1);
            bool street=false;foreach(var road in BuildRegion(Mathf.FloorToInt(centre.x/128),Mathf.FloorToInt(centre.y/128)).roads)if(road.Contains(centre)){street=true;break;}
            if(!street)return false;
            foreach(var p in new[]{new Vector2(x,z),new Vector2(x+2,z),new Vector2(x,z+2),new Vector2(x+2,z+2),centre})
                if(!AirportRoadSurface(p))return false;
            return true;
        }
        bool AirportRoadSurface(Vector2 point)
        {
            float x=point.x,z=point.y;
            return !AirportReserved(x,z,3)&&!crossings.LotAt(x,z)&&ZoneAt(x,z)!=EcologyZone.Ocean&&!Farmland(x,z)
                &&WaterDistance(x,z)>=7&&Height(x,z)<=.001f&&(Sample(x,z)!=Land.Forest||Ecology(x,z).x>.55f);
        }
        bool ClearAirportAccess(AirportVisitPlan.Site site,List<Vector2> points,out List<AirportAccessRoad.Span> spans,bool coastalFallback=false)
        {
            spans=new List<AirportAccessRoad.Span>();float walked=0,wetBegin=-1,totalWet=0;
            for(int segment=1;segment<points.Count;segment++)
            {
                var a=points[segment-1];var b=points[segment];var direction=(b-a).normalized;var normal=new Vector2(-direction.y,direction.x);float length=Vector2.Distance(a,b);
                if(segment<points.Count-1)
                {
                    var next=points[segment+1]-b;
                    bool turning=Mathf.Abs(direction.x*next.y-direction.y*next.x)>.001f;
                    if(turning&&!coastalFallback&&WaterDistance(b.x,b.y)<7)return false;
                }
                int samples=Mathf.Max(1,Mathf.CeilToInt(length/2));
                for(int step=0;step<=samples;step++)
                {
                    float distance=walked+length*step/samples;var centre=Vector2.Lerp(a,b,step/(float)samples);bool wet=false;
                    for(int side=-1;side<=1;side++)
                    {
                        var p=centre+normal*(2.6f*side);var source=SmallAirportNetwork.Source(site,new Vector3(p.x,0,p.y));
                        bool gate=source.x>=162&&source.x<=175&&Mathf.Abs(source.z+54)<=6.5f;
                        if(!gate&&AirportReserved(p.x,p.y,.5f))return false;
                        if(Height(p.x,p.y)>.001f||crossings.LotAt(p.x,p.y)||Farmland(p.x,p.y))return false;
                        wet|=WaterDistance(p.x,p.y)<7;
                    }
                    if(wet&&wetBegin<0)wetBegin=distance;
                    if(!wet&&wetBegin>=0)
                    {
                        float extent=distance-wetBegin;if(extent>(coastalFallback?160:64))return false;totalWet+=extent;
                        spans.Add(new AirportAccessRoad.Span{begin=wetBegin,end=distance});wetBegin=-1;
                    }
                    if(wetBegin>=0&&distance-wetBegin>(coastalFallback?160:64))return false;
                }
                walked+=length;
            }
            return wetBegin<0&&spans.Count<=2&&totalWet<=(coastalFallback?160:80);
        }
        public IEnumerable<AirportAccessRoad> AirportAccessRoads(Rect area)
        {
            var seen=new HashSet<int>();
            foreach(float z in new[]{area.yMin,area.center.y,area.yMax})
            {
                var site=airports.Near(z,AirportVisitPlan.AccessFallbackRadius);
                if(site==null||!seen.Add(site.id))continue;
                var road=AirportAccess(site);if(road.Connected&&road.bounds.Overlaps(area))yield return road;
            }
        }
        public bool AirportAccessOverlaps(Rect lot,float margin=0)
        {
            var site=airports.Near(lot.center.y,AirportVisitPlan.AccessFallbackRadius+lot.height*.5f);
            if(site==null||!AirportVisitPlan.Expand(site.CoreBounds,AirportVisitPlan.AccessFallbackRadius).Overlaps(lot))return false;
            var road=AirportAccess(site);
            if(road.Connected&&road.bounds.Overlaps(AirportVisitPlan.Expand(lot,margin)))
                for(int i=1;i<road.points.Count;i++)
                {
                    var a=road.points[i-1];var b=road.points[i];var rect=Rect.MinMaxRect(Mathf.Min(a.x,b.x)-2-margin,Mathf.Min(a.y,b.y)-2-margin,Mathf.Max(a.x,b.x)+2+margin,Mathf.Max(a.y,b.y)+2+margin);
                    if(rect.Overlaps(lot))return true;
                }
            return false;
        }
        public bool AirportAccessRaisedAt(Vector2 point)
        {
            var site=airports.Near(point.y,AirportVisitPlan.AccessFallbackRadius);if(site==null)return false;
            if(!AirportVisitPlan.Expand(site.CoreBounds,AirportVisitPlan.AccessFallbackRadius).Contains(point))return false;
            var road=AirportAccess(site);if(!road.Connected||!road.bounds.Contains(point))return false;
            float walked=0;
            for(int i=1;i<road.points.Count;i++)
            {
                var a=road.points[i-1];var delta=road.points[i]-a;float length=delta.magnitude;
                float distance=Mathf.Clamp(Vector2.Dot(point-a,delta/length),0,length);
                if(Vector2.Distance(point,a+delta/length*distance)<=2.1f&&road.Height(walked+distance)>.11f)return true;
                walked+=length;
            }
            return false;
        }
        public bool AirportAccessReserved(float x,float z,float margin=0)=>AirportAccessOverlaps(new Rect(x-.01f,z-.01f,.02f,.02f),margin);
    }
}









