using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
namespace OnAir
{
    // Historical name retained for serialized/editor compatibility. This renders an
    // opacity shadow map in SUN space, never a fullscreen colour multiply.
    public sealed class CloudShadowOverlay:ScriptableRenderPass
    {
        public readonly Vector4[] centres=new Vector4[128],sizes=new Vector4[128],rects=new Vector4[128],grids=new Vector4[128];
        public int count;
        public const float RelativeStrength=.8f;
        readonly Material material;RTHandle map;Vector4 bounds,slope,heights;float strength;
        public CloudShadowOverlay(){material=CoreUtils.CreateEngineMaterial(Shader.Find("Hidden/OnAir/CloudShadowOverlay"));renderPassEvent=RenderPassEvent.BeforeRenderingOpaques;}
        public void Prepare(Texture atlas,Camera camera,Light light)
        {
            var sun=-light.transform.forward;strength=sun.y>.01f&&light.intensity>0?RelativeStrength*light.shadowStrength:0;
            slope=new Vector4(sun.x/Mathf.Max(.01f,sun.y),sun.z/Mathf.Max(.01f,sun.y),0,0);
            var forward=camera.transform.forward;
            var right=camera.transform.right-forward*(camera.transform.right.y/forward.y);
            var up=camera.transform.up-forward*(camera.transform.up.y/forward.y);
            material.SetTexture("_CloudAtlas",atlas);material.SetVector("_CloudRight",right);material.SetVector("_CloudUp",up);
            // Fit all possible receivers in this view, including the aircraft above the city.
            Vector2 lo=new Vector2(float.MaxValue,float.MaxValue),hi=-lo;
            for(int y=0;y<=1;y++)for(int x=0;x<=1;x++)foreach(float height in new[]{0f,200f}){
                var ray=camera.ViewportPointToRay(new Vector3(x,y,0));var p=ray.GetPoint((height-ray.origin.y)/ray.direction.y);
                var q=new Vector2(p.x-p.y*slope.x,p.z-p.y*slope.y);lo=Vector2.Min(lo,q);hi=Vector2.Max(hi,q);
            }
            lo-=Vector2.one*8;hi+=Vector2.one*8;bounds=new Vector4(lo.x,lo.y,1/(hi.x-lo.x),1/(hi.y-lo.y));
            heights=new Vector4(-10000,-10000,-10000,0);
            for(int i=0;i<count;i++){int layer=Mathf.RoundToInt(sizes[i].w);heights[layer]=Mathf.Max(heights[layer],centres[i].y);}
            material.SetVector("_CloudMapBounds",bounds);material.SetVector("_CloudSunSlope",slope);
        }
        // Kept for old diagnostics; opacity is relative to the real main-light shadow.
        public static float ShadowStrength(Color sky,Color sunlight,float intensity,float elevation,float strength)=>elevation>.01f&&intensity>0?RelativeStrength*strength:0;
        public override void OnCameraSetup(CommandBuffer cmd,ref RenderingData data){
            var descriptor=new RenderTextureDescriptor(1024,1024,RenderTextureFormat.ARGBHalf,0){msaaSamples=1,sRGB=false};
            RenderingUtils.ReAllocateIfNeeded(ref map,descriptor,FilterMode.Bilinear,TextureWrapMode.Clamp,name:"Cloud sun-space opacity");
            ConfigureTarget(map);ConfigureClear(ClearFlag.Color,Color.clear);
        }
        public override void Execute(ScriptableRenderContext context,ref RenderingData data){
            var cmd=CommandBufferPool.Get("Cloud projected sunlight shadows");
            material.SetVectorArray("_CloudCentres",centres);material.SetVectorArray("_CloudSizes",sizes);material.SetVectorArray("_CloudRects",rects);material.SetVectorArray("_CloudGrids",grids);
            if(count>0&&strength>0)cmd.DrawProcedural(Matrix4x4.identity,material,0,MeshTopology.Triangles,6,count);
            cmd.SetGlobalTexture("_OnAirCloudShadowMap",map.nameID);cmd.SetGlobalVector("_OnAirCloudShadowBounds",bounds);cmd.SetGlobalVector("_OnAirCloudShadowSlope",slope);cmd.SetGlobalVector("_OnAirCloudShadowHeights",heights);cmd.SetGlobalFloat("_OnAirCloudShadowStrength",count>0?strength:0);
            context.ExecuteCommandBuffer(cmd);CommandBufferPool.Release(cmd);
        }
        public void Dispose(){Shader.SetGlobalFloat("_OnAirCloudShadowStrength",0);map?.Release();CoreUtils.Destroy(material);}
    }
}
