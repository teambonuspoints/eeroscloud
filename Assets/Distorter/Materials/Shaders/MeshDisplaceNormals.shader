
Shader "Distorter/MeshDisplaceNormals" {

    Properties {
	  	_Pos("DisplacerPosition", Vector) = (1,1,1,1)
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
				float3 _Pos;

                struct appdata
				{
					float4 vertex : POSITION;
					float3 normal : NORMAL;
				};

                struct v2f {
	                half3 worldNormal : TEXCOORD0;
		            float4 pos : SV_POSITION;
		            fixed4 color: COLOR;
                };
           

                v2f vert(appdata v) {
//                	float4 v = ve.vertex;
//                	float3 normal = ve.normal;

                    v2f o;

//                 
//        			// TODO: should take direction the camera is facing, now bulge comes at exact point position, not towards dir..
//					float distMulti = (_Limit-min(_Limit,distance(v.vertex.xyz, _Pos)))/_Limit; //distance falloff
//					float3 dir = normalize(v.vertex.xyz-_Pos);
//					v.vertex.xyz += dir * (distMulti*_Amount);
//					v.vertex.xz += dir * (distMulti*_Amount);
//					o.pos = mul(UNITY_MATRIX_MVP, v.vertex);
//


                 
        			// TODO: should take direction the camera is facing, now bulge comes at exact point position, not towards dir..
					float distMulti = (_Limit-min(_Limit,distance(v.vertex.xyz, _Pos)))/_Limit; //distance falloff
					float3 dir = normalize(v.vertex.xyz-_Pos);
					v.vertex.xyz += dir * (distMulti*_Amount);
					v.vertex.xz += dir * (distMulti*_Amount);
					o.pos = mul(UNITY_MATRIX_MVP, v.vertex);



//
//					float4 pos = mul(UNITY_MATRIX_MV, v.vertex);
//                   
//                    float distanceSquared = pos.x * pos.x + pos.z * pos.z;
//                    pos.y += 5*sin(distanceSquared*_SinTime.x/1000);
//                    float y = pos.y;
//                    float x = pos.x;
//                    float om = sin(distanceSquared*_SinTime.x/5000) * _SinTime.x;
//                    pos.y = x*sin(om)+y*cos(om);
//                    pos.x = x*cos(om)-y*sin(om);
//                   
//                    o.pos = mul(UNITY_MATRIX_P, pos);
//
//
//                    float lenToVert = length(ObjSpaceViewDir(v.vertex));

                    //o.uv_MainTex = TRANSFORM_TEX(v.texcoord, _MainTex);

                    o.worldNormal = UnityObjectToWorldNormal(v.normal);

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