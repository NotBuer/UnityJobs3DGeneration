using ECS.Components;
using ECS.Utils;
using Unity.Entities;
using UnityEngine;

namespace ECS.Authoring
{
        public class WorldConfigAuthoring : MonoBehaviour
        {
            [Header("Chunk Size Settings")]
            [Range(1, 32)] [SerializeField] private byte chunkSize = 16;
            [Range(1, 0XFF)] [SerializeField] private byte chunkSizeY = 255;
            
            // TODO: Segregate render distance from world config, to user settings component.
            [Header("Render Distance Settings")]
            [Range(2, 16)] [SerializeField] private byte renderDistance = 2;
            
            [Header("World Generation Settings")]
            [SerializeField] private float frequency = 0.01f;
            [SerializeField] private float amplitude = 32f;
            
            [Header("World Seed Settings")]
            [SerializeField] private string seed;
            [SerializeField] private bool useRandomSeed;
            
            private class WorldConfigBaker : Baker<WorldConfigAuthoring>
            {
                public override void Bake(WorldConfigAuthoring authoring)
                {
                    var entity = GetEntity(authoring, TransformUsageFlags.None);
                    
                    AddComponent(entity, new WorldConfiguration
                    {
                        ChunkSize = authoring.chunkSize,
                        ChunkSizeY = authoring.chunkSizeY,
                        Frequency = authoring.frequency,
                        Amplitude = authoring.amplitude,
                        Seed = WorldSeedUtils.GetSeedHash(authoring.useRandomSeed ? string.Empty : authoring.seed)
                    });
                }
            }
        }
}