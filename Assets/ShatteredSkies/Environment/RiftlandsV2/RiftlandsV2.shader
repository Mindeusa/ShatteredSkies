Shader "ShatteredSkies/RiftlandsV2"
{
    Properties
    {
        [NoScaleOffset] _Panorama ("Sky Panorama", 2D) = "gray" {}
        _Exposure ("Exposure", Range(0, 2)) = 1
        _Rotation ("Yaw (degrees)", Range(0, 360)) = 0
    }

    SubShader
    {
        Tags { "Queue" = "Background" "RenderType" = "Background" "PreviewType" = "Skybox" }
        Cull Off
        ZWrite Off

        Pass
        {
            Name "Sky"
            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing
            #include "UnityCG.cginc"

            sampler2D _Panorama;
            float _Exposure;
            float _Rotation;

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 direction : TEXCOORD0;
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_INITIALIZE_OUTPUT(Varyings, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);
                output.positionCS = UnityObjectToClipPos(input.positionOS);
                output.direction = input.positionOS.xyz;
                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float3 direction = normalize(input.direction);
                float2 uv = float2(
                    0.5 + atan2(direction.x, direction.z + 1e-10) * (0.5 / UNITY_PI) - _Rotation / 360.0,
                    0.5 + asin(clamp(direction.y, -1.0, 1.0)) / UNITY_PI);

                // Keep mip selection continuous across the longitude seam.
                float2 dx = ddx(uv);
                float2 dy = ddy(uv);
                dx.x -= round(dx.x);
                dy.x -= round(dy.x);
                return half4(tex2Dgrad(_Panorama, uv, dx, dy).rgb * _Exposure, 1);
            }
            ENDHLSL
        }
    }
    Fallback Off
}



