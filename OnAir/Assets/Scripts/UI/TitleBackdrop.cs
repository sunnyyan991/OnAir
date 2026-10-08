using UnityEngine;
using UnityEngine.Rendering;

namespace OnAir
{
    // Authored objects, camera and lighting. No journey, save or generation dependencies.
    public sealed class TitleBackdrop : MonoBehaviour
    {
        public Camera sceneCamera;
        public Light sun;
        public Color ambient=new Color(.65f,.72f,.76f);
        public void Show()
        {
            gameObject.SetActive(true);
            RenderSettings.fog=false;
            RenderSettings.ambientMode=AmbientMode.Flat;
            RenderSettings.ambientLight=ambient;
            RenderSettings.sun=sun;
            // Pixel surfaces use explicit palette globals; restore these after any flight climate.
            Shader.SetGlobalColor("_PixelSky",ambient);
            Shader.SetGlobalColor("_PixelEquator",ambient*.67f);
            Shader.SetGlobalColor("_PixelGround",ambient*.34f);
            Shader.SetGlobalFloat("_PixelOriginZ",0);
            Shader.SetGlobalFloat("_PixelWorldSeed",0);
            Shader.SetGlobalFloat("_WindowSleepProgress",0);
            Shader.SetGlobalFloat("_OnAirCloudShadowStrength",0);
        }
        public void Hide()=>gameObject.SetActive(false);
    }
}
