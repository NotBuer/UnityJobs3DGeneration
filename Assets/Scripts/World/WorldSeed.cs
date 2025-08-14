using System;
using Unity.Burst;
using Unity.Collections;
using UnityEngine;

namespace World
{
    [BurstCompile]
    public class WorldSeed : MonoBehaviour
    {
        public static WorldSeed Instance { get; private set; }
        
        private const byte SeedLength = 16;
    
        [SerializeField] private string currentSeed;
        [SerializeField] private bool generateRandomSeed;
        
        public ulong CurrentSeedHashCode { get; private set; }

        private void Awake()
        {
            if (Instance == null) Instance = this;
            else DestroyImmediate(gameObject);
            
            if (generateRandomSeed || string.IsNullOrWhiteSpace(currentSeed))
            {
                currentSeed = GenerateRandomStringSeed(SeedLength);
            }
            
            var fixedCurrentSeed = new FixedString64Bytes(currentSeed);
            CurrentSeedHashCode = GetStableHash64(ref fixedCurrentSeed);
        }

        private static string GenerateRandomStringSeed(int lenght)
        {
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[lenght];
        
            var random = new System.Random(DateTime.Now.Millisecond);

            for (var i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }
        
            return new string(stringChars);
        }

        /// <summary>
        /// Computes a stable 64-bit hash for the provided unmanaged fixed-width string.
        /// This function is Burst-compatible.
        /// </summary>
        /// <typeparam name="T">Any type that implements INativeList<byte> and IUTF8Bytes, such as FixedString32Bytes.</typeparam>
        /// <param name="input">The input string for which the hash is generated.</param>
        /// <returns>A stable 64-bit hash representing the input string.</returns>
        [BurstCompile]
        public static unsafe ulong GetStableHash64<T>(ref T input) where T : unmanaged, INativeList<byte>, IUTF8Bytes
        {
            // A FixedString can't be null, so we only check if it's empty.
            if (input.Length == 0) return 0;

            // Directly get the pointer and length from the FixedString's unmanaged buffer.
            // The 'Length' property is the number of bytes in the UTF-8 sequence.
            var hashComponents = xxHash3.Hash64(input.GetUnsafePtr(), input.Length);
    
            // Combine the two 32-bit hash components into a single 64-bit ulong.
            return ((ulong)hashComponents.y << 32) | hashComponents.x;
        }
    }
}

