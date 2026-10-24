Shader "HookWars/FieldOverlay"
{
    Properties { _BaseColor("Tint", Color)=(1,1,1,1) _UseVertexColor("Vertex colors", Float)=0 }
    SubShader
    {
        Tags { "RenderPipeline"="UniversalPipeline" "RenderType"="Transparent" "Queue"="Transparent" }
        Pass
        {
            Blend SrcAlpha OneMinusSrcAlpha
            ZWrite Off
            Cull Off
            HLSLPROGRAM
            #pragma vertex Vert
            #pragma fragment Frag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor; half _UseVertexColor;
            CBUFFER_END
            struct Attributes { float3 positionOS:POSITION; half4 color:COLOR; };
            struct Varyings { float4 positionCS:SV_POSITION; half4 color:COLOR; };
            Varyings Vert(Attributes i) { Varyings o; o.positionCS=TransformObjectToHClip(i.positionOS); o.color=lerp(half4(1,1,1,1),i.color,_UseVertexColor); return o; }
            half4 Frag(Varyings i):SV_Target { return i.color*_BaseColor; }
            ENDHLSL
        }
    }
}
