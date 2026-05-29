Shader "Custom/WindTrailRibbon"
{
    Properties
    {
        [Header(Trail Colour)]
        _Color          ("Trail colour",            Color)  = (0.4, 0.85, 1.0, 1.0)
        _EdgeColor      ("Edge / fresnel colour",   Color)  = (1.0, 1.0, 1.0, 1.0)
        _EdgePower      ("Edge power",              Range(0.5, 8))   = 2.5
        _EdgeStrength   ("Edge strength",           Range(0, 1))     = 0.6

        [Header(Toon Banding)]
        _BandCount      ("Opacity band count",      Range(1, 8))     = 3
        _BandSharpness  ("Band sharpness",          Range(1, 32))    = 12
        _AlphaBoost     ("Alpha boost",             Range(0, 1))     = 0.15

        [Header(Texture)]
        _MainTex        ("Trail texture (opt)",     2D)     = "white" {}
        _ScrollSpeed    ("UV scroll speed",         Float)  = 0.4

        [Header(Outline)]
        _OutlineColor   ("Outline colour",          Color)  = (0.05, 0.05, 0.1, 1.0)
        _OutlineWidth   ("Outline width",           Range(0, 0.05))  = 0.008

        [Header(Soft Dissolve)]
        _TailFade       ("Tail fade sharpness",     Range(0.5, 8))   = 3.0
    }

    SubShader
    {
        Tags
        {
            "RenderType"      = "Transparent"
            "Queue"           = "Transparent"
            "RenderPipeline"  = "UniversalPipeline"
            "IgnoreProjector" = "True"
        }

        // -----------------------------------------------------------------
        // Pass 1 — Ink outline (back-face expanded hull)
        // -----------------------------------------------------------------
        Pass
        {
            Name "Outline"
            Tags { "LightMode" = "SRPDefaultUnlit" }

            Cull   Front
            ZWrite Off
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   OutlineVert
            #pragma fragment OutlineFrag
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _OutlineColor;
                float  _OutlineWidth;
                float4 _Color;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float  alpha       : TEXCOORD0;
            };

            Varyings OutlineVert(Attributes IN)
            {
                Varyings OUT;
                // Expand along normal in object space
                float3 pos = IN.positionOS.xyz + IN.normalOS * _OutlineWidth;
                OUT.positionHCS = TransformObjectToHClip(pos);
                OUT.alpha = IN.color.a;
                return OUT;
            }

            half4 OutlineFrag(Varyings IN) : SV_Target
            {
                return half4(_OutlineColor.rgb, _OutlineColor.a * IN.alpha);
            }
            ENDHLSL
        }

        // -----------------------------------------------------------------
        // Pass 2 — Main trail (front-face, transparent, toon shading)
        // -----------------------------------------------------------------
        Pass
        {
            Name "TrailForward"
            Tags { "LightMode" = "UniversalForward" }

            Cull   Off
            ZWrite Off
            Blend  SrcAlpha OneMinusSrcAlpha

            HLSLPROGRAM
            #pragma vertex   TrailVert
            #pragma fragment TrailFrag
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _Color;
                float4 _EdgeColor;
                float  _EdgePower;
                float  _EdgeStrength;
                float  _BandCount;
                float  _BandSharpness;
                float  _AlphaBoost;
                float  _ScrollSpeed;
                float4 _MainTex_ST;
                float  _TailFade;
                float4 _OutlineColor;
                float  _OutlineWidth;
            CBUFFER_END

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            struct Attributes
            {
                float4 positionOS : POSITION;
                float3 normalOS   : NORMAL;
                float2 uv         : TEXCOORD0;
                float4 color      : COLOR;     // alpha carries per-vertex tail fade
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv          : TEXCOORD0;
                float3 normalWS    : TEXCOORD1;
                float3 viewDirWS   : TEXCOORD2;
                float  vertAlpha   : TEXCOORD3;
                float  fogFactor   : TEXCOORD4;
            };

            Varyings TrailVert(Attributes IN)
            {
                Varyings OUT;
                VertexPositionInputs posInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                OUT.positionHCS = posInputs.positionCS;
                OUT.uv          = TRANSFORM_TEX(IN.uv, _MainTex);
                OUT.normalWS    = TransformObjectToWorldNormal(IN.normalOS);
                OUT.viewDirWS   = GetWorldSpaceViewDir(posInputs.positionWS);
                OUT.vertAlpha   = IN.color.a;
                OUT.fogFactor   = ComputeFogFactor(posInputs.positionCS.z);
                return OUT;
            }

            half4 TrailFrag(Varyings IN) : SV_Target
            {
                // --- Scrolling texture ---
                float2 scrollUV = IN.uv + float2(_ScrollSpeed * _Time.y, 0);
                half4 texSample = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scrollUV);

                // --- Base colour ---
                half3 col = _Color.rgb * texSample.rgb;

                // --- Fresnel edge glow ---
                float3 N = normalize(IN.normalWS);
                float3 V = normalize(IN.viewDirWS);
                float fresnel = 1.0 - saturate(dot(N, V));
                fresnel = pow(fresnel, _EdgePower);
                col = lerp(col, _EdgeColor.rgb, fresnel * _EdgeStrength);

                // --- Toon banding on opacity ---
                // UV.x goes 0→1 from head to tail. We band the alpha to create
                // discrete rings / pulses along the ribbon.
                float rawAlpha = IN.uv.x;

                // Tail soft dissolve
                float tailMask = pow(saturate(1.0 - IN.uv.x), _TailFade);

                // Quantise into bands
                float banded = floor(rawAlpha * _BandCount) / _BandCount;
                // Smooth step between bands (sharpness controls hardness)
                float bandBlend = smoothstep(
                    banded,
                    banded + 1.0 / _BandCount,
                    rawAlpha + (1.0 / (_BandCount * _BandSharpness)));
                float bandedAlpha = lerp(banded, banded + 1.0 / _BandCount, bandBlend);
                bandedAlpha = saturate(bandedAlpha + _AlphaBoost);

                // Combine: vertex alpha (tail fade from CPU), band pattern, tail dissolve
                float alpha = bandedAlpha * tailMask * IN.vertAlpha * _Color.a * texSample.a;
                alpha = saturate(alpha);

                // --- Fog ---
                half3 finalCol = MixFog(col, IN.fogFactor);

                return half4(finalCol, alpha);
            }
            ENDHLSL
        }
    }

    FallBack "Hidden/Universal Render Pipeline/FallbackError"
}
