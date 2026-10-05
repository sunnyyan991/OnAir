using UnityEngine;
namespace OnAir
{
    // Root sits at ground centre; the entrance always faces local -Z.
    public sealed class BuildingFootprint : MonoBehaviour
    {
        public Vector2 size = new Vector2(6.5f,6.5f);
        public int floors = 3;
        public bool cornerShop;
        [Tooltip("Allow a 90 degree placement when only the rotated footprint fits a frontage strip.")]
        public bool allowQuarterTurn;
        public const float ModuleUnit = .25f;
        [Tooltip("Authored height; zero uses the floor-count estimate for older assets.")]
        public float height;
        public Vector3Int Modules => new Vector3Int(Mathf.CeilToInt(size.x/ModuleUnit),Mathf.CeilToInt((height>0?height:floors*3f+2f)/ModuleUnit),Mathf.CeilToInt(size.y/ModuleUnit));
        public Vector3 EntranceDirection => -transform.forward;
    }
}
