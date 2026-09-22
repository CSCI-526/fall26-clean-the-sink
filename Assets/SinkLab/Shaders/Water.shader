Shader "SinkLab/Water"
{
 Properties { _BaseColor("Water tint", Color) = (0.3,0.78,0.95,0.65) }
 SubShader
 {
  Tags { "RenderPipeline"="UniversalPipeline" "Queue"="Transparent" "RenderType"="Transparent" }
  Pass
  {
   Blend SrcAlpha OneMinusSrcAlpha
   ZWrite Off
   Cull Off
   HLSLPROGRAM
   #pragma vertex vert
   #pragma fragment frag
   #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
   struct Attributes { float4 positionOS:POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
   struct Varyings { float4 positionHCS:SV_POSITION; half4 color:COLOR; float2 uv:TEXCOORD0; };
   CBUFFER_START(UnityPerMaterial)
    half4 _BaseColor;
   CBUFFER_END
   Varyings vert(Attributes i) { Varyings o; o.positionHCS=TransformObjectToHClip(i.positionOS.xyz);o.color=i.color*_BaseColor;o.uv=i.uv;return o; }
   half4 frag(Varyings i):SV_Target { half4 c=i.color;c.a*=saturate(1-abs(i.uv.y*2-1));return c; }
   ENDHLSL
  }
 }
}
