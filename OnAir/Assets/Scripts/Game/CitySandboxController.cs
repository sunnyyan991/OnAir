using System;
using UnityEngine;
namespace OnAir
{
    public sealed class CitySandboxController:MonoBehaviour
    {
        public CitySandboxCatalog Catalog{get;private set;}
        public CityAssetLoadout Applied{get;private set;}
        public CityAssetLoadout Draft{get;private set;}
        public string Notice{get;private set;}="";
        public bool HasChanges=>Draft!=null&&JsonUtility.ToJson(Draft)!=JsonUtility.ToJson(Applied);
        BuildingData data;
        SaveFileStore store;
        bool persistent,readBlocked;
        public void Initialize(BuildingData buildings,string directory,bool saving,CitySandboxCatalog catalog=null)
        {
            data=buildings;store=new SaveFileStore(directory);persistent=saving;
            Catalog=catalog?catalog:Resources.Load<CitySandboxCatalog>("CitySandbox/Catalog");
            Applied=CityAssetLoadout.Defaults(data);
            if(persistent)
                try{var json=store.Read("citySandbox.json");if(json!=null){var loaded=JsonUtility.FromJson<CityAssetLoadout>(SaveFileStore.Unpack(json));if(loaded==null)throw new FormatException("Empty city configuration.");loaded.Validate(data);Applied=loaded;}}
                catch(Exception e){readBlocked=true;Notice="SAVED CONFIG KEPT - USING DEMO DEFAULTS";Debug.LogWarning("City sandbox settings preserved: "+e.Message);}
        }
        public void Open(){Draft=Applied.Copy();if(!readBlocked)Notice="DRAG ASSETS INTO A DISTRICT. APPLIES TO NEW FLIGHTS.";}
        public bool SetEquipped(CityDistrict district,string id,bool equipped)
        {
            if(Draft==null||(district!=CityDistrict.CBD&&district!=CityDistrict.Residence))return false;
            bool allowed=false;foreach(var r in data.Rules)if(r.Id==id&&r.Weight>0&&r.Matches(Biome.City,Weather.Clear,DayPeriod.Day)){allowed=true;break;}
            if(!allowed)return false;Draft.For(district).Set(id,equipped);Notice="CHANGES READY - APPLY FOR YOUR NEXT NEW FLIGHT";return true;
        }
        public void ResetDraft(){Draft=CityAssetLoadout.Defaults(data);Notice="DEMO DEFAULTS RESTORED. APPLY TO SAVE.";}
        public bool Apply()
        {
            if(Draft==null)return false;
            try{
                Draft.Validate(data);
                if(persistent){if(readBlocked){Notice="SAVED CONFIG NEEDS REVIEW BEFORE OVERWRITING";return false;}store.Write("citySandbox.json",SaveFileStore.Pack(Draft),"citySandbox.bak");}
                Applied=Draft.Copy();Notice="APPLIED TO NEW FLIGHTS. EXISTING SAVES KEEP THEIR MAP.";return true;
            }catch(Exception e){Notice="COULD NOT SAVE - YOUR CHANGES ARE STILL HERE";Debug.LogWarning("City sandbox unchanged: "+e.Message);return false;}
        }
    }
}
