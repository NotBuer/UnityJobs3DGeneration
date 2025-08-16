using ECS.Components;
using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS.Authoring
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float3 startingPosition;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(authoring, TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PlayerPosition
                {
                    Value = authoring.startingPosition
                });
            }
        }
    }
}