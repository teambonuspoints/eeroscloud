Shader "vertexColorCylinderDisplace1" {

	Properties {
		//_LightPos("Light Position", Vector) = (0,0,0,0)
		_LightLimit("Bulge Threshold", Float) = 5
		_Pos("Displacer Position", Vector) = (1,1,1,1)
		_Dir("Displacer Direction",Vector) = (0,0,1,0)
		_Limit("Distance Limit", Float) = 5
		_Amount("Extrusion Amount", Float) = 3
	} 

	SubShader {
		LOD 200

		Pass {
			CGPROGRAM
			#pragma vertex vert
			#pragma fragment frag
			#include "UnityCG.cginc"

			float _Amount;
			float _Limit;
			float _LightLimit;
			float3 _Pos;
			float3 _Dir;
			//float3 _LightPos;

			static const float PI = 3.14159265f;

			struct v2f 
			{
				half3 worldNormal : TEXCOORD0;
				float4 pos : SV_POSITION;
				float size : PSIZE0;
			};

			float4 gaeel(float4 v, float3 normal){
				float4 pos = mul(UNITY_MATRIX_MV, v);

				float distanceSquared = pos.x * pos.x + pos.z * pos.z;
				pos.y += 5*sin(distanceSquared*_SinTime.x/1000);
				float y = pos.y;
				float x = pos.x;
				float om = sin(distanceSquared*_SinTime.x/5000) * _SinTime.x;
				pos.y = x*sin(om)+y*cos(om);
				pos.x = x*cos(om)-y*sin(om);

				//o.pos = mul(UNITY_MATRIX_P, pos);
				return mul(UNITY_MATRIX_P, pos);
			}

			float capsuleLineDist(float3 p, float3 a, float3 b, float r){
				float3 pa = p - a;
				float3 ba = b - a;
				float h = clamp(dot(pa, ba)/dot(ba, ba), 0.0, 1.0);
				return length(pa - ba * h) - r;
			}

			v2f bubbler(float4 v){
				v2f o;
				// TODO: should take direction the camera is facing, now bulge comes at exact point position, not towards dir..
				float distMulti = (_Limit-min(_Limit,distance(v.xyz, _Pos)))/_Limit; //distance falloff
				float3 dir = normalize(v.xyz-_Pos);
				v.xyz += dir * (distMulti*_Amount);
				v.xz += dir * (distMulti*_Amount);
				o.pos = mul(UNITY_MATRIX_MVP, v);
				return o;
			}
			
			float trigStep(float x){
				float a = clamp(x, 0.0, 1.0);
				return (cos(a * PI) + 1)/2;
			}

			v2f capsuleBubbler(float4 v, float3 nrm){
				v2f o; 
				float3 lineJump = _Dir * 100;
				// TODO: should take direction the camera is facing, now bulge comes at exact point position, not towards dir..
				//float distMulti = 
				//	//(_Limit-min(_Limit,distance(v.xyz, _Pos)))/_Limit;
				//	_Limit - min(
				//		_Limit,
				//		capsuleLineDist(
				//			v.xyz,
				//			_Pos - lineJump,
				//			_Pos + lineJump,
				//			0.0))
				//	/ _Limit;
				//float3 dir = normalize(v.xyz-_Pos);
				// ....
				//v.xyz += dir * (distMulti*_Amount);
				//v.xz += dir * (distMulti*_Amount);
				float cdist = capsuleLineDist(
								v.xyz,
								_Pos - lineJump,
								_Pos + lineJump,
								0.0);
				float bulgedFctr = trigStep(cdist / _Limit);
				float3 dir = normalize(nrm);
				float bulgedFctr2 = bulgedFctr * _Amount;
				v.xyz += dir * bulgedFctr2;
				o.pos = mul(UNITY_MATRIX_MVP, v);
				return o;
			}

			v2f vert(float4 v : POSITION, float3 normal : NORMAL) {
				v2f o;

				//o.pos = {0,0,0,0}; // gaeel(v, normal);
				//o = bubbler(v);

				o = capsuleBubbler(v, normal);

				//o.uv_MainTex = TRANSFORM_TEX(v.texcoord, _MainTex);

				o.worldNormal = UnityObjectToWorldNormal(normal);

				return o;
			}


			float4 frag(v2f i) : COLOR {

				fixed4 c = 0;
				// normal is a 3D vector with xyz components; in -1..1
				// range. To display it as color, bring the range into 0..1
				// and put into red, green, blue components

				c.rgb = i.worldNormal*0.5+0.5;
				return c;

			}
			ENDCG
		}
	}
}