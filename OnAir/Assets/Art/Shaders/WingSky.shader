Shader "OnAir/WingSky" {
Properties { _SkyColor("Sky",Color)=(.4,.6,.8,1) _HorizonColor("Horizon",Color)=(.6,.7,.75,1) }
SubShader { Tags { "Queue"="Background" "RenderType"="Background" "RenderPipeline"="UniversalPipeline" } Cull Off ZWrite Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
struct A {float4 vertex:POSITION;}; struct V {float4 pos:SV_POSITION;float3 dir:TEXCOORD0;};
float4 _SkyColor,_HorizonColor;float _Night;
V vert(A i){V o;o.pos=TransformObjectToHClip(i.vertex.xyz);o.dir=i.vertex.xyz;return o;}
float hash(float2 p){return frac(sin(dot(p,float2(127.1,311.7)))*43758.5453);}
float noise(float2 p){float2 i=floor(p),f=frac(p);f=f*f*(3-2*f);return lerp(lerp(hash(i),hash(i+float2(1,0)),f.x),lerp(hash(i+float2(0,1)),hash(i+1),f.x),f.y);}
half4 frag(V i):SV_Target {float3 d=normalize(i.dir);float h=saturate(d.y);float3 c=lerp(_HorizonColor.rgb,_SkyColor.rgb,smoothstep(0,.65,h));
float light=1-_Night;
float2 uv=float2(atan2(d.z,d.x),asin(d.y));float2 cell=uv*340;
float star=step(.994,hash(floor(cell)))*(1-smoothstep(.04,.26,length(frac(cell)-.5)));
c+=star*_Night*smoothstep(.015,.2,h)*.65;
// Quantize only cloud art, never the world or the camera. Overlapping round
// lobes form a cotton silhouette with a flat, shaded underside.
float2 q=floor(uv*360)/360;
float cloud=0,shade=0;
[unroll] for(int n=-1;n<=1;n++) {
 float column=floor(q.x/.55)+n;
 float seed=hash(float2(column,17));float cx=(column+.5)*.55;
 float cy=.13+seed*.20;float2 p=(q-float2(cx,cy))/float2(.17+seed*.035,.105+seed*.025);
 float shape=min(length((p-float2(-.50,.02))/float2(.57,.65)),length((p-float2(.10,.30))/float2(.57,.86)));
 shape=min(shape,length((p-float2(.65,.06))/float2(.53,.63)));
 shape=min(shape,length((p-float2(-.04,-.17))/float2(.92,.43)));
 float mask=step(shape,1)*step(-.46,p.y);cloud=max(cloud,mask);
 shade=max(shade,mask*(p.y<-.20+.10*p.x?.0:shape>.86&&p.y<.35?.60:1));
}
float3 cloudShade=lerp(float3(.55,.65,.75),float3(.98,.97,.91),shade);
cloudShade=lerp(cloudShade,float3(.10,.14,.23)*( .7+shade*.3),_Night);
c=lerp(c,cloudShade,cloud*.93);
return half4(c,1);}
ENDHLSL } } }
