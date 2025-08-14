Shader "Custom/VoxelShader_WithLighting"
{
    Properties
    {
        [Header(Lighting)]
        _Ambient ("Ambient Light", Range(0,1)) = 0.3
        [Toggle] _EnableLighting ("Enable Lighting", Float) = 1
        
        [Header(Debug)]
        [Toggle] _ShowNormals ("Show Normals (Debug)", Float) = 0
    }
    SubShader
    {
        Tags {
            "RenderType" = "Opaque" // This is a solid object (not transparent)
            "RenderPipeline" = "UniversalPipeline" // Works with URP
        }
        LOD 100 // Level of detail (100 = highest quality)
        
        Pass
        {
            Name "ForwardLit"  // This pass handles lighting
            Tags { "LightMode" = "UniversalForward" } // Works with URP lighting
            
            Cull Back // Don't render backfaces (optimization)
            ZWrite On // Enables depth writing (objects block each other)
            ZTest LEqual // Standard depth test
            
            HLSLPROGRAM // Use modern HLSL (not old Cg)
            #pragma target 4.5
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma instancing_options renderinglayer   // lets Entities control rendering layers
            #pragma multi_compile _ DOTS_INSTANCING_ON  // REQUIRED for BRG/Entities Graphics

            // URP Core and Lighting libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct attributes // "What the mesh gives us"
            {
                float4 positionOS    : POSITION;  // Object-space position
                float3 normalOS      : NORMAL;    // Object-space normal (surface direction)
                half4 color          : COLOR;     // Vertex color
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            struct varyings // "What we calculate and pass to the pixel shader"
            {
                float4 positionsCS  : SV_POSITION;   // Screen position
                half3 normalWS      : TEXCOORD0;  // World-space normal
                half4 color         : COLOR;      // Pass-through color
                UNITY_VERTEX_INPUT_INSTANCE_ID
            };

            // (URP/Lit-like convention)
            CBUFFER_START(UnityPerMaterial)
                float _Ambient;
                float _EnableLighting;
                float _ShowNormals;
            CBUFFER_END

            // DOTS-instanced properties: at least one property must be declared.
            // We mirror the same names so that, if no per-instance override is set,
            // the shader will fall back to the material value.
            #ifdef UNITY_DOTS_INSTANCING_ENABLED
            UNITY_DOTS_INSTANCING_START(MaterialPropertyMetadata)
                UNITY_DOTS_INSTANCED_PROP(float, _Ambient)
                UNITY_DOTS_INSTANCED_PROP(float, _EnableLighting)
                UNITY_DOTS_INSTANCED_PROP(float, _ShowNormals)
            UNITY_DOTS_INSTANCING_END(MaterialPropertyMetadata)

            // Replace direct uses with the accessor macros that handle per-instance
            // overrides (and gracefully fall back to material values when not set).
            #define _Ambient        UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _Ambient)
            #define _EnableLighting UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _EnableLighting)
            #define _ShowNormals    UNITY_ACCESS_DOTS_INSTANCED_PROP_WITH_DEFAULT(float, _ShowNormals)
            #endif

            varyings vert(attributes input)
            {
                varyings output;

                // Set up instancing (boilerplate)
                UNITY_SETUP_INSTANCE_ID(input);
                UNITY_TRANSFER_INSTANCE_ID(input, output);

                // Convert object position(3D) to screen position(2D)
                output.positionsCS = TransformObjectToHClip(input.positionOS.xyz);

                // Convert normals to world space (for lighting)
                output.normalWS = TransformObjectToWorldNormal(normalize(input.normalOS));

                // Pass the vertex color through
                output.color = input.color;
                
                return output;
            }

            half4 frag(varyings input) : SV_TARGET
            {
                UNITY_SETUP_INSTANCE_ID(input);

                // now use _Ambient/_EnableLighting/_ShowNormals as usual;
                // they’ll be per-instance if BRG/Entities supplies overrides.
                float3 n = normalize(input.normalWS);
                half3 lit = 1.0h;

                if (_EnableLighting > 0.5)
                {
                    // simple lambert with main light
                    Light mainLight = GetMainLight();
                    lit = saturate(dot(n, mainLight.direction)) * mainLight.color + _Ambient;
                }
                if (_ShowNormals > 0.5)
                    return half4(n * 0.5h + 0.5h, 1);
            
                return half4(lit * input.color.rgb, input.color.a);
                
                // // Normalize the normal vector (ensure correct lenght)
                // half3 normal_ws = normalize(input.normalWS);
                //
                // // Get the main light from the URP pipeline.
                // Light mainLight = GetMainLight();
                // half3 light_dir = mainLight.direction;
                //
                // // Calculate lighting using the main light's color and direction
                // half ndot_L = saturate(dot(normal_ws, light_dir));
                // half3 lighting = _Ambient.xxx + mainLight.color * ndot_L;
                //
                // // Apply lighting to the vertex color
                // half4 final_color = input.color;
                // if (_EnableLighting > 0.5)
                // {
                //     final_color.rgb *= lighting;
                // }
                //
                // // Debug: Show normals as colors
                // #if defined(_SHOW_NORMALS_ON)
                //     return half4(normal_ws * 0.5 + 0.5, 1);
                // #endif
                //
                // return final_color;
            }
            
            ENDHLSL
        }
    }
    Fallback "Hidden/Core/FallbackError" // Backup shader if this fails.
}