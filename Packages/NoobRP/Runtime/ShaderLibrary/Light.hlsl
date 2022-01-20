#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

float4 _DirectionalLightColor;
float3 _DirectionalLightDirection;

struct DirectionalLight {
    float3 color;
    float3 directionWS;
};


DirectionalLight GetDirectionalLight () {
    DirectionalLight light;
    light.color = _DirectionalLightColor.xyz;
    light.directionWS = _DirectionalLightDirection;
    return light;
}

#endif