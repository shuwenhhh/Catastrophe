Shader "Catastrophe/Tripo Vertex Color Lit"
{
    Properties { _BaseMap("Base Map", 2D) = "white" {} _BaseColor("Base Color", Color) = (1,1,1,1) _Smoothness("Smoothness", Range(0,1)) = .25 }
    SubShader
    {
        Tags { "RenderType"="Opaque" "RenderPipeline"="UniversalPipeline" "Queue"="Geometry" }
        Pass
        {
            Name "UniversalForward" Tags { "LightMode"="UniversalForward" }
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial) float4 _BaseMap_ST; float4 _BaseColor; float _Smoothness; CBUFFER_END
            struct Attributes { float4 positionOS:POSITION; float3 normalOS:NORMAL; float2 uv:TEXCOORD0; float4 color:COLOR; };
            struct Varyings { float4 positionHCS:SV_POSITION; float3 normalWS:TEXCOORD0; float2 uv:TEXCOORD1; float4 color:COLOR; float3 positionWS:TEXCOORD2; };
            Varyings vert(Attributes v) { Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz); o.positionHCS=p.positionCS; o.positionWS=p.positionWS; o.normalWS=TransformObjectToWorldNormal(v.normalOS); o.uv=TRANSFORM_TEX(v.uv,_BaseMap); o.color=v.color; return o; }
            half4 frag(Varyings i):SV_Target { half4 albedo=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv)*_BaseColor*i.color; Light l=GetMainLight(TransformWorldToShadowCoord(i.positionWS)); half ndl=saturate(dot(normalize(i.normalWS),l.direction)); half3 ambient=SampleSH(normalize(i.normalWS)); albedo.rgb*=ambient+l.color*(.32h+.68h*ndl)*l.shadowAttenuation; return albedo; }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
    }
}
