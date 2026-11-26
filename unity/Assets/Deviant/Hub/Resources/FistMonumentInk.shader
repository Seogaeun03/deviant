Shader "Deviant/FistMonumentInk"
{
    Properties { [PerRendererData] _MainTex ("Sprite", 2D) = "white" {} _Saturation ("Village colour", Range(0,1)) = 0 }
    SubShader
    {
        Tags { "Queue"="Transparent" "RenderType"="Transparent" "IgnoreProjector"="True" }
        Cull Off Lighting Off ZWrite Off Blend SrcAlpha OneMinusSrcAlpha
        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"
            struct appdata { float4 vertex:POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            struct v2f { float4 vertex:SV_POSITION; float2 uv:TEXCOORD0; fixed4 color:COLOR; };
            sampler2D _MainTex; float _Saturation;
            v2f vert(appdata v) { v2f o; o.vertex=UnityObjectToClipPos(v.vertex); o.uv=v.uv; o.color=v.color; return o; }
            float3 SketchTileTone(float3 colour)
            {
                // Use the ink, stone and paper swatches from the village sketch terrain atlas.
                #ifdef UNITY_COLORSPACE_GAMMA
                colour = GammaToLinearSpace(colour);
                #endif
                float luminance = dot(colour, float3(.299, .587, .114));
                float3 tone = luminance < .03 ? float3(63, 62, 60) :
                              luminance < .24 ? float3(111, 110, 105) :
                              luminance < .48 ? float3(160, 157, 150) :
                              luminance < .72 ? float3(204, 199, 190) :
                                               float3(254, 247, 238);
                tone /= 255.0;
                #ifndef UNITY_COLORSPACE_GAMMA
                tone = GammaToLinearSpace(tone);
                #endif
                return tone;
            }
            fixed4 frag(v2f i):SV_Target
            {
                fixed4 c=tex2D(_MainTex,i.uv)*i.color;
                c.rgb=lerp(SketchTileTone(c.rgb),c.rgb,_Saturation); return c;
            }
            ENDCG
        }
    }
}
