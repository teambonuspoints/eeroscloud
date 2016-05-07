Shader "UnityCoder/PointCloudColorsTintDX11" 
{
	Properties {
		_Color ("Main Color", Color) = (0,1,0,0.5)
		_Alpha ("Alpha", Range (0,1)) = 0.5
	}

	SubShader 
	{
		Tags { "RenderType"="Transparent" "Queue"="Transparent" }

		// blendmode
		Blend SrcAlpha OneMinusSrcAlpha     // Alpha blending * default *
		//Blend One One                       // Additive
		//Blend OneMinusDstColor One          // Soft Additive
		//Blend DstColor Zero                 // Multiplicative
		//Blend DstColor SrcColor             // 2x Multiplicative

		Pass 
		{
			CGPROGRAM
			#pragma target 5.0
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			StructuredBuffer<half3> buf_Points;
			StructuredBuffer<fixed3> buf_Colors;

			fixed4 _Color;
			fixed _Alpha;

			struct ps_input {
				half4 pos : SV_POSITION;
				fixed4 customColor : TEXCOORD1;
			};



			ps_input vert (uint id : SV_VertexID, uint inst : SV_InstanceID)
			{
				ps_input o;
				o.pos = mul (UNITY_MATRIX_VP, half4(buf_Points[id],1.0f));
				o.customColor = fixed4(buf_Colors[id],_Alpha)*_Color;
				return o;
			}

			float4 frag (ps_input i) : COLOR
			{
				return i.customColor;
			}

			ENDCG

		}
	}

	Fallback Off
}