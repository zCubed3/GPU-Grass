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

Shader "zCubed/Grass/Encoders/Default"
{
    Properties
    {
        _MaterialIndex ("Material Index", float) = 0
        _NoiseScale ("Noise Scale", float) = 10
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        //ZTest Greater

        // Pass 0: Encodes non-blocking geometry
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "ShaderInclude/GrassCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL0;
                float3 positionWS : TEXCOORD0;
            };

            int _MaterialIndex;
            float _NoiseScale;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float blend = noise(i.positionWS.xz / _NoiseScale);
                return EncodeGrass(i.normal, i.vertex, _MaterialIndex + blend);
            }
            ENDCG
        }

        // Pass 1: Encodes blocking geometry
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "ShaderInclude/GrassCommon.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL0;
            };

            int _MaterialIndex;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                return 0;
            }
            ENDCG
        }

        // Pass 2: Encodes biome data
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"
            #include "ShaderInclude/GrassCommon.hlsl"
            #include "ShaderInclude/GrassSampling.hlsl"

            struct appdata
            {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : NORMAL0;
                float3 positionWS : TEXCOORD0;
            };

            int _MaterialIndex;
            float _NoiseScale;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.normal = UnityObjectToWorldNormal(v.normal);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex);
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float grad = noise(i.positionWS.xz / 10);

                float u = _MaterialIndex;
                float v = grad * (64 + 1);

                float4 lut = read_lut_blend_2d(float2(u, v));

                return lut;
            }
            ENDCG
        }
    }
}
