
Shader "DistortionPlusNormals-FLAVOR2" {

    SubShader {
        LOD 200
       
        Pass {
            CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #include "UnityCG.cginc"
           
                struct v2f {
	                half3 worldNormal : TEXCOORD0;
                    float4 pos : SV_POSITION;
                };
           

                v2f vert(float4 v : POSITION, float3 normal : NORMAL) {
                    v2f o;
                    //o.pos = mul (UNITY_MATRIX_MVP, v.vertex);
                    //Gaeel's vertex modification starts here...
                    float4 pos = mul(UNITY_MATRIX_MV, v);
                   
                    float distanceSquared = pos.x * pos.x + pos.z * pos.z;
                    pos.y += 5*sin(distanceSquared*_SinTime.x/100);
                    float y = pos.y;
                    float x = pos.x;
                    float om = sin(distanceSquared*_SinTime.x/500) * _SinTime.x;
                    pos.y = x*sin(om)+y*cos(om);
                    pos.x = x*cos(om)-y*sin(om);
                   
                    o.pos = mul(UNITY_MATRIX_P, pos);
                    //...and ends here.
                   
                    //o.uv_MainTex = TRANSFORM_TEX(v.texcoord, _MainTex);

                    o.worldNormal = UnityObjectToWorldNormal(normal);

                    return o;
                }
           

                float4 frag(v2f i) : COLOR {

                    fixed4 c = 0;
	                // normal is a 3D vector with xyz components; in -1..1
	                // range. To display it as color, bring the range into 0..1
	                // and put into red, green, blue components

	                c.rgb = i.worldNormal*0.4-0.2;
	                return c;

                }
            ENDCG
        }
    }
}