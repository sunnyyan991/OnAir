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
            game.session.SetClimateZone(game.flight.Landscape.ZoneAt(game.flight.transform.position.x,z).ToString());
        }
    }
}
