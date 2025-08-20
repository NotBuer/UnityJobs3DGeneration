using ECS.Components;
using Unity.Burst;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;

namespace ECS.System
{
    [BurstCompile]
    [UpdateInGroup(typeof(SimulationSystemGroup))]
    public partial struct PlayerMovementSystem : ISystem
    {
        [BurstCompile]
        public void OnCreate(ref SystemState state)
        {
            state.RequireForUpdate<PlayerInput>();
            state.RequireForUpdate<EcsCameraFollow>();
        }
        
        [BurstCompile]
        public void OnDestroy(ref SystemState state) { }

        [BurstCompile]
        public void OnUpdate(ref SystemState state)
        {
            var deltaTime = SystemAPI.Time.DeltaTime;

            foreach (var (transform, position, 
                          stats, input, 
                          settings, playerEntity)
                     in SystemAPI.Query<
                             RefRW<LocalTransform>, RefRW<PlayerPosition>, 
                             RefRO<PlayerStats>, RefRO<PlayerInput>, 
                             RefRO<PlayerSettings>
                         >()
                         .WithNone<EcsCameraFollow>().WithEntityAccess())
            {
                // -- YAW ROTATION (Player) --
                // Rotate the player transforms around the Y-axis for horizontal looking.
                transform.ValueRW = transform.ValueRW.RotateY(
                    input.ValueRO.Look.x * stats.ValueRO.LookSpeed * settings.ValueRO.LookSensitivityX * deltaTime);
                
                foreach (var (camera, follow, cameraTransform) 
                         in SystemAPI.Query<RefRW<EcsCamera>, RefRO<EcsCameraFollow>, RefRW<LocalTransform>>())
                {
                    if (follow.ValueRO.Target != playerEntity) continue;
                
                    // -- PITCH ROTATION (Camera) --
                    camera.ValueRW.Pitch -= input.ValueRO.Look.y * settings.ValueRO.LookSensitivityY * stats.ValueRO.LookSpeed;
                    camera.ValueRW.Pitch = math.clamp(camera.ValueRO.Pitch, -90f, 90f);
                
                    // -- COMBINE ROTATIONS & SET TRANSFORM --
                    var playerYaw = transform.ValueRO.Rotation;
                    var cameraPitch = quaternion.RotateX(math.radians(camera.ValueRO.Pitch));
                    var combinedRotation = math.mul(playerYaw, cameraPitch);
                
                    cameraTransform.ValueRW.Rotation = combinedRotation;
                    cameraTransform.ValueRW.Position = transform.ValueRO.Position + math.mul(combinedRotation, camera.ValueRO.Offset);
                    
                    // -- MOVEMENT (Player) --
                    var moveInput = input.ValueRO.Move;
                    if (math.lengthsq(moveInput) > 0f)
                    {
                        // Get the camera forward and right vectors from its final rotation.
                        var forward = math.forward(combinedRotation);
                        var right = math.cross(math.up(), forward);
                        
                        var speed = stats.ValueRO.MovementSpeed;
                        if (input.ValueRO.Run) speed *= 2;
                    
                        // Calculate a movement direction based on camera orientation.
                        var moveDirection = math.normalize(right * moveInput.x + forward * moveInput.y);
                        transform.ValueRW.Position += moveDirection * speed * deltaTime;
                    }

                    position.ValueRW.Value = transform.ValueRO.Position;
                    
                    break;
                }

                position.ValueRW.Value = transform.ValueRO.Position;
            }
            
            // TODO: Create jump logic later on... (entities physics? simulated physics?)
        }
    }
}