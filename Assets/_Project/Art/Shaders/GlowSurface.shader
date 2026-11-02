// Glow surface — see-through, self-lit parts of the Animal Orchestra: the jellyfish's bells and
// tentacles, the moon, the whale under the water.
//
// A faint body (colour alpha), a Fresnel rim that brightens where the surface turns away from the
// eye — which is what makes a jellyfish read as a bell rather than a disc, and changes with every
// step the tracked viewer takes — and emission on top. Drawn from both sides with the back face's
// normal flipped, so a bell seen from below is lit inside.
//
// Premultiplied alpha: the body darkens a little of what is behind it and the glow adds, so it reads
// against the bright dusk horizon and the dark water alike. No depth write, so overlapping parts never
// cut holes in each other; four CAVE cameras sorting differently costs nothing visible this way.
//
// _VertexColor = 1 multiplies by the mesh's vertex colour (the whale carries its belly-to-edge shading
// there). Unity's primitives have no vertex colour, so it is off by default.
Shader "VRMusic/Glow Surface"
{
    Properties
    {
        _BaseColor ("Colour (alpha = body opacity)", Color) = (0.5, 0.8, 1.0, 0.25)
        [HDR] _EmissionColor ("Emission", Color) = (0, 0, 0, 1)
        _RimStrength ("Rim strength", Range(0, 4)) = 1.4
        _RimPower ("Rim power", Range(0.5, 8)) = 2.2
        [Toggle] _VertexColor ("Use vertex colour", Float) = 0
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }
        LOD 100

        Pass
        {
            Name "ForwardGlow"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            ZWrite Off
            ZTest LEqual
            Cull Off

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_fog
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half _RimStrength;
                half _RimPower;
                half _VertexColor;
            CBUFFER_END

            UNITY_INSTANCING_BUFFER_START(GlowProps)
                UNITY_DEFINE_INSTANCED_PROP(float4, _BaseColor)
                UNITY_DEFINE_INSTANCED_PROP(float4, _EmissionColor)
            UNITY_INSTANCING_BUFFER_END(GlowProps)

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                half4  color      : COLOR;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                half3  normalWS   : TEXCOORD1;
                half   fogFactor  : TEXCOORD2;
                half4  color      : COLOR;
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
                OUT.color = lerp(half4(1, 1, 1, 1), IN.color, _VertexColor);
                return OUT;
            }

            half4 Frag(Varyings IN, FRONT_FACE_TYPE frontFace : FRONT_FACE_SEMANTIC) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(IN);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(IN);

                half4 baseColor = UNITY_ACCESS_INSTANCED_PROP(GlowProps, _BaseColor) * IN.color;
                half3 emission = UNITY_ACCESS_INSTANCED_PROP(GlowProps, _EmissionColor).rgb * IN.color.rgb;

                half3 normal = normalize(IN.normalWS) * IS_FRONT_VFACE(frontFace, 1.0h, -1.0h);
                half3 view = GetWorldSpaceNormalizeViewDir(IN.positionWS);
                half facing = saturate(abs(dot(normal, view)));
                half rim = pow(1.0h - facing, _RimPower) * _RimStrength;

                half3 ambient = SampleSH(normal);
                half body = baseColor.a;

                // Premultiplied: body light weighted by its opacity, rim and emission added on top.
                half3 color = baseColor.rgb * (body * (0.35h + ambient) + rim) + emission;
                // The rim covers what is behind it only as much as it has colour of its own, so an emission-only
                // part with its glow off (the woodpecker's peck spot) is truly invisible, not a dark ring.
                half rimCover = rim * 0.5h * max(baseColor.r, max(baseColor.g, baseColor.b));
                half alpha = saturate(body + rimCover);

                color = MixFog(color, IN.fogFactor);
                return half4(color, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
