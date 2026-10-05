Shader "OnAir/LayeredPixelCloud" {
Properties { _MainTex("Approved atlas",2D)="white"{} _CloudRect("UV rectangle",Vector)=(0,0,1,1) _CloudGrid("Art pixel grid",Vector)=(64,32,0,0) _CloudPlaneY("Layer height",Float)=80 _CloudFade("Fade",Range(0,1))=1 }
SubShader { Tags {"Queue"="Transparent+20" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline"} Blend SrcAlpha OneMinusSrcAlpha ZWrite Off Cull Off
Pass { HLSLPROGRAM
#pragma vertex vert
#pragma fragment frag
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);float4 _PixelSky,_PixelEquator,_CloudRect,_CloudGrid;float _CloudPlaneY,_CloudSunsetGlow,_CloudFade;float4 _CloudViewForward,_CloudLightTint,_CloudFlash;
V vert(A i){V o;float3 p=TransformObjectToWorld(i.vertex.xyz);float3 forward=_CloudViewForward.xyz;
// Project the billboard onto a horizontal layer ALONG the viewing ray. In an
// orthographic view this preserves its exact silhouette while giving each pixel
// the correct depth. Large lower clouds can never rise over the aircraft.
p+=forward*((_CloudPlaneY-p.y)/min(forward.y,-.001));o.pos=TransformWorldToHClip(p);o.uv=i.uv;return o;}
half4 frag(V i):SV_Target {float2 q=(floor(saturate(i.uv)*(_CloudGrid.xy-.0001))+.5)/_CloudGrid.xy;half4 t=SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_CloudRect.xy+q*_CloudRect.zw);clip(t.a-.5);Light sun=GetMainLight();
half3 bright=_PixelSky.rgb*.6+sun.color*.85*(.35+.65*saturate(sun.direction.y));half3 shade=_PixelEquator.rgb*.6+sun.color*.10;
// Retain the approved painted value clusters while removing baked cyan pigment.
float value=dot(t.rgb,half3(.30,.59,.11));
bright/=max(1,max(bright.r,max(bright.g,bright.b)));
float warmth=saturate(sun.color.r-sun.color.b);
half3 underside=lerp(half3(1,1,1),half3(1.06,.94,.98),warmth);
half3 albedo=lerp(half3(.72,.72,.71),half3(1,1,1),smoothstep(.18,.95,value));
half3 lighting=lerp(shade*.65+bright*.35,bright,smoothstep(.12,.85,value));
// Weather attenuates brightness, not the clock's warm/cool colour identity.
half3 lightTint=_CloudLightTint.rgb/max(.01,max(_CloudLightTint.r,max(_CloudLightTint.g,_CloudLightTint.b)));
lighting=lerp(lighting,dot(lighting,half3(.30,.59,.11))*lightTint,.65);
// The final minute of a ten-minute day adds a restrained peach crown / pink base.
// High cloud tops retain a little scattered sunset light after the streets dim.
// The phase gate reaches zero at blue hour, so this never brightens night clouds.
half3 sunsetTint=lerp(half3(1.10,.90,.82),half3(1.075,.98,.90),value);
lighting*=lerp(half3(1,1,1),sunsetTint,_CloudSunsetGlow*.65);
half skyStrength=saturate(dot(_PixelSky.rgb,half3(.30,.59,.11))*2);
lighting+=lerp(half3(.035,.005,.018),half3(.11,.044,.02),smoothstep(.25,.85,value))*(_CloudSunsetGlow*skyStrength);
// Height bands follow the approved silhouette; painted value clusters remain intact.
float lower=1-smoothstep(.12,.52,q.y);
float middle=smoothstep(.12,.42,q.y)*(1-smoothstep(.55,.85,q.y));
// Coral-orange sunset: pink remains in the underside, while red/green lead blue.
lighting*=lerp(half3(1,1,1),half3(1.16,.84,.72),lower*_CloudSunsetGlow*.75);
lighting*=lerp(half3(1,1,1),half3(1.12,.94,.80),middle*_CloudSunsetGlow*.65);
lighting+=half3(.045,.006,.015)*(lower*_CloudSunsetGlow*skyStrength);
half3 result=albedo*lighting*lerp(underside,half3(1,1,1),value);
// A single local pocket of cloud lights up; no scene light, exposure or fullscreen effect.
float2 flashDelta=(q-_CloudFlash.xy)/float2(.20,.25);
float pocket=1-smoothstep(0,1,dot(flashDelta,flashDelta));
result+=half3(.91,.94,1)*(_CloudFlash.w*pocket);
return half4(result,_CloudFade);}

ENDHLSL }
Pass {
 Name "ShadowCaster" Tags {"LightMode"="ShadowCaster"} ZWrite On ZTest LEqual ColorMask 0 Cull Off
 HLSLPROGRAM
 #pragma vertex shadowVert
 #pragma fragment shadowFrag
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
 #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
 TEXTURE2D(_MainTex);SAMPLER(sampler_MainTex);
 float4 _CloudRect,_CloudGrid,_CloudViewForward;float _CloudPlaneY,_CloudFade;float3 _LightDirection;
 struct A {float4 vertex:POSITION;float2 uv:TEXCOORD0;};struct V {float4 pos:SV_POSITION;float2 uv:TEXCOORD0;};
 V shadowVert(A i){V o;float3 p=TransformObjectToWorld(i.vertex.xyz);float3 f=_CloudViewForward.xyz;
 p+=f*((_CloudPlaneY-p.y)/min(f.y,-.001));
 o.pos=TransformWorldToHClip(ApplyShadowBias(p,float3(0,1,0),_LightDirection));
 #if UNITY_REVERSED_Z
 o.pos.z=min(o.pos.z,UNITY_NEAR_CLIP_VALUE);
 #else
 o.pos.z=max(o.pos.z,UNITY_NEAR_CLIP_VALUE);
 #endif
 o.uv=i.uv;return o;}
 half4 shadowFrag(V i):SV_Target{float2 q=(floor(saturate(i.uv)*(_CloudGrid.xy-.0001))+.5)/_CloudGrid.xy;
 // Runtime uses a separate sun-space opacity map: continuous fades without depth-map dithering.
 clip(SAMPLE_TEXTURE2D(_MainTex,sampler_MainTex,_CloudRect.xy+q*_CloudRect.zw).a-.5);return 0;}
 ENDHLSL
}
} }
