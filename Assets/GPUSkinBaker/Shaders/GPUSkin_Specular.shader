
Shader "GPUSkin/Specular"
{
	Properties
	{
		_Color("Main Color", Color) = (1,1,1,1)
		_SpecColor("Specular Color", Color) = (0.5, 0.5, 0.5, 1)
		 _Shininess("Shininess", Range(0.03, 1)) = 0.078125
		 _MainTex("Base (RGB)", 2D) = "white" {}
		_BumpMap("Normalmap", 2D) = "bump" {}
		_GlossTex("Gloss (A)", 2D) = "white" {}

		_PosTex("Bake PosTex", 2D) = "black"{}
		_BakeNmlTex("Bake Normal texture", 2D) = "white"{}
		_ClipTime("Normalized Time", Range(0,1)) = 0
		_ClipStart("Clip Start v", Float) = 0
		_ClipEnd("Clip End v", Float) = 1
	}

		CGINCLUDE
		sampler2D _MainTex;
		sampler2D _GlossTex;
		sampler2D _BumpMap;
		fixed4 _Color;
		half _Shininess;

		sampler2D _PosTex;
		sampler2D _BakeNmlTex;
		UNITY_INSTANCING_BUFFER_START(Props)
			UNITY_DEFINE_INSTANCED_PROP(float4, _PosTex_TexelSize)
			UNITY_DEFINE_INSTANCED_PROP(float, _ClipTime)
			UNITY_DEFINE_INSTANCED_PROP(float, _ClipStart)
			UNITY_DEFINE_INSTANCED_PROP(float, _ClipEnd)
			UNITY_INSTANCING_BUFFER_END(Props)

			struct Input {
			float2 uv_MainTex;
			float2 uv_BumpMap;
		};

		void vert(inout appdata_full v) {
			float clipTime = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipTime);
			float start_v = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipStart);
			float end_v = UNITY_ACCESS_INSTANCED_PROP(Props, _ClipEnd);
			float t = clipTime;

			float x = v.texcoord1;
			float y = lerp(start_v, end_v, t);
			float4 pos = tex2Dlod(_PosTex, float4(x, y, 0, 0));
			float3 normal = tex2Dlod(_BakeNmlTex, float4(x, y, 0, 0));

			v.vertex = pos;
			v.normal = normal;
		}

		void surf(Input IN, inout SurfaceOutput o) {
			fixed4 tex = tex2D(_MainTex, IN.uv_MainTex);
			o.Albedo = tex.rgb * _Color.rgb;
			o.Gloss = tex2D(_GlossTex, IN.uv_MainTex).r;
			o.Alpha = tex.a * _Color.a;
			o.Specular = _Shininess;
			o.Normal = UnpackNormal(tex2D(_BumpMap, IN.uv_BumpMap));
		}
		ENDCG

		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 400

			CGPROGRAM
			#pragma surface surf BlinnPhong noforwardadd noshadow vertex:vert addshadow
			#pragma target 3.0
			ENDCG
		}

		SubShader{
			Tags { "RenderType" = "Opaque" }
			LOD 400

			CGPROGRAM
			#pragma surface surf BlinnPhong nodynlightmap noforwardadd noshadow vertex:vert addshadow
			ENDCG
		}

		CustomEditor "GPUSkinShaderEditor"
		FallBack "Legacy Shaders/Specular"
}
