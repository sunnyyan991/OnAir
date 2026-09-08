Shader "OnAir/PixelWeather"
{
    Properties { _Tint("Tint", Color) = (1,1,1,1) _Shape("Shape", Float) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "RenderPipeline"="UniversalPipeline" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Attributes { float4 positionOS:POSITION; float4 color:COLOR; float2 uv:TEXCOORD0; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
            CBUFFER_START(UnityPerMaterial)
            half4 _Tint;
            float _Shape;
            CBUFFER_END
            Varyings vert(Attributes v) { Varyings o; o.positionCS=TransformObjectToHClip(v.positionOS.xyz); o.color=v.color*_Tint; o.uv=v.uv; return o; }
            half4 frag(Varyings i):SV_Target
            {
                float2 p=floor(saturate(i.uv)*5);
                if (_Shape > .5 && _Shape < 1.5) clip((abs(p.x-2)<.5 || abs(p.y-2)<.5) ? 1 : -1);
                if (_Shape > 1.5) clip((abs(p.x-2)+abs(p.y-2)<3) ? 1 : -1);
                return i.color;
            }
            ENDHLSL
        }
    }
}
