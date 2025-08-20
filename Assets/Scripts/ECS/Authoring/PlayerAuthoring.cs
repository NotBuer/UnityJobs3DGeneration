using ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using Unity.Transforms;
using UnityEngine;

namespace ECS.Authoring
{
    public class PlayerAuthoring : MonoBehaviour
    {
        [Header("Position Settings")]
        [SerializeField] private float3 position;
        
        [Header("Stats Settings")]
        [Range(0, 0XFF)] [SerializeField] private byte movementSpeed;
        [Range(0, 0XFF)] [SerializeField] private byte lookSpeed;
        
        [Header("Camera Settings")]
        [SerializeField] private float3 offset;
        [SerializeField] private float near;
        [SerializeField] private float far;

        [Header("Player Settings")] 
        [Range(0f, 0.05f)] [SerializeField] private float lookSensitivityX;
        [Range(0f, 0.05f)] [SerializeField] private float lookSensitivityY;
        [SerializeField] private float fov;
        [Range(2, 32)] [SerializeField] private byte renderDistance = 2;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PlayerPosition
                {
                    Value = authoring.position
                });
                AddComponent(entity, new PlayerInput
                {
                    Move = float2.zero,
                    Look = float2.zero,
                    Jump = false
                });
                AddComponent(entity, new PlayerStats
                {
                    MovementSpeed = authoring.movementSpeed,
                    LookSpeed = authoring.lookSpeed
                });
                AddComponent(entity, new PlayerSettings
                {
                    LookSensitivityX = authoring.lookSensitivityX,
                    LookSensitivityY = authoring.lookSensitivityY
                });

                var cameraEntity = CreateAdditionalEntity(TransformUsageFlags.Dynamic);
                AddComponent(cameraEntity, LocalTransform.FromPositionRotationScale(float3.zero, quaternion.identity, 1f));
                AddComponent(cameraEntity, new EcsCamera
                {
                    Offset = authoring.offset,
                    Pitch = 0f,
                    Near = authoring.near,
                    Far = authoring.far,
                    FoV = authoring.fov
                });
                AddComponent(cameraEntity, new EcsCameraFollow
                {
                    Target = entity
                });
            }
        }
    }
}