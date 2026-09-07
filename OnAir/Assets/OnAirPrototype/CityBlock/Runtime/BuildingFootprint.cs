using UnityEngine;
namespace OnAir.Prototype
{
    // Root sits at ground centre; the entrance always faces local -Z.
    public sealed class BuildingFootprint : MonoBehaviour
    {
        public Vector2 size = new Vector2(6.5f,6.5f);
        public int floors = 3;
        public bool cornerShop;
        public Vector3 EntranceDirection => -transform.forward;
    }
}
