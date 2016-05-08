// some parts of the source: http://forum.unity3d.com/threads/5785-shader-SetGlobalVector?p=43611&viewfull=1#post43611
// modified by http://unitycoder.com/blog/

Shader "UnityCoder/VertexColorDisplacePseudoLight2"
{
	Properties {
		//_LightPos("LightPosition", Vector) = (0,0,0,0)
		//_LightLimit("Light Limit", Float) = 5
		_Pos("DisplacerPosition", Vector) = (1,1,1,1)
		_Limit("Distance Limit", Float) = 5
		_Amount("Extrusion Amount", Float) = 3
	}

	SubShader
	{
		Tags { "Queue"="Geometry"}
		ZWrite On
		Blend SrcAlpha OneMinusSrcAlpha // alpha
		Fog { Mode Off }
		Pass
		{
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#pragma fragmentoption ARB_precision_hint_fastest

			float _Amount;
			float _Limit;
			float _LightLimit;
			float3 _Pos;
			float3 _LightPos;

			struct appdata
			{
				float4 vertex : POSITION;
				float4 color : COLOR;
			};
			struct v2f
			{
				float4 pos : SV_POSITION;
				fixed4 color : COLOR;
				float size:PSIZE;

			};
			v2f vert (appdata v)
			{
				v2f o;
				// TODO: should take direction the camera is facing, now bulge comes at exact point position, not towards dir..
				float distMulti = (_Limit-min(_Limit,distance(v.vertex.xyz, _Pos)))/_Limit; //distance falloff
				float3 dir = normalize(v.vertex.xyz-_Pos);
				v.vertex.xyz += dir * (distMulti*_Amount);
				v.vertex.xz += dir * (distMulti*_Amount);
				o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
				// fakelight effect for now..
				float distLight = (_LightLimit-min(_LightLimit,distance(v.vertex.xyz, _LightPos)))/_LightLimit; //distance falloff

				//o.color = v.color-(1-distLight);
				//o.color.a = 1-distMulti;
				o.color = v.color;
				o.color.a = 1;

				float blah = 10.1;
				o.size = blah;

				return o;
			}
			half4 frag(v2f i) : COLOR
			{
				fixed4 c = i.color; // *1.2
				return c;
			}
			ENDCG
		}
	}
}