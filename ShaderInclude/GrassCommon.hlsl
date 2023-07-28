/*
 * MIT License
 * 
 * Copyright (c) 2023 zCubed3
 * 
 * Permission is hereby granted, free of charge, to any person obtaining a copy
 * of this software and associated documentation files (the "Software"), to deal
 * in the Software without restriction, including without limitation the rights
 * to use, copy, modify, merge, publish, distribute, sublicense, and/or sell
 * copies of the Software, and to permit persons to whom the Software is
 * furnished to do so, subject to the following conditions:
 * 
 * The above copyright notice and this permission notice shall be included in all
 * copies or substantial portions of the Software.
 * 
 * THE SOFTWARE IS PROVIDED "AS IS", WITHOUT WARRANTY OF ANY KIND, EXPRESS OR
 * IMPLIED, INCLUDING BUT NOT LIMITED TO THE WARRANTIES OF MERCHANTABILITY,
 * FITNESS FOR A PARTICULAR PURPOSE AND NONINFRINGEMENT. IN NO EVENT SHALL THE
 * AUTHORS OR COPYRIGHT HOLDERS BE LIABLE FOR ANY CLAIM, DAMAGES OR OTHER
 * LIABILITY, WHETHER IN AN ACTION OF CONTRACT, TORT OR OTHERWISE, ARISING FROM,
 * OUT OF OR IN CONNECTION WITH THE SOFTWARE OR THE USE OR OTHER DEALINGS IN THE
 * SOFTWARE.
*/

#ifndef GRASS_COMMON_INCLUDED
#define GRASS_COMMON_INCLUDED

#include "UnityCG.cginc"
#include "UnityInstancing.cginc"
#include "AutoLight.cginc"
#include "UnityLightingCommon.cginc"
#include "UnityStandardBRDF.cginc"
#include "UnityPBSLighting.cginc"

#include "GrassStruct.hlsl"

struct grass_shade_info_t {
    float3 position;
    float3 normal;
    float3 view;
    half3 color;
};

struct grass_v2f {
    float4 pos : SV_POSITION;
    float3 normal : TEXCOORD0;
    float3 position : TEXCOORD1;
    half4 color : TEXCOORD2;
    half3 ambient : TEXCOORD3;

    SHADOW_COORDS(4)
    UNITY_FOG_COORDS(5)

    UNITY_VERTEX_OUTPUT_STEREO
};

StructuredBuffer<grass_instance_t> _GrassInstanceInfos;
float4 _GrassInstanceCullRef;
float4 _GrassInstanceEncodeRef;
float4 _GrassInstanceBounds;

float _SoftlightMin;

Texture2D _GrassObstacleMap;
SamplerState sampler_GrassObstacleMap;
float _GrassDistortBias;
float _GrassDistortStrength;

Texture2D _WindTex;
SamplerState sampler_WindTex;
float _WindScale;
float _WindStrength;
float _WindSpeed;
float _WindLod;

grass_instance_t GetGrassInfo(uint index) {
#if defined(UNITY_STEREO_INSTANCING_ENABLED)
    uint actual = index / 2;
#else
    uint actual = index;
#endif

    grass_instance_t instance = (grass_instance_t)0;

    [branch]
    if (index < (uint)_GrassInstanceBounds.x) {
        instance = _GrassInstanceInfos[actual];
    }

    return instance;
}

void WindDistortion(in float3 baseVertex, in float3 baseNormal, out float3 vertex, out float3 normal) {
    // Distance falloff
    float4 origin = mul(unity_ObjectToWorld, float4(0, 0, 0, 1));   

    float2 cullVec = origin.xz - _GrassInstanceCullRef.xz;
    float cullDist = length(cullVec);   

    float distFalloff = saturate(1.0 - cullDist / _GrassInstanceCullRef.w); 

    // Distort the vertex to emulate wind
    float rootDelta = max(baseVertex.y, 0);
    rootDelta *= rootDelta;

    float2 encodeVec = origin.xz - _GrassInstanceEncodeRef.xz;
    float2 obstacleCoord = ((encodeVec / _GrassInstanceCullRef.w) + 1.0) / 2.0;
    float2 windCoord = origin.xz / _WindScale;

#ifdef _USE_WIND_TEX
    float2 wind = _WindTex.SampleLevel(sampler_WindTex, windCoord + (_Time.xx * _WindSpeed), _WindLod).xy;
    wind = ((wind - 0.5) * 2.0);
#else
    float2 wind = sin(windCoord + (_Time.zz * _WindSpeed));
#endif

    float4 obstacle = _GrassObstacleMap.SampleLevel(sampler_GrassObstacleMap, obstacleCoord, 0);
    float2 distortion = obstacle.xy;

    float heightFade = saturate(obstacle.z - (origin.y + _GrassDistortBias));
        
    float fade = lerp(obstacle.w, 1, heightFade);
    distortion = lerp(distortion, 0, heightFade);

    vertex = mul(unity_ObjectToWorld, float4(baseVertex * distFalloff * fade * origin.w, 1));

    float3 trueDistortion = float3(distortion.x, 0, distortion.y);
    trueDistortion *= rootDelta * _GrassDistortStrength;
    trueDistortion.y = 0;

    float3 trueWind = float3(wind.x, 0, wind.y);
    trueWind *= rootDelta * _WindStrength;
    trueWind.y = 0;

    vertex += trueWind + trueDistortion;
    //vertex = mul(unity_ObjectToWorld, float4(baseVertex, 1.0));
    normal = baseNormal;
}

half3 GrassPBR(in grass_shade_info_t info, in grass_v2f i) {
    half3 light = _WorldSpaceLightPos0;
    if (_WorldSpaceLightPos0.w > 0) {
        light = normalize(_WorldSpaceLightPos0 - info.position);
    }

    float3 view = normalize(UnityWorldSpaceViewDir(info.position));
    float3 halfway = normalize(view + light);

    float NdotL = saturate(dot(light, info.normal));
    float NdotV = saturate(dot(info.normal, view));
    float NdotH = saturate(dot(info.normal, halfway));

    float softLight = max(NdotL, _SoftlightMin);

    UNITY_LIGHT_ATTENUATION(atten, i, info.position)

    float lightAtten = atten * softLight;

    half3 direct = info.color * _LightColor0 * lightAtten;

    half4 color = 0;
    color.rgb = direct + i.ambient;

    UNITY_APPLY_FOG(i.fogCoord, color);
    return color.rgb;
}

inline half4 EncodeGrass(float3 normalWS, float3 vertexCS, float materialIndex)
{
    return float4(normalWS.z, -normalWS.x, materialIndex, vertexCS.z);
}

// https://gist.github.com/patriciogonzalezvivo/670c22f3966e662d2f83v
float rand(float2 n)
{
    return frac(sin(dot(n, float2(12.9898, 4.1414))) * 43758.5453);
}

float noise(float2 p) {
	float2 ip = floor(p);
	float2 u = frac(p);
	u = u*u*(3.0-2.0*u);
	
	float res = lerp(
		lerp(rand(ip),rand(ip+float2(1.0,0.0)),u.x),
		lerp(rand(ip+float2(0.0,1.0)),rand(ip+float2(1.0,1.0)),u.x),u.y);
	return res*res;
}

#endif