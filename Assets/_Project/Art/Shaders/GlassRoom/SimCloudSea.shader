// A sea of cloud below the glass room, and the music running out across it as swells. The mesh is
// a flat polar grid round the room; the vertex shader lifts it from the spectrogram, so a swell
// leaves the room the moment it is heard and rolls outward, older music further away. Direction is
// pitch: the bass rises straight ahead of the front window, the treble round toward the back.
//
// Shaded like anime cloud: white where the sun catches it, lavender in the folds, a silver edge
// toward the sun, and the tops of the swells glowing with the level that raised them.
Shader "VRMusic/SimCloudSea"
{
    Properties
    {
        _LitColor ("Lit", Color) = (0.94, 0.94, 0.95, 1)
        _ShadowColor ("Shadow", Color) = (0.62, 0.66, 0.9, 1)
        [HDR] _CrestColor ("Crest Glow", Color) = (1, 0.82, 0.9, 1)
        _CrestGlow ("Crest Glow Strength", Range(0, 2)) = 0.45
        _Amplitude ("Swell Height (m)", Float) = 22
        _Billow ("Billow (m)", Float) = 9
        _BillowScale ("Billow Size (m)", Float) = 220
        _PuffScale ("Puff Size (m)", Float) = 30
        _PuffHeight ("Puff Height (m)", Float) = 6
        _HollowColor ("Hollow Shade", Color) = (0.72, 0.74, 0.92, 1)
        _Hollows ("Hollows", Range(0, 1)) = 0.6
        _Detail ("Puff Detail", Range(0, 1)) = 0.35
        _FogScale ("Fog", Range(0, 2)) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        Pass
        {
            Name "SimCloudSea"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _LitColor;
                float4 _ShadowColor;
                float4 _CrestColor;
                float _CrestGlow;
                float _Amplitude;
                float _Billow;
                float _BillowScale;
                float _PuffScale;
                float _PuffHeight;
                float4 _HollowColor;
                float _Hollows;
                float _Detail;
                float _FogScale;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                float3 normalWS : TEXCOORD1;
                float2 local : TEXCOORD2;
                float level : TEXCOORD3;
                float puff : TEXCOORD4;
            };

            // Height of the cloud top at a point round the room; the music's share of it; and how
            // high it stands among its own puffs, which the shading darkens the hollows by.
            float Height(float2 xz, out float level, out float puff)
            {
                float r = length(xz);
                float t = abs(atan2(xz.x, xz.y)) / GR_PI;     // 0 straight ahead, 1 straight behind
                float age = (r - _GR_Spec.w) / max(_GR_Spec.z, 1.0);
                float held = max(_GR_Spec.y, 0.01);
                float alive = step(0.0, age) * (1.0 - smoothstep(held * 0.7, held, age));

                // Neighbouring bands and slices blended, so a swell is a rounded hill rather than the
                // creased bilinear patch between four texels, which a sharp terminator shows as steps.
                float d = 1.0 / max(_GR_BandCount - 1.0, 1.0);
                float s = 0.12;                                // seconds: a slice and a half at 12 a second
                float a = max(age, 0.0);
                level = GR_SpecAt(t, a) * 0.4
                      + (GR_SpecAt(t - d, a) + GR_SpecAt(t + d, a)) * 0.15
                      + (GR_SpecAt(t, max(a - s, 0.0)) + GR_SpecAt(t, a + s)) * 0.15;
                level *= alive;

                float billow = (GR_Fbm(xz / _BillowScale + _GR_Clock * 0.004) - 0.5) * 2.0 * _Billow;

                // Puffs: rounded domes, flat between, the way a cloud top heaps up.
                float n = GR_Noise(xz / _PuffScale + float2(5.1, -_GR_Clock * 0.012));
                float n2 = GR_Noise(xz / (_PuffScale * 0.45) + float2(-2.7, _GR_Clock * 0.02));
                puff = sqrt(smoothstep(0.3, 0.85, n)) * 0.75 + sqrt(smoothstep(0.4, 0.9, n2)) * 0.35;

                float swell = level * level * (3.0 - 2.0 * level);
                return swell * _Amplitude * (0.55 + 0.45 * smoothstep(0.0, 350.0, r)) + billow + puff * _PuffHeight;
            }

            Varyings vert(Attributes input)
            {
                Varyings output;
                float2 xz = input.positionOS.xz;

                float level, puff, unusedLevel, unusedPuff;
                float h = Height(xz, level, puff);
                float e = max(3.0, length(xz) * 0.015);
                float hx = Height(xz + float2(e, 0.0), unusedLevel, unusedPuff);
                float hz = Height(xz + float2(0.0, e), unusedLevel, unusedPuff);
                float3 normalOS = normalize(float3(-(hx - h) / e, 1.0, -(hz - h) / e));

                output.positionWS = TransformObjectToWorld(float3(xz.x, h, xz.y));
                output.positionCS = TransformWorldToHClip(output.positionWS);
                output.normalWS = TransformObjectToWorldNormal(normalOS);
                output.local = xz;
                output.level = level;
                output.puff = puff;
                return output;
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 N = normalize(input.normalWS);
                float grain = GR_Noise(input.local / 9.0 - _GR_Clock * 0.02);
                float grain2 = GR_Noise(input.local / 9.0 + 7.3);
                N = normalize(N + float3(grain - 0.5, 0.0, grain2 - 0.5) * _Detail * 0.5);

                float ndl = dot(N, _GR_SunDir.xyz);
                float lit = smoothstep(-0.3, 0.45, ndl);
                float3 col = lerp(_GR_ShadowColor.rgb * _ShadowColor.rgb, _GR_SunColor.rgb * _LitColor.rgb, lit);

                // The hollows between puffs sit in the cloud's own shade.
                float hollow = 1.0 - saturate(input.puff * 1.6);
                col = lerp(col, col * _HollowColor.rgb, hollow * _Hollows);

                // A silver edge on slopes facing away from the eye, toward the sun.
                float3 V = normalize(_WorldSpaceCameraPos - input.positionWS);
                float rim = pow(1.0 - saturate(dot(N, V)), 3.0) * (0.3 + 0.7 * saturate(dot(-V, _GR_SunDir.xyz)));
                col += _GR_SunColor.rgb * rim * 0.3;

                col += _CrestColor.rgb * input.level * input.level * _CrestGlow * _GR_Intensity;
                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
