Shader "FlappyBoids/Underwater Volumetric God Rays"
{
    Properties
    {
        [HDR] _RayColor("Ray Color", Color) = (0.16, 0.62, 1.35, 1)
        _Intensity("Intensity", Range(0, 4)) = 2.8
        _Density("Density", Range(0, 0.3)) = 0.15
        _Opacity("Opacity", Range(0, 1)) = 0.80
        _BeamThreshold("Beam Threshold", Range(0.2, 0.9)) = 0.70
        _Sharpness("Beam Sharpness", Range(0.5, 6)) = 1.4
        _DepthFalloff("Depth Falloff", Range(0, 5)) = 0.75
        _Speed("Current Speed", Range(0, 1)) = 0.18
        _SlantX("Ray Slant X", Range(-1, 1)) = 0.32
        _SlantZ("Ray Slant Z", Range(-1, 1)) = 0.08
    }

    SubShader
    {
        Tags
        {
            "RenderPipeline" = "UniversalPipeline"
            "RenderType" = "Transparent"
            "Queue" = "Transparent-20"
        }

        Pass
        {
            Name "UnderwaterGodRays"
            Tags { "LightMode" = "UniversalForward" }

            Blend One OneMinusSrcAlpha
            Cull Front
            ZWrite Off
            ZTest Always

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex Vert
            #pragma fragment Frag
            #pragma multi_compile_instancing

            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/DeclareDepthTexture.hlsl"

            CBUFFER_START(UnityPerMaterial)
                half4 _RayColor;
                half _Intensity;
                half _Density;
                half _Opacity;
                half _BeamThreshold;
                half _Sharpness;
                half _DepthFalloff;
                half _Speed;
                half _SlantX;
                half _SlantZ;
            CBUFFER_END

            struct Attributes
            {
                float4 positionOS : POSITION;
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct Varyings
            {
                float4 positionCS : SV_POSITION;
                float3 positionWS : TEXCOORD0;
                UNITY_VERTEX_INPUT_INSTANCE_ID
                UNITY_VERTEX_OUTPUT_STEREO
            };

            Varyings Vert(Attributes input)
            {
                Varyings output;
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);
                UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(output);

                VertexPositionInputs positionInputs = GetVertexPositionInputs(input.positionOS.xyz);
                output.positionCS = positionInputs.positionCS;
                output.positionWS = positionInputs.positionWS;
                return output;
            }

            float Hash12(float2 value)
            {
                float3 p = frac(value.xyx * 0.1031);
                p += dot(p, p.yzx + 33.33);
                return frac((p.x + p.y) * p.z);
            }

            float BeamDensity(float3 positionOS, float time)
            {
                float depth01 = saturate(0.5 - positionOS.y);
                float2 source = positionOS.xz + float2(_SlantX, _SlantZ) * depth01;
                source += float2(sin(time * 0.21), cos(time * 0.17)) * 0.015;

                float broad = 1.0 - abs(sin(
                    source.x * 20.0 + sin(source.y * 1.2 + time * 0.11) * 0.65 + time * 0.22));
                float secondary = 1.0 - abs(sin(
                    source.x * 9.3 - source.y * 0.8 - time * 0.13));
                float beam = smoothstep(_BeamThreshold, 1.0, max(broad, secondary * 0.72));
                beam = pow(saturate(beam), _Sharpness);

                float2 sideFade = 1.0 - smoothstep(0.38, 0.5, abs(positionOS.xz));
                float volumeFade = sideFade.x * sideFade.y;
                float bottomFade = smoothstep(0.01, 0.14, positionOS.y + 0.5);
                float waterAttenuation = exp2(-_DepthFalloff * depth01);
                float suspendedMatter = 0.88 + 0.12 * sin(
                    positionOS.y * 27.0 + source.x * 17.0 - source.y * 9.0 + time * 0.37);
                return beam * volumeFade * bottomFade * waterAttenuation * suspendedMatter;
            }

            half4 Frag(Varyings input) : SV_Target
            {
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(input);

                float3 cameraWS = GetCameraPositionWS();
                float3 rayDirectionWS = normalize(input.positionWS - cameraWS);
                float3 rayOriginOS = TransformWorldToObject(cameraWS);
                float3 rayDirectionOS = mul((float3x3)unity_WorldToObject, rayDirectionWS);

                float3 safeDirection = rayDirectionOS +
                    (1.0 - step(0.00001, abs(rayDirectionOS))) * 0.00001;
                float3 inverseDirection = rcp(safeDirection);
                float3 firstHit = (-0.5 - rayOriginOS) * inverseDirection;
                float3 secondHit = (0.5 - rayOriginOS) * inverseDirection;
                float3 nearHit = min(firstHit, secondHit);
                float3 farHit = max(firstHit, secondHit);
                float entryDistance = max(max(nearHit.x, nearHit.y), nearHit.z);
                float exitDistance = min(min(farHit.x, farHit.y), farHit.z);
                entryDistance = max(entryDistance, 0.0);

                float2 screenUV = input.positionCS.xy / _ScaledScreenParams.xy;
                float rawDepth = SampleSceneDepth(screenUV);
                #if UNITY_REVERSED_Z
                    bool hasSceneDepth = rawDepth > 0.0001;
                #else
                    bool hasSceneDepth = rawDepth < 0.9999;
                #endif
                if (hasSceneDepth)
                {
                    float3 scenePositionWS = ComputeWorldSpacePosition(screenUV, rawDepth, UNITY_MATRIX_I_VP);
                    float sceneDistance = dot(scenePositionWS - cameraWS, rayDirectionWS);
                    exitDistance = min(exitDistance, sceneDistance - 0.06);
                }

                if (exitDistance <= entryDistance)
                    return half4(0, 0, 0, 0);

                #if defined(SHADER_API_GLES3)
                    const int StepCount = 8;
                #else
                    const int StepCount = 12;
                #endif
                float segmentLength = exitDistance - entryDistance;
                float stepLength = segmentLength / StepCount;
                float jitter = Hash12(input.positionCS.xy);
                float integratedDensity = 0.0;
                float time = _Time.y * _Speed;

                [unroll]
                for (int index = 0; index < StepCount; index++)
                {
                    float distanceAlongRay = entryDistance + (index + 0.45 + jitter * 0.1) * stepLength;
                    float3 samplePositionOS = rayOriginOS + rayDirectionOS * distanceAlongRay;
                    integratedDensity += BeamDensity(samplePositionOS, time) * stepLength;
                }

                float alpha = (1.0 - exp(-integratedDensity * _Density)) * _Opacity;
                half3 premultipliedColor = _RayColor.rgb * (_Intensity * alpha);
                return half4(premultipliedColor, alpha);
            }
            ENDHLSL
        }
    }

    Fallback Off
}
