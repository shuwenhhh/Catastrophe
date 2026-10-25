Shader "Catastrophe/Kitten Black White"
{
    Properties
    {
        _BaseMap("Original coat and eye colors",2D)="white"{}
        _BaseColor("Tint",Color)=(1,1,1,1)
        _Smoothness("Soft highlight",Range(0,1))=.18
    }
    SubShader
    {
        Tags {"RenderPipeline"="UniversalPipeline" "RenderType"="Opaque" "Queue"="Geometry"}
        Pass
        {
            Name "ForwardLit"
            Tags {"LightMode"="UniversalForward"}
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE _MAIN_LIGHT_SHADOWS_SCREEN
            #pragma multi_compile _ _ADDITIONAL_LIGHTS_VERTEX _ADDITIONAL_LIGHTS
            #pragma multi_compile_fragment _ _ADDITIONAL_LIGHT_SHADOWS
            #pragma multi_compile_fragment _ _SHADOWS_SOFT
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"
            TEXTURE2D(_BaseMap); SAMPLER(sampler_BaseMap);
            CBUFFER_START(UnityPerMaterial)
                float4 _BaseMap_ST; half4 _BaseColor; half _Smoothness;
            CBUFFER_END
            struct Attributes {float4 positionOS:POSITION;float3 normalOS:NORMAL;float2 uv:TEXCOORD0;};
            struct Varyings {float4 positionCS:SV_POSITION;float3 positionWS:TEXCOORD0;half3 normalWS:TEXCOORD1;float2 uv:TEXCOORD2;};
            Varyings vert(Attributes v)
            {
                Varyings o; VertexPositionInputs p=GetVertexPositionInputs(v.positionOS.xyz);
                o.positionCS=p.positionCS;o.positionWS=p.positionWS;
                o.normalWS=TransformObjectToWorldNormal(v.normalOS);o.uv=TRANSFORM_TEX(v.uv,_BaseMap);return o;
            }
            half4 frag(Varyings i):SV_Target
            {
                half3 original=SAMPLE_TEXTURE2D(_BaseMap,sampler_BaseMap,i.uv).rgb;
                half hi=max(original.r,max(original.g,original.b));
                half lo=min(original.r,min(original.g,original.b));
                half saturation=(hi-lo)/max(hi,.001h);
                half luminance=dot(original,half3(.2126,.7152,.0722));
                // Only neutral mid/dark fur: protect white fur, pink nose/ears
                // and saturated yellow-green iris. This does not edit the PNG.
                half pink=smoothstep(.30h,.50h,(original.r-original.g)/max(original.r,.001h))*smoothstep(.15h,.30h,hi);
                half coat=(1-smoothstep(.40h,.65h,hi))*(1-smoothstep(.50h,.80h,saturation))*(1-pink);
                half3 albedo=lerp(original,half3(.009h,.011h,.014h),coat)*_BaseColor.rgb;
                InputData inputData=(InputData)0;
                inputData.positionWS=i.positionWS;inputData.normalWS=normalize(i.normalWS);
                inputData.viewDirectionWS=GetWorldSpaceNormalizeViewDir(i.positionWS);
                inputData.shadowCoord=TransformWorldToShadowCoord(i.positionWS);
                inputData.bakedGI=SampleSH(inputData.normalWS);
                inputData.normalizedScreenSpaceUV=GetNormalizedScreenSpaceUV(i.positionCS);
                inputData.shadowMask=half4(1,1,1,1);
                SurfaceData surface=(SurfaceData)0;surface.albedo=albedo;surface.alpha=1;
                surface.metallic=0;surface.specular=half3(.02,.02,.02);
                surface.smoothness=_Smoothness;surface.normalTS=half3(0,0,1);surface.occlusion=1;
                return UniversalFragmentPBR(inputData,surface);
            }
            ENDHLSL
        }
        UsePass "Universal Render Pipeline/Lit/ShadowCaster"
        UsePass "Universal Render Pipeline/Lit/DepthOnly"
        UsePass "Universal Render Pipeline/Lit/DepthNormals"
    }
}
