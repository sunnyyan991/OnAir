using UnityEngine;
using UnityEngine.Rendering;
namespace OnAir
{
    // Overlays pixel raindrop rings on a water mesh. One shared material; WeatherParticles drives
    // the _RainRipples intensity and _RainRippleTime clock globals.
    public static class RainRippleSurface
    {
        static Material shared;
        static bool missing;
        public static void Attach(Transform water,Mesh mesh)
        {
            if(!shared&&!missing)
            {
                var shader=Shader.Find("OnAir/RainRipples");
                if(!shader){missing=true;Debug.LogWarning("RainRipples shader missing; water ripples disabled.");return;}
                shared=new Material(shader){name="Rain ripples",hideFlags=HideFlags.DontSave};
            }
            if(!shared)return;
            var obj=new GameObject("Rain ripples");obj.transform.SetParent(water,false);
            obj.AddComponent<MeshFilter>().sharedMesh=mesh;
            var renderer=obj.AddComponent<MeshRenderer>();renderer.sharedMaterial=shared;
            renderer.shadowCastingMode=ShadowCastingMode.Off;renderer.receiveShadows=false;
            renderer.lightProbeUsage=LightProbeUsage.Off;renderer.reflectionProbeUsage=ReflectionProbeUsage.Off;
        }
    }
}
