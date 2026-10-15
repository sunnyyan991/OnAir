using UnityEngine;
namespace OnAir
{
    // Reports the ecology zone under the aircraft so automatic weather follows the landscape.
    public sealed class ClimateZoneTracker:MonoBehaviour
    {
        public GameEntry game;
        float nextSample;
        void Update()
        {
            if(!game||game.journey.Current==null||game.flight.Landscape==null||Time.unscaledTime<nextSample)return;
            nextSample=Time.unscaledTime+.5f;
            float z=(game.journey.Current.index+game.journey.Progress)*TerrainStreamer.SegmentLength;
            var zone=game.airport&&game.airport.Active?ContinuousWorldPlan.EcologyZone.City:game.flight.Landscape.ZoneAt(game.flight.transform.position.x,z);
            game.session.SetClimateZone((zone==ContinuousWorldPlan.EcologyZone.Airport?ContinuousWorldPlan.EcologyZone.City:zone).ToString());
        }
    }
}
