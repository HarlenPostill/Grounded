Shader "Custom/GroundFog"
{
    Properties
    {
        _FogColor ("Fog Color", Color) = (0.87, 0.82, 0.69, 1)
        _FogDensity ("Fog Density", Range(0, 1)) = 0.35
        _NoiseScale1 ("Noise Scale 1", Float) = 6.0
        _NoiseScale2 ("Noise Scale 2", Float) = 14.0
        _ScrollSpeedX ("Scroll Speed X", Float) = 0.02
        _ScrollSpeedZ ("Scroll Speed Z", Float) = 0.008
        _SoftParticleDist ("Soft Particle Distance", Float) = 1.5
    }

    SubShader
    {
        Tags
        {
            "RenderType" = "Transparent"
            "Queue" = "Transparent"
            "RenderPipeline" = "UniversalPipeline"
        }

        Blend SrcAlpha OneMinusSrcAlpha
        ZWrite Off
        Cull Off

        Pass
        {
            HLSLPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_fog

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                float4 _FogColor;
                float _FogDensity;
                float _NoiseScale1;
                float _NoiseScale2;
                float _ScrollSpeedX;
                float _ScrollSpeedZ;
                float _SoftParticleDist;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                float2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                float4 positionHCS : SV_POSITION;
                float2 uv : TEXCOORD0;
                float4 screenPos : TEXCOORD1;
            };

            // Simple hash-based noise
            float2 hash2(float2 p)
            {
                p = float2(dot(p, float2(127.1, 311.7)),
                    dot(p, float2(269.5, 183.3)));
                return frac(sin(p) * 43758.5453);
            }

            float smoothNoise(float2 uv, float scale)
            {
                uv *= scale;
                float2 i = floor(uv);
                float2 f = frac(uv);
                float2 u = f * f * (3.0 - 2.0 * f); // smoothstep

                float a = frac(sin(dot(i,               float2(127.1, 311.7))) * 43758.5453);
                float b = frac(sin(dot(i + float2(1,0), float2(127.1, 311.7))) * 43758.5453);
                float c = frac(sin(dot(i + float2(0,1), float2(127.1, 311.7))) * 43758.5453);
                float d = frac(sin(dot(i + float2(1,1), float2(127.1, 311.7))) * 43758.5453);

                return lerp(lerp(a, b, u.x), lerp(c, d, u.x), u.y);
            }

            Varyings vert(Attributes IN)
            {
                Varyings OUT;
                OUT.positionHCS = TransformObjectToHClip(IN.positionOS.xyz);
                OUT.uv = IN.uv;
                OUT.screenPos = ComputeScreenPos(OUT.positionHCS);
                return OUT;
            }

            half4 frag(Varyings IN) : SV_Target
            {
                // Scroll UVs over time
                float2 scroll1 = float2(_Time.y * _ScrollSpeedX, _Time.y * _ScrollSpeedZ);
                float2 scroll2 = float2(_Time.y * _ScrollSpeedZ, _Time.y * _ScrollSpeedX * 0.5);

                float2 uv1 = IN.uv + scroll1;
                float2 uv2 = IN.uv + scroll2;

                // Two noise layers combined
                float noise = smoothNoise(uv1, _NoiseScale1) * 0.6
                            + smoothNoise(uv2, _NoiseScale2) * 0.4;

                noise = saturate(noise);

                // Soft particles — fade where fog intersects geometry
                float2 screenUV = IN.screenPos.xy / IN.screenPos.w;
                float sceneDepth = LinearEyeDepth(
                    SampleSceneDepth(screenUV),
                    _ZBufferParams
                );
                float fragDepth = IN.screenPos.w;
                float depthDiff = sceneDepth - fragDepth;
                float softFade = saturate(depthDiff / _SoftParticleDist);

                float alpha = noise * _FogDensity * softFade;

                return half4(_FogColor.rgb, alpha);
            }
            ENDHLSL
        }
    }
}