// Toon creature — the opaque shader every animal of the Animal Orchestra is drawn with.
//
// A soft two-tone ramp on the main light (lit side and shade side, with a narrow blend between),
// ambient from the scene's trilight, a rim light that brightens silhouettes against the dusk sky,
// and emission for the parts that glow on a note. Flat, bold shading reads from across a CAVE and
// hides the low polygon count of the primitive-built animals.
//
// Colour and emission are per renderer, set through a property block by CreatureTint, and are
// instanced properties: with instancing on, every sphere of every animal is one draw.
//
// No additional lights (this project's URP asset has them off), no shadows (they would cross the
// screens' seams differently per camera and the shapes read better without them).
Shader "VRMusic/Toon Creature"
{
    Properties
    {
        _BaseColor ("Colour", Color) = (0.8, 0.8, 0.8, 1)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _ShadeFloor ("Shade side brightness", Range(0, 1)) = 0.45
        _RampCentre ("Ramp centre", Range(-1, 1)) = 0.1
        _RampWidth ("Ramp width", Range(0.01, 1)) = 0.3
        _RimColor ("Rim colour", Color) = (1.0, 0.82, 0.70, 1)
        _RimStrength ("Rim strength", Range(0, 2)) = 0.45
        _RimPower ("Rim power", Range(0.5, 8)) = 3
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Opaque"
            "Queue" = "Geometry"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        CBUFFER_START(UnityPerMaterial)
            half _ShadeFloor;
            half _RampCentre;
            half _RampWidth;
            half4 _RimColor;
            half _RimStrength;
            half _RimPower;
        CBUFFER_END

        // Per renderer, from CreatureTint's property block.
        UNITY_INSTANCING_BUFFER_START(CreatureProps)
            UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
            UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
        UNITY_INSTANCING_BUFFER_END(CreatureProps)
        ENDHLSL

        Pass
        {
            Name "ForwardLit"
            Tags { "LightMode" = "UniversalForward" }

            Cull Back
            ZWrite On
            ZTest LEqual

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);

                VertexPositionInputs positions = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionCS = positions.positionCS;
                OUT.positionWS = positions.positionWS;
                OUT.normalWS = TransformObjectToWorldNormal(IN.normalOS);
                OUT.fogFactor = ComputeFogFactor(positions.positionCS.z);
                return OUT;
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half3 baseColor = UNITY_ACCESS_INSTANCED_PROP(CreatureProps, _BaseColor).rgb;
                half3 emission = UNITY_ACCESS_INSTANCED_PROP(CreatureProps, _EmissionColor).rgb;

                half3 normal = normalize(IN.normalWS);
                half3 view = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                Light light = GetMainLight();

                // Two tones: the lit side and the shade side, with a short blend between them.
                half ndl = dot(normal, light.direction);
                half ramp = smoothstep(_RampCentre - _RampWidth * 0.5h, _RampCentre + _RampWidth * 0.5h, ndl);
                half lit = lerp(_ShadeFloor, 1.0h, ramp);

                // Rim: silhouettes catch a warm edge, which keeps dark animals readable against the sky.
                half rim = pow(1.0h - saturate(dot(normal, view)), _RimPower) * _RimStrength;

                half3 ambient = SampleSH(normal);
                half3 color = baseColor * (ambient + light.color * lit)
                            + rim * _RimColor.rgb * (0.35h + 0.65h * baseColor)
                            + emission;

                color = MixFog(color, IN.fogFactor);
                return half4(color, 1.0h);
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthOnly"
            Tags { "LightMode" = "DepthOnly" }

            Cull Back
            ZWrite On
            ColorMask R

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex DepthVert
            #pragma fragment DepthFrag
            #pragma multi_compile_instancing

            struct DepthAttributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct DepthVaryings
            {
                float4 positionCS : SV_POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            DepthVaryings DepthVert(DepthAttributes IN)
            {
                DepthVaryings OUT;
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_TRANSFER_INSTANCE_ID(IN, OUT);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(OUT);
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                return OUT;
            }

            half DepthFrag(DepthVaryings IN) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);
                return IN.positionCS.z;
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Unlit"
}
