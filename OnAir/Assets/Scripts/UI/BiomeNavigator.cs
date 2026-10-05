using UnityEngine;
namespace OnAir
{
    public sealed class BiomeNavigator:MonoBehaviour
    {
        public GameEntry game;
        ContinuousWorldPlan plan;
        float nextSample;
        public string VisibleName{get;private set;}="TRANSITION";
        public float VisibleShare{get;private set;}
        public string Notice{get;private set;}="";
        public static string Name(ContinuousWorldPlan.EcologyZone zone)
        {
            switch(zone){case ContinuousWorldPlan.EcologyZone.CoastalTown:return "COASTAL TOWN";case ContinuousWorldPlan.EcologyZone.RiverPlain:return "RIVER PLAIN";default:return zone.ToString().ToUpperInvariant();}
        }
        void Update(){if(Time.unscaledTime>=nextSample){nextSample=Time.unscaledTime+.5f;RefreshLabel();}}
        void Prepare(){if(plan==null||plan.seed!=game.journey.seed)plan=new ContinuousWorldPlan(game.journey.seed,game.flight.routeSlope);plan.TrimCaches();}
        public void RefreshLabel()
        {
            if(!game||game.journey.Current==null)return;Prepare();
            var counts=new int[7];int total=0;float origin=game.journey.Current.index*64;
            // Equal viewport samples estimate projected screen coverage, rather than only the plane's tile.
            for(int iy=0;iy<10;iy++)for(int ix=0;ix<16;ix++)
            {
                var ray=game.sceneCamera.ViewportPointToRay(new Vector3((ix+.5f)/16,(iy+.5f)/10,0));
                if(Mathf.Abs(ray.direction.y)<.001f)continue;
                float t=-ray.origin.y/ray.direction.y;var p=ray.GetPoint(t);
                for(int k=0;k<2;k++){float h=plan.Height(p.x,p.z+origin);t=(h-ray.origin.y)/ray.direction.y;p=ray.GetPoint(t);}
                counts[(int)plan.ZoneAt(p.x,p.z+origin)]++;total++;
            }
            int best=0;for(int i=1;i<7;i++)if(counts[i]>counts[best])best=i;
            VisibleShare=total==0?0:counts[best]/(float)total;
            VisibleName=VisibleShare>.5f?Name((ContinuousWorldPlan.EcologyZone)best):"TRANSITION";
        }
        public bool Preview(ContinuousWorldPlan.EcologyZone zone)
        {
            Prepare();var route=game.journey;float current=(route.Current.index+route.Progress)*64;
            for(float z=current+128;z<current+100000;z+=32)
            {
                float x=game.flight.RouteX(z);
                if(plan.ZoneAt(x,z)!=zone||plan.EcologyBlendAt(x,z-120).Weight(zone)<.99f||plan.EcologyBlendAt(x,z+120).Weight(zone)<.99f)continue;
                if(zone==ContinuousWorldPlan.EcologyZone.Hills&&plan.Height(x,z)<24)continue;
                route.JumpForwardWorld(z);game.flight.Tick(0);game.cameraFollow.Follow(0);game.world.Refresh();RefreshLabel();Notice="";return true;
            }
            Notice="NO REGION FOUND - TRY NEW SEED";return false;
        }
    }
}
