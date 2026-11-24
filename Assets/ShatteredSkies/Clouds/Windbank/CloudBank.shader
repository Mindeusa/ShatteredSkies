Shader "ShatteredSkies/Cloud Bank"
{
    Properties
    {
        _Opacity ("Opacity", Range(0,1)) = 0.72
        _Crown ("Sunlit ivory", Color) = (1, .956, .842, 1)
        _Base ("Underside", Color) = (.57, .75, .77, 1)
    }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _Crown, _Base; half _Opacity;
            CBUFFER_END
            struct Varyings { float4 pos : SV_POSITION; half light : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(float4 vertex : POSITION, float3 normal : NORMAL)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(vertex.xyz);
                float3 n = TransformObjectToWorldNormal(normal);
                o.light = saturate(.39 + .46 * dot(n, normalize(float3(.32,.77,-.56))) + .17*n.y);
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                half3 color = lerp(_Base.rgb, _Crown.rgb, smoothstep(.03,.91,i.light));
                return half4(MixFog(color,i.fog),_Opacity);
            }
            ENDHLSL
        }
    }
}

