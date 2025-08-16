using Unity.Collections;

namespace ECS.Utils
{
    public static class WorldSeedUtils
    {
        private const byte SeedLength = 16;

        /// <summary>
        /// Generates a 64-bit stable hash from the provided seed string.
        /// If the input seed is null or whitespace, a random seed is generated before hashing.
        /// </summary>
        /// <param name="seedToHash">The seed string to hash. If null or empty, a random seed is generated.</param>
        /// <returns>A 64-bit unsigned integer representing the stable hash of the seed.</returns>
        public static ulong GetSeedHash(string seedToHash)
        {
            if (string.IsNullOrWhiteSpace(seedToHash))
                seedToHash = StringHashingUtils.GenerateRandomString(SeedLength);

            var seedHashedFixed64Bytes = new FixedString64Bytes(seedToHash);
            return StringHashingUtils.GetStableHash64(ref seedHashedFixed64Bytes);
        }    
    }
}