using System;
using System.Security.Cryptography;
using System.Text;
using LivingWorld.Simulation.Core;

namespace LivingWorld.Simulation.Persistence
{
    /// <summary>
    /// Canonical digest of a whole world state: the SHA-256 of the deterministic save
    /// document <see cref="WorldSaver"/> writes. Two worlds with equal digests serialize
    /// to byte-identical documents, so equal digests prove identical future evolution
    /// (same clock, RNG stream, events, NPCs, knowledge, economy and travel).
    /// </summary>
    public static class WorldDigest
    {
        /// <summary>Computes the canonical digest. Throws <see cref="SaveException"/> for unsavable state.</summary>
        public static string Compute(WorldState state)
        {
            if (state == null) throw new ArgumentNullException(nameof(state));
            // WorldSaver.Save is deterministic: the same state always yields byte-identical
            // JSON, so hashing that document is hashing the canonical state itself.
            byte[] document = Encoding.UTF8.GetBytes(WorldSaver.Save(state));
            using (SHA256 sha = SHA256.Create())
            {
                byte[] hash = sha.ComputeHash(document);
                var text = new StringBuilder(hash.Length * 2);
                foreach (byte b in hash) text.Append(b.ToString("x2"));
                return text.ToString();
            }
        }
    }
}
