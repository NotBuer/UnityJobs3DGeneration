using Unity.Entities;
using Unity.Mathematics;
using UnityEngine;

namespace ECS
{
    public class PlayerAuthoring : MonoBehaviour
    {
        public float3 startingPosition;

        private class PlayerBaker : Baker<PlayerAuthoring>
        {
            public override void Bake(PlayerAuthoring authoring)
            {
                var entity = GetEntity(TransformUsageFlags.Dynamic);
                
                AddComponent(entity, new PlayerPosition
                {
                    Value = authoring.startingPosition
                });
            }
        }
    }
}