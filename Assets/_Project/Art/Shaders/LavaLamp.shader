// The visualizer's lava lamp: the room's screens as the glass of one lamp. Drawn on one quad per
// screen, a few millimetres in front of the water panels, mostly opaque.
//
// The C# side (LavaLamp) moves the wax: each blob's centre, unfolded into this panel's plane so the
// wax is unbroken across the seams, its radius and how hot it is. Here the blobs become metaballs —
// a smooth field that is the wax where it rises over a threshold, so two blobs that touch flow into
// one and a blob pulling away necks and parts — lit as rounded lumps by a fixed lamp, glowing from
// inside where they are hot, over a liquid lit from below by the heater on the floor, with glitter.
Shader "VRMusic/LavaLamp"
{
    Properties
    {
        _PanelSize ("Panel Size (m)", Vector) = (3.556, 2.2225, 0, 0)
        _PanelCentre ("Panel Centre (room)", Vector) = (0, 1.11, 1.11, 0)
        _PanelRight ("Panel Right (room)", Vector) = (1, 0, 0, 0)
        _PanelUp ("Panel Up (room)", Vector) = (0, 1, 0, 0)
        _LightPos ("Lamp (room)", Vector) = (0, 2.6, -0.7, 0)
        _EyePos ("Eye (room)", Vector) = (0, 1.6, -0.3, 0)
        _Room ("Room (half width, front, height)", Vector) = (1.778, 1.111, 2.2225, 0)
        _Ambient ("Ambient (time, wobble, glitter, heater)", Vector) = (0, 0, 0, 0.55)
        _WaxHot ("Wax Hot", Color) = (1, 0.55, 0.1, 1)
        _WaxCool ("Wax Cool", Color) = (0.85, 0.12, 0.05, 1)
        _LiquidLow ("Liquid Low", Color) = (0.32, 0.04, 0.24, 1)
        _LiquidHigh ("Liquid High", Color) = (0.05, 0, 0.1, 1)
        _Opacity ("Opacity", Range(0, 1)) = 1
        _Intensity ("Intensity", Range(0, 2)) = 1
        _Threshold ("Wax Threshold", Range(0.1, 0.9)) = 0.42
        _Bulge ("Bulge", Range(0, 0.3)) = 0.07
        _Glow ("Inner Glow", Range(0, 2)) = 0.7
        _Gloss ("Gloss", Range(0, 2)) = 0.6
        _LiquidAlpha ("Liquid Alpha", Range(0, 1)) = 0.86
        _HeaterGlow ("Heater Glow", Range(0, 2)) = 0.7
        _Glitter ("Glitter", Range(0, 2)) = 0.8
    }

    SubShader
    {
        // Under the ripples, so the two can be layered with the rings running over the wax.
        Tags { "RenderType" = "Transparent" "Queue" = "Transparent-60" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Blend One OneMinusSrcAlpha
        ZWrite Off
        ZTest LEqual
        Cull Off

        Pass
        {
            Name "LavaLamp"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            #define MAX_BLOBS 24

            CBUFFER_START(UnityPerMaterial)
                float4 _PanelSize;
                float4 _PanelCentre;
                float4 _PanelRight;
                float4 _PanelUp;
                float4 _LightPos;
                float4 _EyePos;
                float4 _Room;
                float4 _Ambient;
                float4 _WaxHot;
                float4 _WaxCool;
                float4 _LiquidLow;
                float4 _LiquidHigh;
                float _Opacity;
                float _Intensity;
                float _Threshold;
                float _Bulge;
                float _Glow;
                float _Gloss;
                float _LiquidAlpha;
                float _HeaterGlow;
                float _Glitter;
            CBUFFER_END

            // Per blob, from LavaLamp through a MaterialPropertyBlock.
            float _BlobCount;
            float4 _BlobPos[MAX_BLOBS];     // xy: the centre on this panel, in its metres; zw: a second way round, or far away
            float4 _BlobInfo[MAX_BLOBS];    // x: radius, y: heat 0–1, z: seed

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionCS = TransformObjectToHClip(input.positionOS.xyz);
                output.uv = input.uv;
                return output;
            }

            float Hash21(float2 p)
            {
                p = frac(p * float2(123.34, 456.21));
                p += dot(p, p + 45.32);
                return frac(p.x * p.y);
            }

            half4 frag(Varyings input) : SV_Target
            {
                float2 pm = (input.uv - 0.5) * _PanelSize.xy;
                float3 right = _PanelRight.xyz;
                float3 up = _PanelUp.xyz;
                float3 normal = cross(up, right);
                float3 p = _PanelCentre.xyz + right * pm.x + up * pm.y;
                float t = _Ambient.x;

                // The liquid's slow churn wobbles the wax's outline, more with the mids. A scalar in room
                // space, raising and lowering the threshold, so it is the same on both sides of a seam;
                // bending each panel's own coordinates broke the wax at the corners.
                float churn = (0.06 + 0.22 * _Ambient.y) * (sin(p.x * 5.1 + p.y * 3.7 + t * 0.9) * sin(p.z * 4.3 - p.y * 5.9 - t * 1.1)
                            + 0.5 * sin((p.x - p.z) * 9.1 + p.y * 7.3 + t * 1.3));
                float threshold = _Threshold * (1.0 + churn);
                float2 q = pm;

                // The wax: each blob a soft bump (1 − d²/R²)³ that reaches twice its radius, so a
                // lone blob's edge, where the field crosses the threshold, is at its radius.
                float field = 0.0;
                float2 grad = float2(0.0, 0.0);
                float heatSum = 0.0;
                int count = (int)_BlobCount;

                [loop]
                for (int i = 0; i < MAX_BLOBS; i++)
                {
                    if (i >= count)
                        break;

                    float4 pos = _BlobPos[i];
                    float4 info = _BlobInfo[i];
                    float2 d1 = q - pos.xy;
                    float2 d2 = q - pos.zw;
                    float2 d = dot(d1, d1) <= dot(d2, d2) ? d1 : d2;

                    float reach = 2.0 * info.x;
                    float s = dot(d, d) / (reach * reach);
                    if (s >= 1.0)
                        continue;

                    float k = 1.0 - s;
                    field += k * k * k;
                    grad += -6.0 * k * k * d / (reach * reach);
                    heatSum += k * k * k * info.y;
                }

                float heat = heatSum / max(field, 1e-4);
                float edge = field - threshold;
                float aa = max(fwidth(field), 1e-4);
                float wax = smoothstep(-aa, aa, edge);

                // Metres above the heater: up a wall it is the height; across the floor it goes negative
                // toward the middle, so the floor is the hottest, brightest part of the lamp.
                float above = p.y - min(min(p.x + _Room.x, _Room.x - p.x), _Room.y - p.z);
                float rise = saturate(above / _Room.z);

                // The liquid: lit from below by the heater, dark at the top, glowing round the wax.
                float3 waxColour = lerp(_WaxCool.rgb, _WaxHot.rgb, heat);
                float3 liquid = lerp(_LiquidLow.rgb, _LiquidHigh.rgb, sqrt(rise));
                liquid += _LiquidLow.rgb * _HeaterGlow * _Ambient.w * exp(-max(above, 0.0) / 0.5);
                liquid += waxColour * saturate(field / threshold) * 0.22;

                // Glitter: a few flecks in each patch of liquid, twinkling, more of them with the highs.
                float cellSize = 0.035;
                float2 cell = floor(pm / cellSize);
                float2 inCell = frac(pm / cellSize) - 0.5;
                float h1 = Hash21(cell + _PanelCentre.xz * 17.0);
                float h2 = Hash21(cell * 1.7 + 3.1);
                float lit = step(1.0 - (0.006 + 0.035 * _Ambient.z), h1);
                float twinkle = 0.5 + 0.5 * sin(t * (2.0 + 5.0 * h2) + h1 * 40.0);
                float fleck = smoothstep(0.16, 0.0, length(inCell - (float2(h1, h2) - 0.5) * 0.5));
                liquid += float3(1.0, 0.95, 0.85) * lit * twinkle * fleck * _Glitter * (0.15 + 0.85 * _Ambient.z);

                // The wax as rounded lumps: the field's slope tilts the surface, the lamp lights it, a
                // gloss where it faces the lamp, and a glow from inside that is brighter deeper in and
                // when hot.
                float3 n = normalize(float3(-grad * _Bulge, 1.0));
                float3 toLamp = normalize(_LightPos.xyz - p);
                float3 toEye = normalize(_EyePos.xyz - p);
                float3 l = float3(dot(toLamp, right), dot(toLamp, up), dot(toLamp, normal));
                float3 e = float3(dot(toEye, right), dot(toEye, up), dot(toEye, normal));
                float shade = saturate(dot(n, l)) / max(l.z, 0.2);
                float gloss = pow(saturate(dot(n, normalize(l + e))), 30.0) * _Gloss;
                float core = saturate(edge / max(1.0 - threshold, 1e-3) * 2.0);

                float3 lump = waxColour * (0.3 + 0.5 * saturate(shade)) * (0.55 + 0.45 * core)
                            + waxColour * _Glow * heat * (0.3 + 0.7 * core)
                            + gloss * lerp(waxColour, float3(1.0, 1.0, 1.0), 0.6);

                float3 col = lerp(liquid, lump, wax);
                float alpha = lerp(_LiquidAlpha, 1.0, wax);
                col *= _Intensity;

                return half4(col * _Opacity, alpha * _Opacity);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
