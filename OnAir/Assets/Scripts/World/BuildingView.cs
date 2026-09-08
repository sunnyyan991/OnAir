using UnityEngine;
namespace OnAir
{
    public sealed class BuildingView : MonoBehaviour
    {
        [Tooltip("Renderers lit by the environment at night. Assign explicitly on the prefab.")]
        public MeshRenderer[] windows=System.Array.Empty<MeshRenderer>();
    }
}
