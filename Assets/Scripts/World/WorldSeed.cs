using ECS.Utils;
using Unity.Burst;
using UnityEngine;

namespace World
{
    [BurstCompile]
    public class WorldSeed : MonoBehaviour
    {
        public static WorldSeed Instance { get; private set; }
    
        [SerializeField] private string seed;
        [SerializeField] private bool useRandomSeed;
        
        public ulong CurrentSeedHashCode { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else DestroyImmediate(gameObject);

            CurrentSeedHashCode = WorldSeedUtils.GetSeedHash(useRandomSeed ? string.Empty : seed);
        }
    }
}

