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

Shader "zCubed/Grass/Debug/Overdraw"
{
    Properties
    {
        [Header(Tweakables)]
        _SoftlightMin ("Softlight Min", float) = 0.1
        _GrassDistortBias ("Distortion Height Bias", float) = 2.0
        _GrassDistortStrength ("Distortion Strength", float) = 0.5

        [Header(Wind)]
        _WindTex ("Wind Texture", 2D) = "bump" {}
        _WindScale ("Wind Texture Scale", float) = 100.0
        _WindStrength ("Wind Strength", float) = 0.2
        _WindSpeed ("Wind Speed", float) = 0.95
        _WindLod ("Wind LOD", Range(0, 16)) = 0.0

        [Toggle(_USE_WIND_TEX)] _UseWindTex ("Use Wind Texture?", Int) = 0
    }
    SubShader
    {
        Tags { 
            "Queue"="Geometry+200" 
            "RenderType"="Opaque" 
        }
        LOD 100
        Cull Off

        Pass
        {
            Name "Visualization"
            Blend One One
            
            //ZTest Always
            ZWrite Off


            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 4.5

            // -------------------------------------
            // Unity keywords
            #pragma multi_compile_fog
            //#pragma multi_compile_instancing
            #pragma multi_compile_fwdbase
            #pragma multi_compile _ UNITY_STEREO_INSTANCING_ENABLED

            // -------------------------------------
            // Grass keywords
            #pragma multi_compile_vertex _ _USE_WIND_TEX
            #pragma multi_compile _ _GRASS_SPI_SUPPORT

            #include "UnityCG.cginc"
            #include "UnityInstancing.cginc"
            #include "AutoLight.cginc"
            #include "UnityLightingCommon.cginc"

            #include "ShaderInclude/GrassCommon.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
                float2 uv : TEXCOORD0;

#if defined(UNITY_STEREO_INSTANCING_ENABLED)
                UNITY_VERTEX_INPUT_INSTANCE_ID
#endif
            };

            grass_v2f vert (appdata v, uint instanceID : SV_INSTANCEID)
            {
                grass_v2f o;

#if defined(UNITY_STEREO_INSTANCING_ENABLED)
                UNITY_SETUP_INSTANCE_ID(v);
                UNITY_INITIALIZE_OUTPUT(grass_v2f, o);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);
#endif

                grass_instance_t grass = GetGrassInfo(instanceID);

                o.color = grass.color;

                unity_ObjectToWorld = grass.model;
                unity_WorldToObject = grass.modelInverse;

                float3 vertex;
                float3 normal;
                WindDistortion(v.vertex, v.normal, vertex, normal);

                o.pos = UnityWorldToClipPos(vertex);
                o.normal = UnityObjectToWorldNormal(normalize(normal));
                o.position = vertex;
                o.ambient = o.color * ShadeSH9(float4(o.normal, 1));

                return o;
            }

            half4 frag (grass_v2f i, half facing : VFACE) : SV_Target
            {
#if defined(UNITY_STEREO_INSTANCING_ENABLED)
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);
#endif

                return i.color * 0.01;
                //return half4(0.04, 0.02, 0.1, 0.0);
            }
            ENDHLSL
        }
    }
}
