Shader "Custom/ZeldaWater"
{
    Properties
    {
        _Color ("Main Water Color", Color) = (0.2, 0.6, 0.8, 0.8)
        _HighlightColor ("Foam / Highlight Color", Color) = (0.8, 0.9, 1.0, 1.0)
        _NoiseTex ("Ripples Noise Texture", 2D) = "white" {}
        
        // Separate speeds for the pond surface vs the waterfall!
        _PondScrollSpeed ("Pond Calm Speed", Vector) = (0.02, 0.02, 0, 0)
        _WaterfallScrollSpeed ("Waterfall Fall Speed", Vector) = (0.0, 1.5, 0, 0)
        
        _FoamThreshold ("Foam Amount", Range(0, 1)) = 0.5
    }
    SubShader
    {
        Tags { "RenderType"="Transparent" "Queue"="Transparent" }
        LOD 100
        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                float3 normal : NORMAL; // The shader now looks at the 3D shape!
            };

            struct v2f
            {
                float2 uv : TEXCOORD0;
                float4 vertex : SV_POSITION;
            };

            sampler2D _NoiseTex;
            float4 _NoiseTex_ST;
            float4 _Color;
            float4 _HighlightColor;
            float4 _PondScrollSpeed;
            float4 _WaterfallScrollSpeed;
            float _FoamThreshold;

            v2f vert (appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                
                // If the mesh points up (Y is 1), it's a flat pond. If it points sideways (Y is 0), it's a waterfall!
                float isFlat = abs(v.normal.y);
                
                // Choose the calm speed for ponds, and the fast speed for waterfalls
                float2 scrollSpeed = lerp(_WaterfallScrollSpeed.xy, _PondScrollSpeed.xy, step(0.5, isFlat));

                o.uv = TRANSFORM_TEX(v.uv, _NoiseTex) + (scrollSpeed * _Time.y);
                return o;
            }

            fixed4 frag (v2f i) : SV_Target
            {
                fixed4 noise = tex2D(_NoiseTex, i.uv);
                if (noise.r > _FoamThreshold) return _HighlightColor;
                return _Color;
            }
            ENDCG
        }
    }
}