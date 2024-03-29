﻿using CacheProvider.Providers;

namespace MockCachingOperation.Process
{
    /// <summary>
    /// Class of the arbitrary payload.
    /// </summary>
    public class Payload : IPayload
    {
        /// <summary>
        /// Unique identifier of payload
        /// </summary>
        public required string Identifier { get; set; }

        /// <summary>
        /// Arbitrary data object: in this case, a list of strings
        /// </summary>
        public required List<string> Data { get; set; }

        /// <summary>
        /// Arbitrary property of payload
        /// </summary>
        public bool Property { get; set; }

        /// <summary>
        /// Arbitrary version number of payload
        /// </summary>
        public decimal Version { get; set; }
    }
}
