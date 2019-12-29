Shader "Custom/WindowSurfaceShader"
{
    Properties
    {
        _MainTex ("Albedo (RGB)", 2D) = "white" {}
    }
    SubShader
    {
        Tags {"Queue" = "Transparent" "RenderType"="Transparent" }
        LOD 200
        
        ZWrite On
        ZTest LEqual
   
        CGPROGRAM
 
        #pragma surface surf Standard alpha:fade
        #pragma target 3.0
 
        sampler2D _MainTex;
 
        struct Input {
            float2 uv_MainTex;
        };
        
        void surf (Input IN, inout SurfaceOutputStandard o)
        {
            fixed4 c = tex2D (_MainTex, IN.uv_MainTex).rgba;
            o.Albedo = c.rgb;
            o.Emission = c.rgb / 2;
            o.Alpha = c.a;
        }
        ENDCG
    }
    FallBack "Standard"
}
