Shader "HookWars/RiverFlow"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _WaterTex ("Water Texture", 2D) = "white" {}
        _Tint ("Tint", Color) = (1, 1, 1, 1)
        _TileSize ("Texture Tile Size (meters)", Float) = 4
        _FlowSpeed ("Flow Speed (meters per second)", Float) = 0.5
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "Queue" = "Transparent"
            "RenderType" = "Transparent"
        }

        Cull Off
        ZWrite Off
        Blend SrcAlpha OneMinusSrcAlpha

        Pass
        {
            Tags { "LightMode" = "Universal2D" }

            HLSLPROGRAM

            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            TEXTURE2D(_WaterTex);
            SAMPLER(sampler_WaterTex);

            CBUFFER_START(UnityPerMaterial)
                float4 _Tint;
                float _TileSize;
                float _FlowSpeed;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float4 color : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 worldPosition : TEXCOORD0;
                float4 color : COLOR;
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;

                output.positionCS =
                    TransformObjectToHClip(input.positionOS.xyz);

                output.worldPosition =
                    TransformObjectToWorld(input.positionOS.xyz).xy;

                output.color = input.color;

                return output;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                float tileSize = max(_TileSize, 0.01);

                float2 uv = input.worldPosition / tileSize;

                // Положительный сдвиг выборки по Y
                // визуально перемещает рисунок вниз.
                uv.y += _Time.y * _FlowSpeed / tileSize;

                half4 water = SAMPLE_TEXTURE2D(
                    _WaterTex,
                    sampler_WaterTex,
                    uv
                );

                return water * _Tint * input.color;
            }

            ENDHLSL
        }
    }
}