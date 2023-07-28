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

Shader "zCubed/Flora/Terrain/Debug"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }
        LOD 100

        Pass
        {
            Name "ForwardBase"
            Tags {"LightMode" = "ForwardBase"}

            ZWrite On
            ZTest Less

            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            
            // -------------------------------------
            // Unity keywords
            #pragma multi_compile_fog
            #pragma multi_compile_fwdbase

            #include "UnityCG.cginc"

            #include "ShaderInclude/GrassCommon.hlsl"
            #include "ShaderInclude/GrassSampling.hlsl"

            sampler2D _GlobalSurfaceLUT;

            struct appdata
            {

                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 normal : NORMAL;
                float4 color : COLOR;
            };

            struct v2f
            {
                float4 pos : SV_POSITION;
                float2 uv : TEXCOORD0;

                float4 positionWS : TEXCOORD1;
                float3 normalWS : TEXCOORD2;
                float3 ambient : TEXCOORD3;
    
                SHADOW_COORDS(4)
                UNITY_FOG_COORDS(5)

                float4 color : TEXCOORD6;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.positionWS = mul(unity_ObjectToWorld, v.vertex);
                o.normalWS = UnityObjectToWorldNormal(v.normal);
                o.ambient = ShadeSH9(float4(o.normalWS, 1));
                o.color = v.color;

                TRANSFER_SHADOW(o);
                UNITY_TRANSFER_FOG(o, o.pos);

                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                float grad = noise(i.positionWS.xz / 10);
                grad = i.color.y;

                float index = i.color.x;

                float u = index * (_GrassState.y + 1);
                float v = grad * (_GrassState.z + 1);

                float4 lut = read_lut_blend_2d(float2(u, v));

                half3 light = _WorldSpaceLightPos0;
                if (_WorldSpaceLightPos0.w > 0) {
                    light = normalize(_WorldSpaceLightPos0 - i.positionWS);
                }
            
                float3 normal = normalize(i.normalWS);
                float3 view = normalize(UnityWorldSpaceViewDir(i.positionWS));
                float3 halfway = normalize(view + light);
            
                float NdotL = saturate(dot(light, normal));
                float NdotV = saturate(dot(normal, view));
                float NdotH = saturate(dot(normal, halfway));
            
                UNITY_LIGHT_ATTENUATION(atten, i, i.positionWS)
            
                float lightAtten = atten * NdotL;
            
                half3 base_color = lut.rgb;

                // https://www.martinpalko.com/triplanar-mapping/#Implementation%20-%20Unity
                float texture_scale = 1;
                float texture_sharpness = 1;

			    half2 uv_x = i.positionWS.zy / texture_scale;
			    half2 uv_y = i.positionWS.xz / texture_scale;
			    half2 uv_z = i.positionWS.xy / texture_scale;

			    half3 x_sample = tex2D (_MainTex, uv_x);
			    half3 y_sample = tex2D (_MainTex, uv_y);
			    half3 z_sample = tex2D (_MainTex, uv_z);

			    half3 blendWeights = pow(abs(normal), texture_sharpness);
			
                // Divide our blend mask by the sum of it's components, this will make x+y+z=1
			    blendWeights = blendWeights / (blendWeights.x + blendWeights.y + blendWeights.z);
			
                // Finally, blend together all three samples based on the blend mask.
			    //base_color = x_sample * blendWeights.x + y_sample * blendWeights.y + z_sample * blendWeights.z;

                half3 direct = base_color * _LightColor0 * lightAtten;
            
                half4 color = 1;
                color.rgb = direct + (base_color * i.ambient);
            
                UNITY_APPLY_FOG(i.fogCoord, color);
                
                return color;
            }
            ENDHLSL
        }
        Pass 
        {
            Name "ShadowCaster"
            Tags {"LightMode" = "ShadowCaster"}

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            #pragma target 4.5

            // -------------------------------------
            // Unity keywords

            #include "ShaderInclude/GrassCommon.hlsl"

            struct appdata {
                float4 vertex : POSITION;
                float4 normal : NORMAL;
            };
            
            struct v2f {
                float4 pos : SV_POSITION;
            };

            v2f vert (appdata v)
            {
                v2f o;

                o.pos = UnityObjectToClipPos(v.vertex);

                return o;
            }

            half4 frag (v2f i) : SV_Target2
            {
                return 0;
            }
            ENDHLSL
        }
    }
}
