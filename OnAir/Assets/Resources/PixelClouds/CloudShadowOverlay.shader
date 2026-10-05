Shader "Hidden/OnAir/CloudShadowOverlay"
{
 SubShader { Tags {"RenderPipeline"="UniversalPipeline"} Pass {
 Name "Cloud light-space opacity" ZWrite Off ZTest Always Cull Off Blend One One BlendOp Max
 HLSLPROGRAM
 #pragma vertex vert
 #pragma fragment frag
 #pragma target 3.5
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 TEXTURE2D(_CloudAtlas); SAMPLER(sampler_CloudAtlas);
 float4 _CloudCentres[128],_CloudSizes[128],_CloudRects[128],_CloudGrids[128];
 float3 _CloudRight,_CloudUp;float4 _CloudMapBounds,_CloudSunSlope;
 struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;nointerpolation uint cloud:TEXCOORD1;};
 V vert(uint vertex:SV_VertexID,uint instance:SV_InstanceID){
  const float2 corners[6]={float2(0,0),float2(1,0),float2(1,1),float2(0,0),float2(1,1),float2(0,1)};
  V o;o.uv=corners[vertex];o.cloud=instance;
  float3 world=_CloudCentres[instance].xyz+_CloudRight*((o.uv.x-.5)*_CloudSizes[instance].x)+_CloudUp*((o.uv.y-.5)*_CloudSizes[instance].y);
  float2 projected=world.xz-world.y*_CloudSunSlope.xy;
  float2 uv=(projected-_CloudMapBounds.xy)*_CloudMapBounds.zw;
  o.pos=float4(uv*2-1,0,1);
  #if UNITY_UV_STARTS_AT_TOP
   o.pos.y=-o.pos.y;
  #endif
  return o;
 }
 half4 frag(V i):SV_Target{
  uint n=i.cloud;float2 q=(floor(saturate(i.uv)*(_CloudGrids[n].xy-.0001))+.5)/_CloudGrids[n].xy;
  clip(SAMPLE_TEXTURE2D(_CloudAtlas,sampler_CloudAtlas,_CloudRects[n].xy+q*_CloudRects[n].zw).a-.5);
  float layer=_CloudSizes[n].w;half fade=saturate(_CloudSizes[n].z);
  return half4(layer<.5?fade:0,layer>=.5&&layer<1.5?fade:0,layer>=1.5?fade:0,0);
 }
 ENDHLSL
 } }
}
