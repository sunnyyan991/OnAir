using UnityEngine;
namespace OnAir
{
    // Authoring coordinates survive the four-direction bake, including occlusion.
    public sealed class AnimatedBillboardSurface : MonoBehaviour
    {
        public string slotId;
        public float width,height;
        [Range(0,2)] public int phaseOffset;
    }
}
