Shader "Hidden/BlitDraw"
{
    Properties
    {
        _MainTex    ("Main Texture", 2D) = "white" {}
        _BrushTex   ("Brush Texture",2D) = "white" {}
        _BrushColor ("Brush Color",  Color) = (1,0,0,1)
        _BrushUV    ("Brush UV",     Vector) = (0.5,0.5,0,0)
        _BrushSize  ("Brush Size",   Float) = 0.1
        _BrushStrength("Brush Strength", Range(0,1)) = 1.0   // <<< новый параметр
    }

    SubShader
    {
        Cull Off
        ZWrite Off
        ZTest Always

        Pass
        {
            CGPROGRAM
            #pragma vertex vert_img
            #pragma fragment frag
            #include "UnityCG.cginc"

            sampler2D _MainTex;
            sampler2D _BrushTex;
            float4    _BrushColor;
            float4    _BrushUV;     // xy – uv, zw не используются
            float     _BrushSize;
            half      _BrushStrength;   // <<< интенсивность

            fixed4 frag (v2f_img i) : SV_Target
            {
                fixed4 col = tex2D(_MainTex, i.uv);

                // UV-координаты кисти (центр круга в _BrushUV.xy)
                float2 d = (i.uv - _BrushUV.xy) / _BrushSize;
                half mask = tex2D(_BrushTex, d + 0.5).a;

                // Применяем цвет и интенсивность
                half4 brush = _BrushColor * mask * _BrushStrength;

                // Смешиваем
                col = lerp(col, brush, brush.a);

                return col;
            }
            ENDCG
        }
    }
}