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
            #pragma vertex vert
            #pragma fragment frag
            #pragma multi_compile_instancing
            #pragma multi_compile_fog

            // URP Core and Lighting libraries
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Core.hlsl"
            #include "Packages/com.unity.render-pipelines.universal/ShaderLibrary/Lighting.hlsl"

            struct attributes // "What the mesh gives us"
            {
                float4 positionOS    : POSITION;  // Object-space position
                float3 normalOS      : NORMAL;    // Object-space normal (surface direction)
                half4 color          : COLOR;     // Vertex color
                UNITY_VERTEX_INPUT_INSTANCE_ID    // For instancing
            };

            struct varyings // "What we calculate and pass to the pixel shader"
            {
                float4 positionsCS  : POSITION;   // Screen position
                half3 normalWS      : TEXCOORD0;  // World-space normal
                half4 color         : COLOR;      // Pass-through color
                UNITY_VERTEX_INPUT_INSTANCE_ID    // For GPU instancing
            };

            CBUFFER_START(UnityPerMaterial)
                half _Ambient;
                half _EnableLighting; 
            CBUFFER_END

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
                
                // Normalize the normal vector (ensure correct lenght)
                half3 normal_ws = normalize(input.normalWS);

                // Get the main light from the URP pipeline.
                Light mainLight = GetMainLight();
                half3 light_dir = mainLight.direction;

                // Calculate lighting using the main light's color and direction
                half ndot_L = saturate(dot(normal_ws, light_dir));
                half3 lighting = _Ambient.xxx + mainLight.color * ndot_L;

                // Apply lighting to the vertex color
                half4 final_color = input.color;
                if (_EnableLighting > 0.5)
                {
                    final_color.rgb *= lighting;
                }

                // Debug: Show normals as colors
                #if defined(_SHOW_NORMALS_ON)
                    return half4(normal_ws * 0.5 + 0.5, 1);
                #endif

                return final_color;
            }
            
            ENDHLSL
        }
    }
    Fallback "Hidden/Core/FallbackError" // Backup shader if this fails.
}