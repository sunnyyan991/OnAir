using UnityEngine;
namespace OnAir
{
    // Diagnostics only. The camera renders directly to the display at its normal resolution.
    public sealed class FrameRateDisplay:MonoBehaviour
    {
        public float FramesPerSecond{get;private set;}
        float elapsed;int frames;
        void Update(){elapsed+=Time.unscaledDeltaTime;frames++;if(elapsed>=1){FramesPerSecond=frames/elapsed;elapsed=0;frames=0;}}
    }
}
