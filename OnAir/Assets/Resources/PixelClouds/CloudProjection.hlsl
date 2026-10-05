#ifndef ONAIR_CLOUD_PROJECTION
#define ONAIR_CLOUD_PROJECTION
TEXTURE2D(_OnAirCloudShadowMap); SAMPLER(sampler_OnAirCloudShadowMap);
float4 _OnAirCloudShadowBounds, _OnAirCloudShadowSlope, _OnAirCloudShadowHeights;
float _OnAirCloudShadowStrength;
half OnAirCloudAttenuation(float3 positionWS)
{
    if(_OnAirCloudShadowStrength<=0) return 1;
    float2 projected=positionWS.xz-positionWS.y*_OnAirCloudShadowSlope.xy;
    float2 uv=(projected-_OnAirCloudShadowBounds.xy)*_OnAirCloudShadowBounds.zw;
    if(any(uv<0)||any(uv>1))return 1;
    half3 coverage=SAMPLE_TEXTURE2D_LOD(_OnAirCloudShadowMap,sampler_OnAirCloudShadowMap,uv,0).rgb;
    coverage*=step(positionWS.y+.05,_OnAirCloudShadowHeights.xyz);
    return 1-max(coverage.r,max(coverage.g,coverage.b))*_OnAirCloudShadowStrength;
}
#endif
