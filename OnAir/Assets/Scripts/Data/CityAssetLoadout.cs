using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;

namespace OnAir
{
    public enum CityDistrict { Legacy, CBD, Residence, Airport }

    [Serializable] public sealed class CityDistrictSettings
    {
        public string[] buildings = Array.Empty<string>();
        public float house=1, apartment=1, shop=1, office=1, retail=1, tower=1;
        [Range(0,1)] public float residentialParcels=.33f, commercialParcels=.33f;
        [Range(0,1)] public float largeCommercialChance=1;
        [NonSerialized] HashSet<string> equipped;
        public bool Contains(string id)
        {
            if(equipped==null)equipped=new HashSet<string>(buildings,StringComparer.Ordinal);
            return equipped.Contains(id);
        }
        public void Set(string id,bool value)
        {
            var ids=new HashSet<string>(buildings,StringComparer.Ordinal);
            if(value)ids.Add(id);else ids.Remove(id);
            buildings=ids.OrderBy(s=>s,StringComparer.Ordinal).ToArray();equipped=null;
        }
        public float FamilyWeight(string family)
        {
            switch(family){case "House":return house;case "Apartment":return apartment;case "Shop":return shop;
                case "Office":return office;case "Retail":return retail;case "Tower":return tower;default:return 1;}
        }
    }

    // Captured into each new flight. Missing data means the exact pre-sandbox rules.
    [Serializable] public sealed class CityAssetLoadout
    {
        public int version=1;
        public CityDistrictSettings cbd=new CityDistrictSettings(), residence=new CityDistrictSettings();
        public CityDistrictSettings For(CityDistrict district)=>district==CityDistrict.CBD?cbd:residence;
        public CityAssetLoadout Copy()=>JsonUtility.FromJson<CityAssetLoadout>(JsonUtility.ToJson(this));
        public float Weight(SpawnRule rule,CityDistrict district)
        {
            if(district==CityDistrict.Legacy)return IsReserve(rule.Id)?0:1;
            if(district==CityDistrict.Airport)return 0;
            var settings=For(district);return settings.Contains(rule.Id)?settings.FamilyWeight(rule.Family):0;
        }
        public int ParcelDistrict(CityDistrict district,float ticket,int original)
        {
            if(district==CityDistrict.Legacy||district==CityDistrict.Airport)return original;
            var s=For(district);return ticket<s.residentialParcels?0:ticket<1-s.commercialParcels?1:2;
        }
        public static bool IsReserve(string id)=>int.TryParse(id,out int n)&&n>=181&&n<=186;
        public static CityAssetLoadout Defaults(BuildingData data)
        {
            var ids=data.Rules.Where(r=>r.Weight>0&&r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)&&!IsReserve(r.Id)).Select(r=>r.Id).ToArray();
            return new CityAssetLoadout{
                cbd=new CityDistrictSettings{buildings=ids.ToArray(),house=.18f,apartment=.7f,shop=1,office=3.5f,retail=3,tower=4,residentialParcels=.1f,commercialParcels=.7f,largeCommercialChance=.85f},
                residence=new CityDistrictSettings{buildings=ids.ToArray(),house=2.2f,apartment=3,shop=1.1f,office=.14f,retail=.18f,tower=.06f,residentialParcels=.7f,commercialParcels=.05f,largeCommercialChance=.12f}
            };
        }
        public void Validate(BuildingData data)
        {
            if(version!=1||cbd==null||residence==null)throw new FormatException("Unsupported city asset configuration.");
            var allowed=new HashSet<string>(data.Rules.Where(r=>r.Weight>0&&r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)).Select(r=>r.Id));
            foreach(var s in new[]{cbd,residence})
            {
                if(s.buildings==null||s.buildings.Length>512||s.buildings.Distinct().Count()!=s.buildings.Length||s.buildings.Any(id=>id==null||!allowed.Contains(id)))throw new FormatException("Missing, retired or duplicate city building.");
                foreach(float v in new[]{s.house,s.apartment,s.shop,s.office,s.retail,s.tower,s.residentialParcels,s.commercialParcels,s.largeCommercialChance})
                    if(float.IsNaN(v)||float.IsInfinity(v)||v<0||v>20)throw new FormatException("Invalid city weight.");
                if(s.residentialParcels+s.commercialParcels>1||s.largeCommercialChance>1)throw new FormatException("Invalid city district probabilities.");
            }
        }
    }
}
