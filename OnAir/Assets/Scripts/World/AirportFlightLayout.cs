using UnityEngine;

namespace OnAir
{
    // References only; the authored airport remains one reusable asset.
    public sealed class AirportFlightLayout : ScriptableObject
    {
        public GameObject airport;
        public Bounds bounds;
        public GameObject[] approachHomes;
        public Vector2 cruiseVisualOffset = new Vector2(-45.96194f, -45.96194f);
    }
}
