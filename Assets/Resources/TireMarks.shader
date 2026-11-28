Shader "Test Car Game/Tire Marks"
{
    SubShader
    {
        Tags { "Queue" = "Transparent-20" "RenderType" = "Transparent" }
        Blend SrcAlpha OneMinusSrcAlpha
        Cull Off
        ZWrite Off
        Offset -1, -1

        Pass
        {
            CGPROGRAM
            #pragma vertex vert
            #pragma fragment frag
            #include "UnityCG.cginc"

            struct Vertex
            {
                float4 position : POSITION;
                fixed4 color : COLOR;
                float2 uv : TEXCOORD0;
            };

            struct Fragment
            {
                float4 position : SV_POSITION;
                fixed4 color : COLOR;
                float edge : TEXCOORD0;
            };

            Fragment vert(Vertex input)
            {
                Fragment output;
                output.position = UnityObjectToClipPos(input.position);
                output.color = input.color;
                output.edge = input.uv.x;
                return output;
            }

            fixed4 frag(Fragment input) : SV_Target
            {
                fixed4 color = input.color;
                color.a *= smoothstep(0.0, 0.19, min(input.edge, 1.0 - input.edge));
                return color;
            }
            ENDCG
        }
    }
}
