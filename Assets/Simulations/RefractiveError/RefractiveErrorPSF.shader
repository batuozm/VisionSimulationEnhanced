Shader "Hidden/RefractiveErrorPSF"
{
    Properties
    {
        _MainTex ("Texture", 2D) = "white" {}
    }

    CGINCLUDE
    #include "UnityCG.cginc"

    UNITY_DECLARE_SCREENSPACE_TEXTURE(_MainTex);

    float4 _MainTex_TexelSize;
    float _LeftBlurRadiusPx;
    float _RightBlurRadiusPx;

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

    half4 SampleBlur(float2 uv, float blurRadiusPx)
    {
        if (blurRadiusPx < 0.01)
        {
            return UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv);
        }

        float2 t = _MainTex_TexelSize.xy * blurRadiusPx;

        half4 sum = 0;

        // Center
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv) * 0.20;

        // Cardinals
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 1.0,  0.0) * t) * 0.10;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2(-1.0,  0.0) * t) * 0.10;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.0,  1.0) * t) * 0.10;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.0, -1.0) * t) * 0.10;

        // Diagonals
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.7071,  0.7071) * t) * 0.07;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2(-0.7071,  0.7071) * t) * 0.07;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.7071, -0.7071) * t) * 0.07;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2(-0.7071, -0.7071) * t) * 0.07;

        // Inner ring
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.5,  0.0) * t) * 0.03;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2(-0.5,  0.0) * t) * 0.03;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.0,  0.5) * t) * 0.03;
        sum += UNITY_SAMPLE_SCREENSPACE_TEXTURE(_MainTex, uv + float2( 0.0, -0.5) * t) * 0.03;

        return sum;
    }
    ENDCG

    SubShader
    {
        Cull Off ZWrite Off ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag

            half4 frag(v2f i) : SV_Target
            {
                UNITY_SETUP_STEREO_EYE_INDEX_POST_VERTEX(i);

                float blurRadiusPx = (unity_StereoEyeIndex == 0)
                    ? _LeftBlurRadiusPx
                    : _RightBlurRadiusPx;

                return SampleBlur(i.uv, blurRadiusPx);
            }
            ENDCG
        }
    }
}