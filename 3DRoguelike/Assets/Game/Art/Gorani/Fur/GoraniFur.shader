// 고라니 털(셸 방식). GoraniFurSetup이 구운 털 메시(털 범위 삼각형을 겹 수만큼 복사,
// TEXCOORD1 = (겹 높이 0..1, 길이 비율, 덮임))를 고라니와 같은 뼈로 스킨드 렌더러 하나가 그린다.
// 그래서 털은 스키닝을 그대로 따라가고, 털 전체가 그리기 1번이다.
// 털 색은 고라니 알베도를 따른다. 조절 값은 Assets/Game/Data/GoraniFur.asset(FurSettings)이 넣는다.
Shader "Game/Fur Shell"
{
    Properties
    {
        [MainTexture] _BaseMap ("Albedo", 2D) = "white" {}
        [MainColor] _BaseColor ("Tint", Color) = (1, 1, 1, 1)
        _ShellCount ("Shell Count (baked, info)", Float) = 12
        _FurLength ("Length (m)", Range(0.001, 0.05)) = 0.007
        _FurDensity ("Density (strands per UV)", Range(100, 4000)) = 1400
        _FurThickness ("Strand Thickness", Range(0.2, 2)) = 0.95
        _FurThinning ("Tip Thinning", Range(0, 1)) = 0.75
        _FurJitter ("Length Jitter", Range(0, 1)) = 0.5
        _DarkCutoff ("Dark Albedo Cutoff", Range(0, 0.3)) = 0.11
        _Gravity ("Gravity Droop", Range(0, 1)) = 0.25
        _WindStrength ("Wind Strength", Range(0, 1)) = 0.15
        _WindSpeed ("Wind Speed", Range(0, 10)) = 2
        _Occlusion ("Root Occlusion", Range(0, 1)) = 0.45
        _Rim ("Rim", Range(0, 1)) = 0.2
        [HDR] _EmissionColor ("Emission (HitFlash)", Color) = (0, 0, 0, 1)
    }

    SubShader
    {
        Tags { "RenderPipeline" = "UniversalPipeline" "RenderType" = "TransparentCutout" "Queue" = "AlphaTest" "IgnoreProjector" = "True" }

        HLSLINCLUDE
        #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"

        TEXTURE2D(_BaseMap);
        SAMPLER(sampler_BaseMap);

        CBUFFER_START(UnityPerMaterial)
            float4 _BaseMap_ST;
            half4 _BaseColor;
            float _ShellCount;
            float _FurLength;
            float _FurDensity;
            float _FurThickness;
            float _FurThinning;
            float _FurJitter;
            float _DarkCutoff;
            float _Gravity;
            float _WindStrength;
            float _WindSpeed;
            float _Occlusion;
            float _Rim;
            half4 _EmissionColor;
        CBUFFER_END

        struct FurAttributes
        {
            float4 positionOS : POSITION;
            float3 normalOS : NORMAL;
            float2 uv : TEXCOORD0;
            float3 shell : TEXCOORD1; // x = 겹 높이(0..1), y = 길이 비율, z = 덮임(털 범위 마스크)
        };

        struct FurVaryings
        {
            float4 positionCS : SV_POSITION;
            float2 uv : TEXCOORD0;
            float3 shell : TEXCOORD1;
            float3 positionWS : TEXCOORD2;
            float3 normalWS : TEXCOORD3;
            float fogFactor : TEXCOORD4;
        };

        // 월드 공간에서 노멀 방향으로 밀고, 끝으로 갈수록(h²) 중력 · 바람으로 휜다
        float3 FurDisplace(float3 positionWS, float3 normalWS, float h, float lengthScale)
        {
            float len = _FurLength * lengthScale;
            float bend = h * h * len;
            float t = _Time.y * _WindSpeed;
            float3 wind = float3(sin(t + positionWS.x * 7.0 + positionWS.y * 3.0), 0.0, cos(t * 0.83 + positionWS.z * 6.0 + positionWS.y * 2.0));
            return positionWS + normalWS * (len * h) + float3(0.0, -_Gravity * bend, 0.0) + wind * (_WindStrength * bend);
        }

        FurVaryings FurVert(FurAttributes v)
        {
            FurVaryings o;
            float3 positionWS = TransformObjectToWorld(v.positionOS.xyz);
            float3 normalWS = TransformObjectToWorldNormal(v.normalOS);
            positionWS = FurDisplace(positionWS, normalWS, v.shell.x, v.shell.y);
            o.positionCS = TransformWorldToHClip(positionWS);
            o.positionWS = positionWS;
            o.normalWS = normalWS;
            o.uv = TRANSFORM_TEX(v.uv, _BaseMap);
            o.shell = v.shell;
            o.fogFactor = ComputeFogFactor(o.positionCS.z);
            return o;
        }

        float2 FurHash22(float2 p)
        {
            float3 p3 = frac(float3(p.xyx) * float3(0.1031, 0.1030, 0.0973));
            p3 += dot(p3, p3.yzx + 33.33);
            return frac((p3.xx + p3.yz) * p3.zy);
        }

        // 가닥 판정: 셸 픽셀이 털 가닥 단면 안이면 남기고, 밖이면 버린다
        void FurClip(float2 uv, float h, float coverage, half3 albedo)
        {
            float2 c = uv * _FurDensity;
            float2 id = floor(c);
            float2 r = FurHash22(id);
            float2 f = frac(c) - 0.5 - (r - 0.5) * 0.3;

            float strandLength = 1.0 - _FurJitter * r.x;        // 이 가닥의 길이(전체 길이에 대한 비율)
            float edgeKeep = saturate(coverage * 2.0);          // 털 범위 끝에서는 가닥 수를 줄인다
            half luminance = dot(albedo, half3(0.299, 0.587, 0.114));
            float t = h / strandLength;
            float radius = 0.5 * _FurThickness * (1.0 - _FurThinning * t);

            // 멀리서는 가닥 한 칸이 픽셀보다 작아 반짝인다: 그때는 단면을 넓혀 덮게 한다
            float footprint = max(fwidth(c.x), fwidth(c.y));
            radius = lerp(radius, 0.75, saturate(footprint - 0.5));

            float keep = min(min(strandLength - h, radius - length(f)), edgeKeep - r.y);
            keep = min(keep, luminance - _DarkCutoff * (0.7 + 0.6 * r.y)); // 눈 · 코 · 입처럼 어두운 곳은 맨살
            clip(keep);
        }

        half3 FurAlbedo(float2 uv)
        {
            return SAMPLE_TEXTURE2D(_BaseMap, sampler_BaseMap, uv).rgb * _BaseColor.rgb;
        }
        ENDHLSL

        Pass
        {
            Name "FurForward"
            Tags { "LightMode" = "UniversalForward" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurVert
            #pragma fragment FurFrag
            #pragma multi_compile _ _MAIN_LIGHT_SHADOWS _MAIN_LIGHT_SHADOWS_CASCADE
            #pragma multi_compile_fragment _ _SHADOWS_SOFT _SHADOWS_SOFT_LOW _SHADOWS_SOFT_MEDIUM _SHADOWS_SOFT_HIGH
            #pragma multi_compile_fog
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            half4 FurFrag(FurVaryings i) : SV_Target
            {
                half3 albedo = FurAlbedo(i.uv);
                float h = i.shell.x;
                FurClip(i.uv, h, i.shell.z, albedo);

                float3 normalWS = normalize(i.normalWS);
                Light mainLight = GetMainLight(TransformWorldToShadowCoord(i.positionWS));
                float ndl = dot(normalWS, mainLight.direction);
                half wrap = saturate((ndl + 0.35) / 1.35);   // 털은 빛이 조금 돌아 들어간다
                half3 direct = mainLight.color * (wrap * mainLight.shadowAttenuation * mainLight.distanceAttenuation);
                half3 ambient = SampleSH(normalWS);
                half occlusion = lerp(1.0 - _Occlusion, 1.0, h);  // 뿌리 쪽이 어둡다

                float3 viewWS = GetWorldSpaceNormalizeViewDir(i.positionWS);
                half rim = pow(1.0 - saturate(dot(normalWS, viewWS)), 3.0) * _Rim * h;

                half3 color = albedo * (direct + ambient) * occlusion + albedo * rim * (mainLight.color + ambient);
                color += _EmissionColor.rgb;
                color = MixFog(color, i.fogFactor);
                return half4(color, 1.0);
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
            #pragma target 3.5
            #pragma vertex FurVert
            #pragma fragment FurDepthFrag

            half FurDepthFrag(FurVaryings i) : SV_Target
            {
                FurClip(i.uv, i.shell.x, i.shell.z, FurAlbedo(i.uv));
                return i.positionCS.z;
            }
            ENDHLSL
        }

        Pass
        {
            Name "DepthNormals"
            Tags { "LightMode" = "DepthNormals" }
            Cull Back
            ZWrite On

            HLSLPROGRAM
            #pragma target 3.5
            #pragma vertex FurVert
            #pragma fragment FurDepthNormalsFrag
            #pragma multi_compile_fragment _ _GBUFFER_NORMALS_OCT

            half4 FurDepthNormalsFrag(FurVaryings i) : SV_Target
            {
                FurClip(i.uv, i.shell.x, i.shell.z, FurAlbedo(i.uv));
                float3 normalWS = NormalizeNormalPerPixel(i.normalWS);
                #if defined(_GBUFFER_NORMALS_OCT)
                    float2 octNormalWS = PackNormalOctQuadEncode(normalWS);
                    float2 remappedOctNormalWS = saturate(octNormalWS * 0.5 + 0.5);
                    return half4(PackFloat2To888(remappedOctNormalWS), 0.0);
                #else
                    return half4(normalWS, 0.0);
                #endif
            }
            ENDHLSL
        }
    }
    FallBack Off
}
