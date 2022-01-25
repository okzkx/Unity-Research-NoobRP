#ifndef CUSTOM_LIGHT_INCLUDED
#define CUSTOM_LIGHT_INCLUDED

#include "Shadow.hlsl"

float4 _DirectionalLightColor;
float3 _DirectionalLightDirection;
float4 _LightColors[6];
float4 _LightPositions[6];
float4 _LightDirections[6];

int _SpotLightCount;
int _PointLightCount;
#define SPOT_LIGHT_CAPCITY 4;

struct DirectionalLight {
    float3 color;
    float3 directionWS;
    float attenuation;
};

DirectionalLight GetDirectionalLight () {
    DirectionalLight light;
    light.color = _DirectionalLightColor.xyz;
    light.directionWS = _DirectionalLightDirection;
    // light.attenuation = GetDirectionalShadowAttenuation();
    return light;
}

struct SpotLight
{
    float3 color;
    float3 position;
    float3 direction;
    float angle;
    float range;
};

void ToSpotLightIndex(inout int index)
{
    
}

SpotLight CreateSpotLight(int index)
{
    ToSpotLightIndex(index);
    
    SpotLight light;
    light.color = _LightColors[index].xyz;
    light.position = _LightPositions[index].xyz;
    light.direction = _LightDirections[index].xyz;
    
    light.range = _LightPositions[index].w;
    light.angle = _LightDirections[index].w;
    return light;
}

struct PointLight
{
    float3 color;
    float3 position;
    float range;
};

void ToPointLightIndex(inout int index)
{
    index += SPOT_LIGHT_CAPCITY;
}

PointLight CreatePointLight(int index)
{
    ToPointLightIndex(index);
    
    PointLight light;
    light.color = _LightColors[index].xyz;
    light.position = _LightPositions[index].xyz;
    light.range = _LightPositions[index].w;
    return light;
}



#endif