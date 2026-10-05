using UnityEngine;
namespace OnAir
{
    public sealed class BuildingView : MonoBehaviour
    {
        [Tooltip("Keep the approved asset palette instead of applying a random district facade colour.")]
        public bool preserveFacadeColor;

        [Tooltip("Explicit coloured self-lit signs/lanterns; baked separately from random windows.")]
        public MeshRenderer[] colouredLights=System.Array.Empty<MeshRenderer>();
        [Tooltip("Renderers lit by the environment at night. Assign explicitly on the prefab.")]
        public MeshRenderer[] windows=System.Array.Empty<MeshRenderer>();
        [Tooltip("Explicit plaster/cladding surfaces eligible for the city facade palette; excludes glass, timber and roof.")]
        public MeshRenderer[] facades=System.Array.Empty<MeshRenderer>();
    }
}
