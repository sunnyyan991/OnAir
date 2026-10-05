// Keep URP surface/BRDF, ambient and emission intact. Only direct sun is occluded.
#include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Shadows.hlsl"
#include "Assets/Resources/PixelClouds/CloudProjection.hlsl"
half OnAirMainLightShadow(float4 coord,float3 positionWS,half4 mask,half4 channels)
{
    return min(MainLightShadow(coord,positionWS,mask,channels),OnAirCloudAttenuation(positionWS));
}
#define MainLightShadow OnAirMainLightShadow
