using ECS.Components;
using Unity.Burst;

namespace ECS.World
{
    [BurstCompile]
    public static class Settings
    {
        public struct WorldConfigKey { }
        
        public static readonly SharedStatic<WorldConfiguration> World = 
            SharedStatic<WorldConfiguration>.GetOrCreate<WorldConfigKey>();

        public static void Reset()
        {
            World.Data = default;
        }
    }
}