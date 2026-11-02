// Starling — URP particle shader that draws a small bird in flight from the billboard's UVs: a body
// along +V and two wings whose tips rise and fall with a flap phase, so the flock beats its wings
// without a texture or a mesh per bird.
//
// The particle colour carries per-bird state, set by StarlingFlock every frame: G is the flap phase
// (0–1, one wingbeat), R is a flash toward _FlashColor (a note rippling through the flock), A is
// opacity. The bird's own colour is the material's — a murmuration reads as dark shapes against the
// bright dusk horizon.
//
// Alpha-blended, not additive, because the birds are darker than the sky behind them. Unsorted: a
// starling is a few pixels across and overlaps are brief, so the cost of sorting four cameras' worth
// of flock buys nothing visible.
Shader "VRMusic/Starling"
{
    Properties
    {
        _BaseColor ("Bird", Color) = (0.07, 0.05, 0.12, 1)
        _FlashColor ("Flash", Color) = (1.0, 0.85, 0.6, 1)
        _WingSpan ("Wing span", Range(0.3, 1)) = 0.9
        _Thickness ("Wing thickness", Range(0.02, 0.3)) = 0.11
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
            "IgnoreProjector" = "True"
            "PreviewType" = "Plane"
        }
        LOD 100

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            Name "Starling"
            Tags { "LightMode" = "UniversalForward" }

            HLSLPROGRAM
            #pragma target 3.0
            #pragma vertex Vert
            #pragma fragment Frag

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _BaseColor;
                half4 _FlashColor;
                half _WingSpan;
                half _Thickness;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float2 uv         : TEXCOORD0;
                half4  color      : COLOR;
            };

            Varyings Vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.color = IN.color;
                return OUT;
            }

            // Distance from p to the segment a–b.
            float Segment(float2 p, float2 a, float2 b)
            {
                float2 pa = p - a;
                float2 ba = b - a;
                float h = saturate(dot(pa, ba) / dot(ba, ba));
                return length(pa - ba * h);
            }

            half4 Frag(Varyings IN) : SV_Target
            {
                // Quad centred on the origin, ±1 at its edges; the bird flies toward +y.
                float2 p = IN.uv * 2.0 - 1.0;

                // Wing tips sweep from above the body to below it over one beat. Each wing bends at a
                // wrist, so the shape reads as a bird rather than a cross.
                float flap = sin(IN.color.g * 6.2831853);
                float2 shoulder = float2(0.0, 0.12);
                float2 wristL = float2(-_WingSpan * 0.5, 0.12 + flap * 0.30);
                float2 wristR = float2(_WingSpan * 0.5, 0.12 + flap * 0.30);
                float2 tipL = float2(-_WingSpan, 0.0 + flap * 0.55);
                float2 tipR = float2(_WingSpan, 0.0 + flap * 0.55);

                float wing = min(min(Segment(p, shoulder, wristL), Segment(p, wristL, tipL)),
                                 min(Segment(p, shoulder, wristR), Segment(p, wristR, tipR)));

                // Wings taper toward the tips.
                float taper = lerp(1.0, 0.45, saturate(abs(p.x) / _WingSpan));
                half wings = 1.0h - smoothstep(_Thickness * taper * 0.6, _Thickness * taper, wing);

                // Body: a short ellipse along the flight line, with a pointed tail.
                float2 b = p / float2(0.13, 0.42);
                half body = 1.0h - smoothstep(0.75, 1.0, length(b));

                half shape = saturate(wings + body);
                half3 rgb = lerp(_BaseColor.rgb, _FlashColor.rgb, saturate(IN.color.r));
                return half4(rgb, shape * IN.color.a * _BaseColor.a);
            }
            ENDHLSL
        }
    }

    Fallback "Universal Render Pipeline/Particles/Unlit"
}
