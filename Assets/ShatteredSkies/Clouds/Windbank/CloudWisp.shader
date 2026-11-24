Shader "ShatteredSkies/Cloud Wisp"
{
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            struct Varyings { float4 pos : SV_POSITION; half4 color : COLOR; float2 uv : TEXCOORD0; half fog : TEXCOORD1; };
            Varyings Vert(float4 vertex : POSITION, half4 color : COLOR, float2 uv : TEXCOORD0)
            {
                Varyings o;
                o.pos = TransformObjectToHClip(vertex.xyz);
                o.color = color;
                o.uv = uv*2-1;
                o.fog = ComputeFogFactor(o.pos.z);
                return o;
            }
            half4 Frag(Varyings i) : SV_Target
            {
                float2 p = i.uv;
                p.y += .16*sin(p.x*5);
                half opacity = 1-smoothstep(.05,1,dot(p,p));
                return half4(MixFog(i.color.rgb,i.fog),0.75h*i.color.a*opacity*opacity);
            }
            ENDHLSL
        }
    }
}

