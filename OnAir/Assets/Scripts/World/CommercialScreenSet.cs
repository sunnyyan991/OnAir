using UnityEngine;
namespace OnAir
{
    // Shared baked LED playlist. Animation is evaluated in the pixel shader;
    // no camera, RenderTexture, per-building Update or material allocation.
    public sealed class CommercialScreenSet : MonoBehaviour
    {
        public string contentCode="DEC-SCREEN-001";
        public Texture2DArray frames;
    }
}
