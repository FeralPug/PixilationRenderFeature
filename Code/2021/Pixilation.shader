Shader "MyShaders/URP/Pixilation/Pixilation"
{//https://www.cyanilux.com/tutorials/urp-shader-code/
    Properties
    {
        _MainTex("Base Texture", 2D) = "white" {}
        _ZoomAmount ("Zoom Amount", float) = 1.0
        _BlurSize("Blur Size", Range(0, 0.5)) = 0
        [KeywordEnum(Low, Medium, High)] _Samples ("Sample amount", float) = 0
        [PowerSlider(3)] _StandardDeviation("Standard Deviation", Range(0.0, 0.3)) = 0.02
    }
        SubShader
    {
        Tags {
            "RenderPipeline" = "UniversalPipeline"
        }

        HLSLINCLUDE

        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

        #pragma multi_compile _SAMPLES_LOW _SAMPLES_MEDIUM _SAMPLES_HIGH

        #pragma exclude_renderers gles gles3 glcore
        #pragma target 4.5

        //#define PI 3.14159265349
        #define E 2.71828182846

        #if _SAMPLES_LOW
            #define SAMPLES 10
        #elif _SAMPLES_MEDIUM
            #define SAMPLES 30
        #else
            #define SAMPLES 100
        #endif

        CBUFFER_START(UnityPerMaterial)
            real4 _MainTex_TexelSize;
            real _BlurSize;
            real _StandardDeviation;
            real _ZoomAmount;
        CBUFFER_END

        ENDHLSL

        Pass
        {
            Name "Mesh_Draw"

            Cull Off
            ZTest LEqual
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                real4 positionOS : POSITION;
            };

            struct Varyings
            {
                real4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;

                return Out;
            }

            real frag(Varyings IN) : SV_Target
            {
                return 1.0;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Vertical_Blur" //https://www.ronja-tutorials.com/post/023-postprocessing-blur/

            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                real4 positionOS : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                real4 positionCS : SV_POSITION;
                real2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;
                Out.uv = IN.uv;

                return Out;
            }

            real frag(Varyings IN) : SV_Target
            {
                real output = 0.0;
                real sum = 0.0;

                UNITY_UNROLL
                for (float i = 0; i < SAMPLES; i++) {
                    float offset = (i / (SAMPLES - 1) - 0.5) * _BlurSize;
                    float2 uv = IN.uv + float2(0, offset);

                    float stDevSquared = _StandardDeviation * _StandardDeviation;
                    float gauss = (1 / sqrt(2 * PI * stDevSquared)) * pow(E, -((offset * offset) / (2 * stDevSquared)));

                    sum += gauss;

                    output += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r * gauss;
                }

                return output / sum;
            }

            ENDHLSL
        }


        Pass
        {
            Name "Horizontal_Blur" //https://www.ronja-tutorials.com/post/023-postprocessing-blur/

            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                real4 positionOS : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                real4 positionCS : SV_POSITION;
                real2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;
                Out.uv = IN.uv;

                return Out;
            }

            real frag(Varyings IN) : SV_Target
            {
                real invAspect = _ScreenParams.y / _ScreenParams.x;

                real output = 0.0;
                real sum = 0.0;

                UNITY_UNROLL
                for (float i = 0; i < SAMPLES; i++) {
                    float offset = (i / (SAMPLES - 1) - 0.5) * _BlurSize * invAspect;
                    float2 uv = IN.uv + float2(offset, 0);

                    float stDevSquared = _StandardDeviation * _StandardDeviation;
                    float gauss = (1 / sqrt(2 * PI * stDevSquared)) * pow(E, -((offset * offset) / (2 * stDevSquared)));

                    sum += gauss;

                    output += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, uv).r * gauss;
                }

                return output / sum;
            }

            ENDHLSL
        }

        Pass
        {
            Name "Down_Sample"

            Cull Off
            ZTest Always
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                real4 positionOS : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                real4 positionCS : SV_POSITION;
                real2 uv : TEXCOORD0;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            Varyings vert(Attributes IN)
            {
                Varyings Out;

                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;
                Out.uv = IN.uv;

                return Out;
            }

            real frag(Varyings IN) : SV_Target
            {
                return SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv).r;
            }

            ENDHLSL
        }

        Pass
            {
                Name "Resolve_DownSample"

                Cull Off
                ZTest Always
                ZWrite Off

                HLSLPROGRAM

                #pragma vertex vert
                #pragma fragment frag

                struct Attributes
                {
                    real4 positionOS : POSITION;
                    real2 uv : TEXCOORD0;
                };

                struct Varyings
                {
                    real4 positionCS : SV_POSITION;
                    real2 uv : TEXCOORD0;
                };

                TEXTURE2D(_MainTex);
                SAMPLER(sampler_MainTex);

                Varyings vert(Attributes IN)
                {
                    Varyings Out;

                    VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                    Out.positionCS = positionInputs.positionCS;
                    Out.uv = IN.uv;

                    return Out;
                }

                real frag(Varyings IN) : SV_Target
                {
                    real invAspect = _ScreenParams.y / _ScreenParams.x;

                    real output = 0.0;

                    UNITY_UNROLL
                    for (float x = -1; x <= 1; x++) {
                        UNITY_UNROLL
                        for (float y = -1; y <= 1; y++) {
                            float2 offsetUV = IN.uv + float2(x * _MainTex_TexelSize.x, y * _MainTex_TexelSize.y);

                            output += SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, offsetUV).r;
                        }
                    }

                    return saturate(output);
                }

                ENDHLSL
        }

        Pass
        {
            Name "Pixilation"

            ZTest Always
            ZWrite Off

            HLSLPROGRAM

            #pragma vertex vert
            #pragma fragment frag

            struct Attributes
            {
                real4 positionOS : POSITION;
                real2 uv : TEXCOORD0;
            };

            struct Varyings
            {
                real2 uv : TEXCOORD0;
                real4 positionCS : SV_POSITION;
            };

            TEXTURE2D(_MainTex);
            SAMPLER(sampler_MainTex);

            TEXTURE2D(_PixilationDownSampleBuffer);
            SAMPLER(sampler_PixilationDownSampleBuffer);

            float4 _PixilationDownSampleResolution;

            Varyings vert(Attributes IN)
            {
                Varyings Out;
                //Out.positionCS = TransformObjectToHClip(IN.positionOS.xyz);
                //VertexNormalInputs normalInputs = GetVertexNormalInputs(IN.normalOS, IN.tangentOS);
                VertexPositionInputs positionInputs = GetVertexPositionInputs(IN.positionOS.xyz);
                Out.positionCS = positionInputs.positionCS;
                Out.uv = IN.uv;
                return Out;
            }

            real4 frag(Varyings IN) : SV_Target
            {
                //Down sample
                real pixel = step(0.01, SAMPLE_TEXTURE2D(_PixilationDownSampleBuffer, sampler_PixilationDownSampleBuffer, IN.uv).r);

                //sample the camera target
                real4 camTarget = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, IN.uv);

                real2 downSampleInvRes = _PixilationDownSampleResolution.xy;

                float2 zoomRatio = _MainTex_TexelSize.zw / _PixilationDownSampleResolution.zw;

                float2 ratio = IN.uv / downSampleInvRes;

                int2 gridPos = floor(ratio);
                int2 nextPos = gridPos + 1;
                float2 center = lerp(gridPos, nextPos, .5);

                float2 mod = ratio - gridPos;

                float2 scaledPos = (mod - 0.5) / zoomRatio;
                scaledPos += center;
                scaledPos *= downSampleInvRes;

                float4 zoomTarget = SAMPLE_TEXTURE2D(_MainTex, sampler_MainTex, scaledPos);

                //lerp to pixilation
                camTarget = lerp(camTarget, zoomTarget, pixel);// step(0.001, pixel));

                return camTarget;
            }
            ENDHLSL
        }
    }
}
