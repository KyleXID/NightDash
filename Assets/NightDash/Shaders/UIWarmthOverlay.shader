// Sprint B / M2 Phase 2 — UI shader that adds a distance-based warmth wash
// on top of any uGUI Image / RawImage texture. Used by the lobby campfire
// background so the painted props (logs, stones, trees) close to the fire
// pick up an orange tint that pulses with the flames, while screen edges
// stay cool. Runs in Built-in / URP UI without needing the 2D Renderer.

Shader "NightDash/UI/WarmthOverlay"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture", 2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)

        // Center of the wash in UV space (0..1). For a full-screen background
        // (UV maps to screen rect) the campfire is roughly (0.5, 0.40).
        _FireCenterUV ("Fire Center UV", Vector) = (0.5, 0.40, 0, 0)

        // Radius (in UV units) at which warmth falls off to zero.
        _FireRadiusUV ("Fire Radius UV", Float) = 0.55

        // Warm color added to the texture. Alpha unused.
        _WarmthColor ("Warmth Color", Color) = (0.65, 0.32, 0.08, 1)

        // Overall intensity multiplier. Driven from C# to apply the pulse.
        _WarmthIntensity ("Warmth Intensity", Float) = 0.55

        // Falloff exponent. <1 = ease-out (close holds bright longer).
        _GradientPower ("Gradient Power", Float) = 0.7

        // Standard UI mask helpers — keep the shader compatible with
        // RectMask2D / Stencil even if we don't use them right now.
        _StencilComp ("Stencil Comparison", Float) = 8
        _Stencil ("Stencil ID", Float) = 0
        _StencilOp ("Stencil Operation", Float) = 0
        _StencilWriteMask ("Stencil Write Mask", Float) = 255
        _StencilReadMask ("Stencil Read Mask", Float) = 255
        _ColorMask ("Color Mask", Float) = 15
    }

    SubShader
    {
        Tags
        {
            "Queue"="Transparent"
            "IgnoreProjector"="True"
            "RenderType"="Transparent"
            "PreviewType"="Plane"
            "CanUseSpriteAtlas"="True"
        }

        Stencil
        {
            Ref [_Stencil]
            Comp [_StencilComp]
            Pass [_StencilOp]
            ReadMask [_StencilReadMask]
            WriteMask [_StencilWriteMask]
        }

        Cull Off
        Lighting Off
        ZWrite Off
        ZTest [unity_GUIZTestMode]
        Blend SrcAlpha OneMinusSrcAlpha
        ColorMask [_ColorMask]

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            float4 _MainTex_ST;
            fixed4 _Color;
            float4 _FireCenterUV;
            float _FireRadiusUV;
            fixed4 _WarmthColor;
            float _WarmthIntensity;
            float _GradientPower;

            struct appdata
            {
                float4 vertex : POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            struct v2f
            {
                float4 vertex : SV_POSITION;
                float2 uv : TEXCOORD0;
                fixed4 color : COLOR;
            };

            v2f vert(appdata v)
            {
                v2f o;
                o.vertex = UnityObjectToClipPos(v.vertex);
                o.uv = TRANSFORM_TEX(v.uv, _MainTex);
                o.color = v.color * _Color;
                return o;
            }

            fixed4 frag(v2f i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv) * i.color;

                float2 fireCenter = _FireCenterUV.xy;
                float dist = distance(i.uv, fireCenter);
                float proximity = saturate(1.0 - dist / _FireRadiusUV);
                float gradient = pow(proximity, _GradientPower);

                col.rgb += _WarmthColor.rgb * gradient * _WarmthIntensity;
                return col;
            }
            ENDCG
        }
    }
}
