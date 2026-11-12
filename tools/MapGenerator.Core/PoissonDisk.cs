using System.Collections.Generic;
using System;

namespace AntFarmer.Gen
{
    public readonly record struct Disk(int X, int Y);

    // Dart throwing (Millington §8.1.4): try candidates in random order and keep each one far enough from those kept.
    public static class PoissonDisk
    {
        // Weights make some candidates likely to be tried earlier (weighted random order: each gets the key u^(1/w)).
        public static List<Disk> Generate(IReadOnlyList<Disk> candidates, int count, int minSpacing, Random rand,
            IReadOnlyList<float>? weights = null)
        {
            if (count <= 0)
                return [];

            var order = new List<(double Key, Disk Disk)>(candidates.Count);
            for (int i = 0; i < candidates.Count; i++)
            {
                double u = rand.NextDouble();
                double key = weights == null ? u : Math.Pow(u, 1.0 / Math.Max(weights[i], 1e-6f));
                order.Add((key, candidates[i]));
            }
            order.Sort((a, b) => b.Key.CompareTo(a.Key));

            var placed = new List<Disk>(Math.Min(count, candidates.Count));
            long minSpacingSquared = minSpacing * minSpacing;

            foreach (var (_, candidate) in order)
            {
                bool tooClose = false;
                foreach (var disk in placed)
                {
                    long dx = candidate.X - disk.X;
                    long dy = candidate.Y - disk.Y;
                    if (dx * dx + dy * dy < minSpacingSquared)
                    {
                        tooClose = true;
                        break;
                    }
                }

                if (!tooClose)
                {
                    placed.Add(candidate);
                    if (placed.Count >= count) break;
                }
            }

            return placed;
        }
    }
}
