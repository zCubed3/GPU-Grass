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

Shader "zCubed/Grass/Obstacles/Default"
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
            Name "ObstacleRender"

            Cull Off
            Blend One Zero, DstColor Zero 
            ZTest Greater
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float4 normal : NORMAL;
                float4 tangent : TANGENT;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            
                float3 normal : NORMAL0;
                float3 tangent : NORMAL1;
                float3 binormal : NORMAL2;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

                // Since we're using a quad, just assume
                o.normal = normalize(UnityObjectToWorldNormal(v.normal));
                o.tangent = normalize(UnityObjectToWorldDir(v.tangent));
                o.binormal = normalize(cross(o.normal, o.tangent) * v.tangent.w);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                float3x3 tan2World = float3x3(
					i.tangent,
					i.binormal,
					i.normal
				);

                //half4 info = saturate(tex2Dlod(_MainTex, float4(i.uv, 0, 0)));
                half4 info = saturate(tex2D(_MainTex, i.uv));
                half3 normal = float3((info.xy - 0.5) * 2.0, 1);

                normal = normalize(mul(normal, tan2World));
                info.xy = normal.xz * info.z; 
                info.z = i.worldPos.y;
                //info.z = 0;
                info.w = 1.0 - info.w;

                return info;
            }
            ENDCG
        }

        Pass
        {
            Name "EditorPreview"

            Cull Off
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 worldPos : TEXCOORD1;
            };

            sampler2D _MainTex;
            float4 _MainTex_ST;

            v2f vert (appdata v)
            {
                v2f o;

                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.worldPos = mul(unity_ObjectToWorld, v.vertex);

                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                half4 info = tex2D(_MainTex, i.uv);
                return info;
            }
            ENDCG
        }
    }
}
