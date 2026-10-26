// Still water in the outside worlds — the pool under the canyon's light pillar, the lake under the
// aurora. It reflects the world's sky (stars, moon and all) through ripples the bass stirs, and
// glints toward the sun or moon with the treble. A fjord's water also carries the world's beat rings,
// spreading across it as ripples that catch the light.
//
// It also marks itself in the stencil buffer, so the aurora's mirror image can be drawn on the
// water and nowhere else: the reflection hangs below the water plane, where depth testing would
// hide it, so it ignores depth and trusts the stencil instead.
Shader "VRMusic/SimWater"
{
    Properties
    {
        _DeepColor ("Deep", Color) = (0.02, 0.04, 0.07, 1)
        _Reflectivity ("Reflectivity", Range(0, 1)) = 0.8
        _Ripple ("Ripple", Range(0, 2)) = 0.4
        _RippleScale ("Ripple Size (m)", Float) = 6
        _Glint ("Glint", Range(0, 4)) = 1
        _Rings ("Beat Rings (light)", Range(0, 4)) = 0
        _RingRipple ("Beat Rings (ripple)", Range(0, 4)) = 0
        _FogScale ("Fog", Range(0, 2)) = 1
        _StencilRef ("Stencil Mark", Float) = 1
    }

    SubShader
    {
        Tags { "RenderType" = "Opaque" "Queue" = "Geometry+10" "RenderPipeline" = "UniversalPipeline" "IgnoreProjector" = "True" }

        Cull Off
        ZWrite On

        Stencil
        {
            Ref [_StencilRef]
            Comp Always
            Pass Replace
        }

        Pass
        {
            Name "SimWater"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 4.0
            #pragma vertex vert
            #pragma fragment frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "SimCommon.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _DeepColor;
                float _Reflectivity;
                float _Ripple;
                float _RippleScale;
                float _Glint;
                float _Rings;
                float _RingRipple;
                float _FogScale;
                float _StencilRef;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
            };

            Varyings vert(Attributes input)
            {
                Varyings output;
                output.positionWS = TransformObjectToWorld(input.positionOS.xyz);
                output.positionCS = TransformWorldToHClip(output.positionWS);
                return output;
            }

            float Ripples(float2 q)
            {
                float t = _GR_Clock;
                return GR_Noise(q + float2(t * 0.05, t * 0.03)) + 0.5 * GR_Noise(q * 2.3 - float2(t * 0.08, -t * 0.05));
            }

            half4 frag(Varyings input) : SV_Target
            {
                float3 p = input.positionWS - _GR_Origin.xyz;
                float3 toEye = _WorldSpaceCameraPos - input.positionWS;
                float dist = length(toEye);
                float3 V = toEye / max(dist, 0.001);

                float2 q = p.xz / max(_RippleScale, 0.01);
                float e = 0.05;
                float n0 = Ripples(q);
                float nx = Ripples(q + float2(e, 0.0));
                float nz = Ripples(q + float2(0.0, e));
                float amp = _Ripple * (0.3 + 1.4 * _GR_Music.x) / (1.0 + dist * 0.003);
                float3 N = normalize(float3(-(nx - n0) / e * amp, 1.0, -(nz - n0) / e * amp));

                // The beat rings as ripples: tilt the water along the ring's slope, out from its origin.
                float ring = 0.0;
                if (_Rings > 0.001 || _RingRipple > 0.001)
                {
                    ring = GR_Rings(input.positionWS);
                    float2 outward = normalize(input.positionWS.xz - _GR_RingOrigin.xz + 1e-3);
                    float slope = (GR_Rings(input.positionWS + float3(outward.x, 0.0, outward.y) * 3.0) - ring) / 3.0;
                    N = normalize(N + float3(outward.x, 0.0, outward.y) * slope * 30.0 * _RingRipple);
                }

                float3 R = reflect(-V, N);
                R.y = abs(R.y);
                float3 sky = GR_Sky(R, _Glint * (0.6 + 0.8 * _GR_Music.z));

                float fresnel = 0.04 + 0.96 * pow(1.0 - saturate(dot(N, V)), 5.0);
                float3 col = lerp(_DeepColor.rgb, sky, saturate(_Reflectivity * (0.3 + 0.7 * fresnel)));
                col += _GR_RingColor.rgb * ring * _Rings * _GR_Intensity;

                col = GR_ApplyFog(col, input.positionWS, _FogScale);
                return half4(col, 1.0);
            }
            ENDHLSL
        }
    }

    FallBack Off
}
