Shader "OnAir/ObjectPixelArt"
{
    Properties
    {
        [MainTexture] _BaseMap("Authored object pixels",2D)="white"{}
        _NormalMap("Local normals and surface class",2D)="white"{}
        _DepthMap("Object surface depth",2D)="black"{}
        _WindowMap("Stable individual window identifiers",2D)="black"{}
        _Occupancy("Night occupancy",Range(0,1))=.48
        [MainColor] _BaseColor("Facade palette",Color)=(.78,.78,.76,1)
        [HDR] _EmissionColor("Night windows",Color)=(0,0,0,1)
        _DepthRange("Depth range",Float)=1
        _ViewForward("Baked view forward",Vector)=(0,0,1,0)
        _ObjectSize("Artwork world size",Vector)=(1,1,0,0)
        _TextureSize("Artwork pixel dimensions",Vector)=(1,1,1,1)
    }
    SubShader
    {
        // Depth reconstruction and stable window addresses require each object's
        // own matrix. Dynamic mesh batching would erase this coordinate system.
        Tags{"RenderType"="TransparentCutout" "Queue"="AlphaTest" "RenderPipeline"="UniversalPipeline" "DisableBatching"="True"}
        Pass
        {
            Name "ObjectPixels"
            Tags{"LightMode"="UniversalForward"}
            Cull Off ZWrite On
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma target 3.5
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            #include "Assets/Resources/PixelClouds/CloudProjection.hlsl"
            TEXTURE2D(_BaseMap);SAMPLER(sampler_BaseMap);
            TEXTURE2D(_NormalMap);SAMPLER(sampler_NormalMap);
            TEXTURE2D(_DepthMap);SAMPLER(sampler_DepthMap);
            TEXTURE2D(_WindowMap);SAMPLER(sampler_WindowMap);
            CBUFFER_START(UnityPerMaterial)
            float4 _BaseColor,_EmissionColor,_ViewForward,_ObjectSize,_TextureSize;float _DepthRange,_Occupancy;
            CBUFFER_END
            float4 _PixelSky,_PixelEquator,_PixelGround;
            float _PixelOriginZ,_PixelWorldSeed,_WindowSleepProgress;
            // Diagnostic comparison only; zero (default) uses edge-limited filtering.
            float _PixelPointComparison;
            uint PixelHash(uint v){v^=v>>16;v*=0x7feb352du;v^=v>>15;v*=0x846ca68bu;return v^(v>>16);}
            float PixelRandom(uint v){return (PixelHash(v)&65535u)/65535.0;}
            struct A{float4 positionOS:POSITION;float2 uv:TEXCOORD0;};
            struct V{float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;float2 uv:TEXCOORD1;nointerpolation uint building:TEXCOORD2;};
            V vert(A i)
            {
                V o;o.positionWS=TransformObjectToWorld(i.positionOS.xyz);o.positionCS=TransformWorldToHClip(o.positionWS);o.uv=i.uv;
                float2 origin=float2(unity_ObjectToWorld._m03,unity_ObjectToWorld._m23+_PixelOriginZ);
                o.building=PixelHash((uint)(int)round(origin.x*4))^PixelHash((uint)(int)round(origin.y*4)^0x9e3779b9u)^PixelHash((uint)abs(_PixelWorldSeed));return o;
            }
            struct Output{half4 color:SV_Target;float depth:SV_Depth;};
            half4 ShadeTexel(float2 uv,Light sun,uint building)
            {
                half4 color=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,uv);
                half4 encoded=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,uv);
                half3 normal=normalize(TransformObjectToWorldNormal(encoded.rgb*2-1));
                half facade=step(.25,encoded.a)*(1-step(.75,encoded.a));
                half3 albedo=color.rgb*lerp(half3(1,1,1),_BaseColor.rgb,facade);
                half3 ambient=lerp(_PixelEquator.rgb,normal.y>=0?_PixelSky.rgb:_PixelGround.rgb,abs(normal.y));
                half3 illumination=ambient*.6+sun.color*saturate(dot(normal,sun.direction))*sun.shadowAttenuation*.85;
                half3 glow=0;
                UNITY_BRANCH if(encoded.a>=.75 && dot(_EmissionColor.rgb,float3(1,1,1))>.001)
                {
                    float4 lightCode=SAMPLE_TEXTURE2D(_WindowMap,sampler_WindowMap,uv);
                    if(lightCode.b>.5){
                        half brightness=dot(_EmissionColor.rgb,half3(.2126,.7152,.0722))*lightCode.a;
                        half peak=max(.001,max(albedo.r,max(albedo.g,albedo.b)));
                        glow=(albedo/peak)*brightness*.70;
                    }else{
                    float2 code=lightCode.rg;
                    uint window=(uint)round(code.r*255)+(uint)round(code.g*255)*256u;
                    float occupied=saturate(_Occupancy+(PixelRandom(building)-.5)*.30);
                    float on=step(PixelRandom(window^building),occupied)*step(.5,(float)window);
                    // Independent, stable per-window retirement order, applied only to windows.
                    float remaining=lerp(1.0,.10,saturate(_WindowSleepProgress));
                    float retireRank=lerp(.002,.998,PixelRandom(window^building^0x41c64e6du));
                    on*=smoothstep(retireRank-.001,retireRank+.001,remaining);
                    half3 lampTint=lerp(half3(1,.82,.60),half3(.87,.94,1),step(.82,PixelRandom(window^building^719u)));
                    glow=_EmissionColor.rgb*on*lampTint*lerp(.52,.83,PixelRandom(window^building^173u));
                    }
                }
                return half4((albedo*illumination+glow)*color.a,color.a);
            }
            Output frag(V i)
            {
                half4 pointColor=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv);clip(pointColor.a-.5);
                // Integrate only a screen-pixel-wide band at each authored texel edge.
                // Interiors stay constant; no low-resolution screen buffer, temporal
                // history, camera snapping, mipmaps, or whole-image blur is involved.
                float2 texel=i.uv*_TextureSize.xy;
                float2 footprint=clamp(fwidth(texel),.0001,1.0);
                float2 phase=frac(texel);
                float2 edge=clamp(phase/footprint,0,.5)+clamp((phase-1)/footprint,-.5,0);
                float2 stableUV=(floor(texel)+.5+edge)*_TextureSize.zw;
                half4 encoded=SAMPLE_TEXTURE2D(_NormalMap,sampler_NormalMap,i.uv);
                float scale=length(float3(unity_ObjectToWorld._m00,unity_ObjectToWorld._m10,unity_ObjectToWorld._m20));
                float3 position=i.positionWS+_ViewForward.xyz*(SAMPLE_TEXTURE2D(_DepthMap,sampler_DepthMap,i.uv).r*_DepthRange*scale);
                half3 normal=normalize(TransformObjectToWorldNormal(encoded.rgb*2-1));
                float2 pixelCenter=(floor(i.uv*_TextureSize.xy)+.5)*_TextureSize.zw;
                float3 right=normalize(cross(float3(0,1,0),_ViewForward.xyz));float3 up=cross(_ViewForward.xyz,right);
                float3 shadingPosition=position+(right*(pixelCenter.x-i.uv.x)*_ObjectSize.x+up*(pixelCenter.y-i.uv.y)*_ObjectSize.y)*scale;
                Light sun=GetMainLight(TransformWorldToShadowCoord(shadingPosition+normal*.15));
                sun.shadowAttenuation=min(sun.shadowAttenuation,OnAirCloudAttenuation(shadingPosition));
                half4 shaded;
                UNITY_BRANCH if(_PixelPointComparison>.5)
                    shaded=ShadeTexel(i.uv,sun,i.building);
                else
                {
                    // Blend completed texel colours, not categorical normals/window IDs.
                    // One shadow query and one discrete depth sample are shared; never
                    // interpolate depth across separate walls or neighbouring buildings.
                    float2 address=stableUV*_TextureSize.xy-.5;
                    float2 baseUV=(floor(address)+.5)*_TextureSize.zw;
                    float2 fraction=frac(address),stepUV=_TextureSize.zw;
                    half4 a=ShadeTexel(baseUV,sun,i.building);
                    half4 b=ShadeTexel(baseUV+float2(stepUV.x,0),sun,i.building);
                    half4 c=ShadeTexel(baseUV+float2(0,stepUV.y),sun,i.building);
                    half4 d=ShadeTexel(baseUV+stepUV,sun,i.building);
                    shaded=lerp(lerp(a,b,fraction.x),lerp(c,d,fraction.x),fraction.y);
                }
                Output o;o.color=half4(shaded.rgb/max(shaded.a,.001),1);
                float4 clipPosition=TransformWorldToHClip(position);o.depth=clipPosition.z/clipPosition.w;
                #if !UNITY_REVERSED_Z
                o.depth=(o.depth-UNITY_NEAR_CLIP_VALUE)/(1-UNITY_NEAR_CLIP_VALUE);
                #endif
                return o;
            }
            ENDHLSL
        }
    }
}
