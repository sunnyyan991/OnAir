using System;
using System.Collections.Generic;
using UnityEngine;
namespace OnAir
{
    // Global coordinates define geography; streaming slices are only ownership containers.
    public sealed class ContinuousWorldPlan
    {
        public const int RegionSize=128;
        public const int GeneratorVersion=17;
        public readonly int seed;
        public enum EcologyZone { City, Town, CoastalTown, Forest, RiverPlain, Hills, Ocean }
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
        public readonly float routeSlope;
        public ContinuousWorldPlan(int seed,float routeSlope=0){this.seed=seed;this.routeSlope=routeSlope;hydrology=new WorldHydrology(this);crossings=new WorldCrossings(this);highways=new WorldHighways(this);upperHighways=new WorldUpperHighways(this);layerHighways=new WorldLayerHighways(this);greenSpaces=new WorldGreenSpaces(this);}
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
            var schedule=new ZoneSchedule();float sum=0;
            for(int i=0;i<5;i++)
            {
                // City has the largest individual weight; no fixed hourly quota or repeating biome order.
                int ticket=Hash(cycle,i,873)%100;
                var zone=ticket<40?EcologyZone.City:ticket<54?EcologyZone.Town:ticket<64?EcologyZone.CoastalTown:ticket<78?EcologyZone.Forest:ticket<85?EcologyZone.RiverPlain:ticket<94?EcologyZone.Hills:EcologyZone.Ocean;
                sum+=320+Hash(cycle,i,729)%321;schedule.zones[i]=zone;schedule.ends[i]=sum;
            }
            for(int i=0;i<5;i++)schedule.ends[i]=schedule.ends[i]/sum*EcologyCycle;
            schedule.ends[4]=EcologyCycle;schedules.Add(cycle,schedule);return schedule;
        }
        EcologyZone Zone(float z,out float begin,out float end)
        {
            int cycle=Mathf.FloorToInt(z/EcologyCycle);float local=z-cycle*EcologyCycle;var schedule=Schedule(cycle);int i=0;while(i<4&&local>=schedule.ends[i])i++;
            begin=cycle*EcologyCycle+(i==0?0:schedule.ends[i-1]);end=cycle*EcologyCycle+schedule.ends[i];return schedule.zones[i];
        }
        public EcologyZone ZoneRange(float z,out float begin,out float end)=>Zone(z,out begin,out end);
        public float WaterDistance(float x,float z){if(!SampleKey(x,z,out long key))return hydrology.Distance(x,z);if(waterSamples.TryGetValue(key,out float value))return value;return waterSamples[key]=hydrology.Distance(x,z);}
        public struct EcoBlend
        {
            public EcologyZone a,b;public float t;
            public float Weight(EcologyZone zone)=>(a==zone?1-t:0)+(b==zone?t:0);
        }
        float EcologyCoordinate(float x,float z)=>z+40*Mathf.Sin(x/105f)+18*Mathf.Sin(x/43f);
        public EcologyZone ZoneAt(float x,float z)=>Zone(EcologyCoordinate(x,z),out _,out _);
        public EcoBlend EcologyBlendAt(float x,float z)
        {
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
        public bool AllowsBuilding(Rect bounds,int floors)
        {
            var ecology=Ecology(bounds.center.x,bounds.center.y);
            return floors<=(ecology.y>ecology.x?4:20)&&Dry(bounds);
        }
        public int TreeSpacing(float x,float z)
        {
            float patch=Noise(x,z,36,2141);return patch<.37f?37:patch>.63f?5:11;
        }
        public bool Farmland(float x,float z)
        {
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
            bool finish=depth>=5||(depth>=2&&area.width<=65&&area.height<=65&&random.NextDouble()<(district==2?.42:.15));
            if(finish){region.parcels.Add(new Parcel{bounds=area,district=district});return;}
            bool vertical=area.width>area.height*1.25f||(area.width>=area.height*.8f&&random.Next(2)==0);
            float length=vertical?area.width:area.height;
            if(length<27){region.parcels.Add(new Parcel{bounds=area,district=district});return;}
            float road=depth<2?4:2.5f;
            float cut=Mathf.Round((length*(.3f+(float)random.NextDouble()*.4f))/2)*2;
            cut=Mathf.Clamp(cut,11,length-road-11);
            if(vertical){region.roads.Add(new Rect(area.x+cut,area.y,road,area.height));Split(region,new Rect(area.x,area.y,cut,area.height),random,depth+1);Split(region,new Rect(area.x+cut+road,area.y,length-cut-road,area.height),random,depth+1);}
            else{region.roads.Add(new Rect(area.x,area.y+cut,area.width,road));Split(region,new Rect(area.x,area.y,area.width,cut),random,depth+1);Split(region,new Rect(area.x,area.y+cut+road,area.width,length-cut-road),random,depth+1);}
        }
        public bool Bridge(float z)=>Mathf.Abs(z-Mathf.Round(z/128)*128-2)<2.01f;
    }
}









