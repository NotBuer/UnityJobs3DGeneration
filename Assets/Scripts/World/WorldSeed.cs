using System;
using System.Runtime.CompilerServices;
using Unity.Collections;
using Unity.Collections.LowLevel.Unsafe;
using UnityEngine;

namespace World
{
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
            
            CurrentSeedHashCode = GetStableHash64(currentSeed);
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
        /// Computes a stable 64-bit hash for the provided input string.
        /// </summary>
        /// <param name="input">The input string for which the hash is generated.</param>
        /// <returns>A stable 64-bit hash representing the input string.</returns>
        public static unsafe ulong GetStableHash64(string input)
        {
            if (string.IsNullOrEmpty(input)) return 0;
            
            var bytes = new NativeArray<byte>(System.Text.Encoding.UTF8.GetBytes(input), Allocator.Temp);

            var hashComponents = xxHash3.Hash64(bytes.GetUnsafeReadOnlyPtr(), bytes.Length);
            
            bytes.Dispose();
            
            // Bit shifts it left by 32 bits, placing it in the upper half of the 64-bit space,
            // and Bitwise OR combines the shifted high bits with the low 32 bits.
            return (ulong)hashComponents.y << 32 | hashComponents.x;
        }
    }
}

