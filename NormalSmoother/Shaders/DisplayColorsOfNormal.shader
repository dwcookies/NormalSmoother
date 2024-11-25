Shader "NormalSmoother/DisplayColorsOfNormal"
{
    Properties
    {
        [Toggle] _DisplayColors ("显示顶点色法线颜色（否则显示法线颜色）", Int) = 0
    }
    SubShader
    {
        Tags { "RenderType"="Opaque" }

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #pragma multi_compile _ _DISPLAYCOLORS_ON

            struct appdata
            {
                float4 vertex : POSITION;
                float3 normal : NORMAL;
                #ifdef _DISPLAYCOLORS_ON
                    float4 tangent : TANGENT;
                    float4 color : COLOR;
                #endif
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float3 normal : TEXCOORD0;
                #ifdef _DISPLAYCOLORS_ON
                    float4 color : TEXCOORD1;
                    float3 TBN1 : TEXCOORD2;
                    float3 TBN2 : TEXCOORD3;
                    float3 TBN3 : TEXCOORD4;
                #endif
            };

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = TransformObjectToHClip(v.vertex);
                o.normal = v.normal;//要看的是模型自身的法线，所以不需要转到世界空间
                
                #ifdef _DISPLAYCOLORS_ON
                    o.color = v.color;
                
                    float4 tangent = v.tangent;//因为只用于检查法线，所以此处也不转到世界空间，下面同理
                    float3 normal = v.normal;
                    float3 bitangent = cross(normal, tangent.xyz) * tangent.w;
                    o.TBN1 = float3(tangent.x, bitangent.x, normal.x);
                    o.TBN2 = float3(tangent.y, bitangent.y, normal.y);
                    o.TBN3 = float3(tangent.z, bitangent.z, normal.z);
                #endif
                
                return o;
            }

            half4 frag (v2f i) : SV_Target
            {
                #ifdef _DISPLAYCOLORS_ON
                    float3x3 TBN = float3x3(i.TBN1, i.TBN2, i.TBN3);
                
                    float3 normal = i.color.xyz;
                    normal.x = normal.x * 2.0 - 1.0;
                    normal.y = normal.y * 2.0 - 1.0;
                    normal.z = sqrt(1.0 - saturate(normal.x * normal.x + normal.y * normal.y));
                    normal = mul(TBN, normal);
                    return half4(normalize(normal) * 0.5 + 0.5, 1.0);
                #else
                    float3 normal = normalize(i.normal);
                    half3 normalColor = normal * 0.5 + 0.5;
                    return half4(normalColor.xyz, 1.0);
                #endif
            }
            ENDHLSL
        }
    }
}









