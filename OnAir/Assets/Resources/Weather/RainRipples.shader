Shader "OnAir/RainRipples"
{
    // Pixel-snapped raindrop rings drawn over water surfaces. Intensity, clock and tint are
    // globals set by WeatherParticles, so every water tile shares one material.
    SubShader
    {
        Tags { "Queue"="Transparent-10" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" "IgnoreProjector"="True" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Offset -1, -1
            Cull Back
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            float _RainRipples,_RainRippleTime,_PixelOriginZ;
            float4 _PixelSky;
            struct Attributes { float4 positionOS:POSITION; };
            struct Varyings { float4 positionCS:SV_POSITION; float3 positionWS:TEXCOORD0; };
            Varyings vert(Attributes v)
            {
                Varyings o;o.positionWS=TransformObjectToWorld(v.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);return o;
            }
            float Hash(float2 p){p=frac(p*float2(127.1,311.7));p+=dot(p,p+19.19);return frac(p.x*p.y);}
            static const float PixelSize=.25;   // PixelArtStandard.PixelWorldSize
            static const float Cell=2.5;        // one potential drop per cell per cycle
            static const float Period=1.1;      // seconds for a ring to expand and fade
            static const float MaxRadius=1.25;
            half4 frag(Varyings i):SV_Target
            {
                clip(_RainRipples-.001);
                // Immutable map coordinates, snapped to the shared world pixel grid.
                float2 map=float2(i.positionWS.x,i.positionWS.z+_PixelOriginZ);
                float2 p=(floor(map/PixelSize)+.5)*PixelSize;
                float2 cell=floor(p/Cell);
                float ring=0,splash=0;
                [unroll] for(int y=-1;y<=1;y++)
                [unroll] for(int x=-1;x<=1;x++)
                {
                    float2 c=cell+float2(x,y);
                    float phase=Hash(c+17.3);
                    float clock=_RainRippleTime/Period+phase;
                    float cycle=floor(clock),t=frac(clock);
                    float2 seed=c+cycle*float2(3.17,7.61);
                    // Halve drop density for both rain profiles while retaining ring opacity.
                    if(Hash(seed)>_RainRipples*.5)continue;
                    // Snap the drop centre to a pixel so rings stay symmetric on the grid.
                    float2 centre=(floor((c+float2(Hash(seed+1.7),Hash(seed+4.1))*.8+.1)*Cell/PixelSize)+.5)*PixelSize;
                    float d=length(p-centre),r=t*MaxRadius;
                    float fade=(1-t)*(1-t);
                    // One-pixel hard ring, no anti-aliasing, then a short-lived centre splash.
                    ring=max(ring,(abs(d-r)<PixelSize*.5?1:0)*fade);
                    splash=max(splash,(t<.12&&d<PixelSize*.75)?1:0);
                }
                float a=max(ring*.75,splash*.9)*saturate(_RainRipples*1.5);
                clip(a-.01);
                half3 tint=saturate(_PixelSky.rgb*1.35+.12);
                return half4(tint,a);
            }
            ENDHLSL
        }
    }
}
