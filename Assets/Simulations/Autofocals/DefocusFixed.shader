Shader "Hidden/DefocusFixed"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "black" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    sampler2D _MainTex;
    sampler2D _defocusTexture;
    sampler2D _blurredTex;
    UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);

    float4 _MainTex_TexelSize;
    float _OpticalPower;
    float _CocConstant;
    float _BokehRadius;
    int _downscaleFactor;

    struct appdata
    {
        float4 vertex : POSITION;
        float2 uv : TEXCOORD0;
        UNITY_VERTEX_INPUT_INSTANCE_ID
    };

    struct v2f
    {
        float2 uv : TEXCOORD0;
        float4 vertex : SV_POSITION;
        UNITY_VERTEX_OUTPUT_STEREO
    };

    v2f vert(appdata v)
    {
        v2f o;

        UNITY_SETUP_INSTANCE_ID(v);
        UNITY_INITIALIZE_OUTPUT(v2f, o);
        UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

        o.vertex = UnityObjectToClipPos(v.vertex);
        o.uv = v.uv;

        return o;
    }

    float SafeDepthMeters(float2 uv)
    {
        float rawDepth = SAMPLE_DEPTH_TEXTURE(_CameraDepthTexture, uv);
        float depthMeters = LinearEyeDepth(rawDepth);

        float farClip = max(_ProjectionParams.z, 0.1);
        depthMeters = clamp(depthMeters, 0.05, farClip);

        return depthMeters;
    }

    half SafeCoc(float depthMeters)
    {
        float safeDepth = max(depthMeters, 0.05);
        float defocus = (_OpticalPower * safeDepth - 1.0) / safeDepth;
        float coc = defocus * _CocConstant;

        float maxCoc = max(_BokehRadius, 1.0);
        coc = clamp(coc, -maxCoc, maxCoc);

        return half(coc);
    }

    half4 SafeSample(sampler2D tex, float2 uv)
    {
        return tex2D(tex, uv);
    }

    ENDCG

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass // 0: CoC pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float depthMeters = SafeDepthMeters(i.uv);
                return SafeCoc(depthMeters);
            }
            ENDCG
        }

        Pass // 1: Prefilter pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 texel = _MainTex_TexelSize.xy;

                half coc0 = tex2D(_defocusTexture, i.uv + texel * float2(-0.5, -0.5)).r;
                half coc1 = tex2D(_defocusTexture, i.uv + texel * float2( 0.5, -0.5)).r;
                half coc2 = tex2D(_defocusTexture, i.uv + texel * float2(-0.5,  0.5)).r;
                half coc3 = tex2D(_defocusTexture, i.uv + texel * float2( 0.5,  0.5)).r;

                half cocMin = min(min(coc0, coc1), min(coc2, coc3));
                half cocMax = max(max(coc0, coc1), max(coc2, coc3));
                half coc = cocMax >= -cocMin ? cocMax : cocMin;

                half maxCoc = half(max(_BokehRadius, 1.0));
                coc = clamp(coc, -maxCoc, maxCoc);

                half4 source = tex2D(_MainTex, i.uv);
                return half4(source.rgb, coc);
            }
            ENDCG
        }

        Pass // 2: Blur pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 center = tex2D(_MainTex, i.uv);

                half coc = center.a;
                half maxRadius = half(max(_BokehRadius, 1.0));
                half radius = clamp(abs(coc), 0.0h, maxRadius);

                float2 texel = _MainTex_TexelSize.xy * radius;

                half4 sum = 0;
                half weightSum = 0;

                sum += tex2D(_MainTex, i.uv) * 0.20h;
                weightSum += 0.20h;

                sum += tex2D(_MainTex, i.uv + texel * float2( 1.0,  0.0)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2(-1.0,  0.0)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2( 0.0,  1.0)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2( 0.0, -1.0)) * 0.10h;
                weightSum += 0.40h;

                sum += tex2D(_MainTex, i.uv + texel * float2( 0.7071,  0.7071)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2(-0.7071,  0.7071)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2( 0.7071, -0.7071)) * 0.10h;
                sum += tex2D(_MainTex, i.uv + texel * float2(-0.7071, -0.7071)) * 0.10h;
                weightSum += 0.40h;

                half4 blurred = sum / max(weightSum, 0.0001h);

                half blurAlpha = saturate(radius / maxRadius);
                return half4(blurred.rgb, blurAlpha);
            }
            ENDCG
        }

        Pass // 3: Postfilter pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float2 texel = _MainTex_TexelSize.xy;

                half4 s = 0;
                s += tex2D(_MainTex, i.uv + texel * float2(-0.5, -0.5));
                s += tex2D(_MainTex, i.uv + texel * float2( 0.5, -0.5));
                s += tex2D(_MainTex, i.uv + texel * float2(-0.5,  0.5));
                s += tex2D(_MainTex, i.uv + texel * float2( 0.5,  0.5));

                return s * 0.25h;
            }
            ENDCG
        }

        Pass // 4: Combine pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                half4 source = tex2D(_MainTex, i.uv);
                half4 blurred = tex2D(_blurredTex, i.uv);

                half coc = tex2D(_defocusTexture, i.uv).r;
                half maxCoc = half(max(_BokehRadius, 1.0));
                coc = clamp(coc, -maxCoc, maxCoc);

                half dofStrength = smoothstep(0.1h, 1.0h, abs(coc));
                half blend = saturate(dofStrength + blurred.a - dofStrength * blurred.a);

                half3 color = lerp(source.rgb, blurred.rgb, blend);

                return half4(color, source.a);
            }
            ENDCG
        }

        Pass // 5: Depth debug pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float depthMeters = SafeDepthMeters(i.uv);
                float farClip = max(_ProjectionParams.z, 0.1);
                half value = saturate(depthMeters / farClip);

                return half4(value, value, value, 1);
            }
            ENDCG
        }

        Pass // 6: Distance debug pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float depthMeters = SafeDepthMeters(i.uv);
                half value = saturate(depthMeters * 0.15);

                return half4(value, value, value, 1);
            }
            ENDCG
        }
    }

    Fallback Off
}