Shader "OnAir/AircraftPixel"
{
    Properties { _BaseMap("Metre-scaled pixels",2D)="white"{} _Accent("Livery accent",Color)=(.38,.17,.20,1) _BaseColor("Tint",Color)=(1,1,1,1) _EmissionColor("Emission",Color)=(0,0,0,1) }
    SubShader
    {
        Tags {"RenderType"="Opaque" "RenderPipeline"="UniversalPipeline"}
        Pass
        {
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Resources/PixelClouds/CloudProjection.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST,_Accent,_BaseColor,_EmissionColor;
            CBUFFER_END
            float4 _PixelSky,_PixelEquator,_PixelGround;
            struct A {float4 p:POSITION;float3 n:NORMAL;float2 uv:TEXCOORD0;};
            struct V {float4 p:SV_POSITION;float3 n:TEXCOORD0;float2 uv:TEXCOORD1;float3 world:TEXCOORD2;};
            V vert(A a){V o;o.world=TransformObjectToWorld(a.p.xyz);o.p=TransformObjectToHClip(a.p.xyz);o.n=TransformObjectToWorldNormal(a.n);o.uv=a.uv;return o;}
            half4 frag(V i):SV_Target
            {
                half4 tex=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);
                half3 albedo=lerp(tex.rgb,_Accent.rgb,tex.a)*_BaseColor.rgb;
                half3 normal=normalize(i.n);Light sun=GetMainLight();
                half3 ambient=lerp(_PixelEquator.rgb,normal.y>=0?_PixelSky.rgb:_PixelGround.rgb,abs(normal.y));
                return half4(albedo*(ambient*.6+sun.color*saturate(dot(normal,sun.direction))*.85*OnAirCloudAttenuation(i.world))+_EmissionColor.rgb,1);
            }
            ENDHLSL
        }
    }
}
