using UnityEngine;

namespace OnAir
{
    // Game drawing profile; actual airport reference and intentional simplifications
    // are documented in Docs/AirportGroundMarkings.md. One unit is one metre.
    public static class AirportMarkingRules
    {
        public const float GuideWidth=.30f, EdgeWidth=.18f, EdgeGap=.18f;
        public const float StopWidth=.30f, SafetyWidth=.24f, ServiceWidth=.24f;
        public const float RunwayCentreWidth=.90f, RunwayEdgeWidth=.90f;
        public const float TaxiPavingHeight=.145f, GuideHeight=.205f;
        public static readonly Color Yellow=new Color32(209,182,92,255);
        public static readonly Color White=new Color32(225,223,202,255);
        public static readonly Color Red=new Color32(172,100,85,255);
        public static Vector3 BayLocal(AirportTaxiNetwork.Bay bay,Vector3 source) => Quaternion.Inverse(Quaternion.LookRotation(bay.forward))*(source-bay.centre);
        public static bool OnServiceApron(Vector3 source)
        {
            foreach(var bay in AirportTaxiNetwork.Bays)
            {var p=BayLocal(bay,source);if(p.x>=-94&&p.x<=62&&p.z>=-28&&p.z<=72)return true;}
            return false;
        }
        public static bool ProtectedRunwayPaint(Vector3 source)
        {
            for(int runway=0;runway<4;runway++)
            {
                var p=Quaternion.Euler(0,-AirportTaxiNetwork.RunwayAngles[runway],0)*(source-AirportTaxiNetwork.RunwayCentres[runway]);
                float x=Mathf.Abs(p.x),end=AirportTaxiNetwork.RunwayLengths[runway]/2-Mathf.Abs(p.z);
                if(x>30||end<0)continue;
                // The retained continuous white runway edge also owns its paint.
                if(x<1.1f||x>28.8f||end<40||(end>58&&end<112&&x<18))return true;
                float aim=AirportTaxiNetwork.AimingDistance(AirportTaxiNetwork.RunwayLengths[runway]);
                if(Mathf.Abs(end-aim)<24&&x>8.7f&&x<15.3f)return true;
                for(int station=1;station<=AirportTaxiNetwork.TouchdownStations(AirportTaxiNetwork.RunwayLengths[runway]);station++)
                    if(Mathf.Abs(end-150*station)<12&&x>9.8f&&x<19.8f)return true;
            }
            return false;
        }
    }
}
