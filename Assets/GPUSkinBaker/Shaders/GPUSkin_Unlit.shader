// Wrote by Joe Rozek
// Commercial use -- yes
// Modification -- yes
// Distribution -- yes
// Private use -- yes
// YusufuCote@gmail.com

Shader "GPUSkin/Unlit"
{
	Properties
	{
		_MainTex ("Texture", 2D) = "white" {}
		_PosTex("Bake PosTex", 2D) = "black"{}
		_ClipTime("Normalized Time", Range(0,1)) = 0
		_ClipStart("Clip Start v", Float) = 0
		_ClipEnd("Clip End v", Float) = 1
		[Toggle(ANIM_LOOP)] _Loop("loop", Float) = 0
	}
	SubShader
	{
		Tags { "RenderType"="Opaque" }

		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma multi_compile_instancing
			#include "UnityCG.cginc"

			#pragma multi_compile ___ ANIM_LOOP
			#pragma target 3.0

			struct appdata
			{
				float4 vertex : POSITION;
				float2 uv : TEXCOORD0;
				float2 uv2 : TEXCOORD1;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			struct v2f
			{
				float2 uv : TEXCOORD0;
				float4 vertex : SV_POSITION;
				UNITY_VERTEX_INPUT_INSTANCE_ID
			};

			sampler2D _MainTex, _PosTex;
			UNITY_INSTANCING_BUFFER_START(Props)
				UNITY_DEFINE_INSTANCED_PROP(float4, _MainTex_ST)
				UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex_TexelSize)
				UNITY_DEFINE_INSTANCED_PROP(float, _ClipTime)
				UNITY_DEFINE_INSTANCED_PROP(float, _ClipStart)
				UNITY_DEFINE_INSTANCED_PROP(float, _ClipEnd)
			UNITY_INSTANCING_BUFFER_END(Props)

			
			v2f vert (appdata v)//, uint vid : SV_VertexID)
			{
				v2f o;

				UNITY_SETUP_INSTANCE_ID(v);
				UNITY_TRANSFER_INSTANCE_ID(v, o);

				float clipTime = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipTime);
				float start_v = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipStart);
				float end_v = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipEnd);
#if ANIM_LOOP
				float t = fmod(_Time.y, 1);
#else
				float t = clipTime;
#endif			

				float x = v.uv2;//(vid + 0.5) * UNITY_ACCESS_INSTANCED_PROP(Props, _PosTex_TexelSize.x);
				float y = lerp(start_v, end_v, t);
				float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));

				o.vertex = UnityObjectToClipPos(pos);
				o.uv = TRANSFORM_TEX(v.uv, UNITY_ACCESS_INSTANCED_PROP(Props, _MainTex));
				return o;
			}
			
			fixed4 frag (v2f i) : SV_Target
			{
				UNITY_SETUP_INSTANCE_ID(i);
				fixed4 col = tex2D(_MainTex, i.uv);
				return col;
			}
			ENDCG
		}
	}
	CustomEditor "GPUSkinShaderEditor"
}
