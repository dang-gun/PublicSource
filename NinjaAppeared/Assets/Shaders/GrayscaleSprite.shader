Shader "Custom/GrayscaleSprite"
{
    Properties
    {
        [PerRendererData] _MainTex ("Sprite Texture",2D) = "white" {}
        _Color ("Tint", Color) = (1,1,1,1)
        [MaterialToggle] PixelSnap ("Pixel snap", Float) =0
        _GrayBrightness ("Gray Brightness", Range(0,1)) =0.6
    }

    SubShader
    {
        Tags
        {
            "Queue" = "Transparent"
            "IgnoreProjector" = "True"
            "RenderType" = "Transparent"
            "CanUseSpriteAtlas" = "True"
            "PreviewType" = "Plane"
        }

        Cull Off
        Lighting Off
        ZWrite Off
        Blend One OneMinusSrcAlpha

        Pass
        {
            CGPROGRAM
            #pragma vertex SpriteVert
            #pragma fragment GrayscaleFrag
            #pragma target2.0
            // Handle external ETC1 alpha atlases like the default sprite shader
            #pragma multi_compile _ ETC1_EXTERNAL_ALPHA
            // Support pixel snap option
            #pragma multi_compile _ PIXELSNAP_ON

            #include "UnitySprites.cginc"

            // Declare custom property so shader compiles on all targets
            fixed _GrayBrightness;

            fixed4 GrayscaleFrag(v2f IN) : SV_Target
            {
                fixed4 c = SampleSpriteTexture(IN.texcoord) * IN.color;
                fixed gray = dot(c.rgb, fixed3(0.299,0.587,0.114));
                // Apply brightness, then premultiply by alpha like default sprite shader
                gray *= _GrayBrightness;
                gray *= c.a;
                return fixed4(gray, gray, gray, c.a);
            }
            ENDCG
        }
    }
}
