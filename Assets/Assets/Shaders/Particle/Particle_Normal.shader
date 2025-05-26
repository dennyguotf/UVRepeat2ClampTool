// Unity built-in shader source. Copyright (c) 2016 Unity Technologies. MIT license (see license.txt)

Shader "Ogre/Particle/Normal" {
    Properties{
        _TintColor("Tint Color", Color) = (1,1,1,1)
        _MainTex("Particle Texture", 2D) = "white" {}
        _InvFade("Soft Particles Factor", Range(0.01,3.0)) = 1.0

        [Enum(UnityEngine.Rendering.BlendMode)]
        _SrcBlend("SrcBlend", Int) = 5 // SrcAlpha
        [Enum(UnityEngine.Rendering.BlendMode)]
        _DstBlend("DstBlend", Int) = 1 // OneMinusSrcAlpha

        [ToggleOff]_IsRotUV("开启旋转", float) = 0
        _RotCenterX("旋转中点X",float) = 0.5
        _RotCenterY("旋转中点Y",float) = 0.5
        _RotAngle("旋转角度",Range(0,360)) = 0
        _RotSpeed("旋转速度",float) = 0

        [ToggleOff]_IsScrollUV("IsScroll", float) = 0
        _ScrollXSpeed("X Scroll Speed", float) = 2
        _ScrollYSpeed("Y Scroll Speed", float) = 2

        [ToggleOff]_IsAniUV("开启动画", float) = 0
        _AniHorizontalAmount("行数", float) = 8 // 行数
        _AniVerticalAmount("列数", float) = 8  // 列数
        _AniSpeed("播放速度", float) = 1 // 播放速度

        [ToggleOff]_IsFadeOut("开启淡出", float) = 0
        _FadeRange("FadeRange",Range(0,1)) = 1

        _ZTestAddValue("深度额外值", float) = 0
    }

    Category{
        Tags { "Queue" = "Transparent+1" "IgnoreProjector" = "True" "RenderType" = "Transparent" "PreviewType" = "Plane" }
        //Blend SrcAlpha One
        Blend[_SrcBlend][_DstBlend]
        ColorMask RGB
        Cull Off 
        Lighting Off
        ZWrite Off

        SubShader {
            Pass {

                CGPROGRAM
                #pragma vertex vert
                #pragma fragment frag
                #pragma target 2.0
                #pragma multi_compile_particles
                #pragma multi_compile_fog

                #include "UnityCG.cginc"
                #include "BasicCommon.cginc"

                sampler2D _MainTex;
                fixed4 _TintColor;
                float4 _MainTex_ST;
                float _ZTestAddValue;

                struct appdata_t {
                    float4 vertex : POSITION;
                    fixed4 color : COLOR;
                    float2 texcoord : TEXCOORD0;
                    UNITY_VERTEX_INPUT_INSTANCE_ID
                };

                struct v2f {
                    float4 vertex : SV_POSITION;
                    fixed4 color : COLOR;
                    float2 texcoord : TEXCOORD0;
                    UNITY_FOG_COORDS(1)
                    #ifdef SOFTPARTICLES_ON
                    float4 projPos : TEXCOORD2;
                    #endif
                    UNITY_VERTEX_OUTPUT_STEREO
                };

                v2f vert(appdata_t v)
                {
                    v2f o;
                    UNITY_SETUP_INSTANCE_ID(v);
                    UNITY_INITIALIZE_VERTEX_OUTPUT_STEREO(o);

                    fixed3 pos = v.vertex;
                    if (_ZTestAddValue != 0)
                    {//额外增加深度
                        fixed3 vertexPosWorld = mul(unity_ObjectToWorld, v.vertex).xyz;// 计算顶点在世界空间中的位置
                        fixed3 cameraDir = normalize(_WorldSpaceCameraPos - vertexPosWorld);// 计算摄像机方向（从顶点指向摄像机）
                        pos += cameraDir * _ZTestAddValue;
                    }
                    o.vertex = UnityObjectToClipPos(pos);

                    #ifdef SOFTPARTICLES_ON
                    o.projPos = ComputeScreenPos(o.vertex);
                    COMPUTE_EYEDEPTH(o.projPos.z);
                    #endif
                    o.color = v.color;
                    o.texcoord = v.texcoord;
                    UNITY_TRANSFER_FOG(o,o.vertex);
                    return o;
                }

                UNITY_DECLARE_DEPTH_TEXTURE(_CameraDepthTexture);
                float _InvFade;

                fixed4 frag(v2f i) : SV_Target
                {
                    #ifdef SOFTPARTICLES_ON
                    float sceneZ = LinearEyeDepth(SAMPLE_DEPTH_TEXTURE_PROJ(_CameraDepthTexture, UNITY_PROJ_COORD(i.projPos)));
                    float partZ = i.projPos.z;
                    float fade = saturate(_InvFade * (sceneZ - partZ));
                    i.color.a *= fade;
                    #endif

                    float2 uv = ScrollUV(AniUV(TransformTex(_MainTex, _MainTex_ST, i.texcoord)));
                    fixed4 col = 1 * i.color * _TintColor * tex2D(_MainTex, uv);
                    col.a = saturate(col.a); // alpha should not have double-brightness applied to it, but we can't fix that legacy behavior without breaking everyone's effects, so instead clamp the output to get sensible HDR behavior (case 967476)

                    float fadeAlpha = fadeOut(i.texcoord);
                    col.a *= fadeAlpha;

                    UNITY_APPLY_FOG_COLOR(i.fogCoord, col, fixed4(0,0,0,0)); // fog towards black due to our blend mode
                    
                    return col;
                }
                ENDCG
            }
        }
    }
}
