using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir.Prototype
{
    // Scene-local rendering override. Restore the user's pipeline when this preview closes.
    [ExecuteAlways]
    public sealed class CityPreviewRendering : MonoBehaviour
    {
        public RenderPipelineAsset pipeline;
        RenderPipelineAsset previous;
        bool applied;
        void OnEnable()
        {
            if(!pipeline)return;
            previous=QualitySettings.renderPipeline;QualitySettings.renderPipeline=pipeline;applied=true;
        }
        void OnDisable()
        {
            if(applied&&QualitySettings.renderPipeline==pipeline)QualitySettings.renderPipeline=previous;
            applied=false;
        }
    }
}
