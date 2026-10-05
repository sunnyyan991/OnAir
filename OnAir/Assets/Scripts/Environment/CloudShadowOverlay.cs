using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.Rendering.Universal;
using UnityEngine.Rendering.RenderGraphModule;
using UnityEngine.Experimental.Rendering;
namespace OnAir
{
    // Historical name retained for serialized/editor compatibility. This renders an
    // opacity shadow map in SUN space, never a fullscreen colour multiply.
    public sealed class CloudShadowOverlay:ScriptableRenderPass
    {
        public readonly Vector4[] centres=new Vector4[128],sizes=new Vector4[128],rects=new Vector4[128],grids=new Vector4[128];
        public int count;
        public const float RelativeStrength=.8f;
        readonly Material material;Vector4 bounds,slope,heights;float strength;
        static readonly int MapId=Shader.PropertyToID("_OnAirCloudShadowMap"),BoundsId=Shader.PropertyToID("_OnAirCloudShadowBounds"),SlopeId=Shader.PropertyToID("_OnAirCloudShadowSlope"),HeightsId=Shader.PropertyToID("_OnAirCloudShadowHeights"),StrengthId=Shader.PropertyToID("_OnAirCloudShadowStrength");
        sealed class PassData
        {
            public Material material;
            public Vector4[] centres,sizes,rects,grids;
            public Vector4 bounds,slope,heights;
            public int count;
            public float strength;
        }
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
        public override void RecordRenderGraph(RenderGraph graph,ContextContainer frameData)
        {
            // URP's opaque/transparent passes consume global textures. Publishing
            // through the graph declares their dependency on this sun-space map.
            var map=graph.CreateTexture(new TextureDesc(1024,1024){name="Cloud sun-space opacity",colorFormat=GraphicsFormat.R16G16B16A16_SFloat,filterMode=FilterMode.Bilinear,wrapMode=TextureWrapMode.Clamp,clearBuffer=true,clearColor=Color.clear});
            using(var builder=graph.AddRasterRenderPass<PassData>("Cloud projected sunlight shadows",out var data))
            {
                data.material=material;data.centres=centres;data.sizes=sizes;data.rects=rects;data.grids=grids;
                data.bounds=bounds;data.slope=slope;data.heights=heights;data.count=count;data.strength=count>0?strength:0;
                builder.SetRenderAttachment(map,0,AccessFlags.Write);
                builder.SetGlobalTextureAfterPass(map,MapId);
                builder.AllowGlobalStateModification(true);
                // Even with no visible clouds, clear the map and publish zero
                // strength so a previous camera/frame cannot leave stale shadows.
                builder.AllowPassCulling(false);
                builder.SetRenderFunc((PassData pass,RasterGraphContext context)=>
                {
                    pass.material.SetVectorArray("_CloudCentres",pass.centres);pass.material.SetVectorArray("_CloudSizes",pass.sizes);pass.material.SetVectorArray("_CloudRects",pass.rects);pass.material.SetVectorArray("_CloudGrids",pass.grids);
                    if(pass.count>0&&pass.strength>0)context.cmd.DrawProcedural(Matrix4x4.identity,pass.material,0,MeshTopology.Triangles,6,pass.count);
                    context.cmd.SetGlobalVector(BoundsId,pass.bounds);context.cmd.SetGlobalVector(SlopeId,pass.slope);context.cmd.SetGlobalVector(HeightsId,pass.heights);context.cmd.SetGlobalFloat(StrengthId,pass.strength);
                });
            }
        }
        public void Dispose(){Shader.SetGlobalFloat(StrengthId,0);CoreUtils.Destroy(material);}
    }
}
