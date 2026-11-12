using System.Collections.Generic;
using System;

namespace AntFarmer.Gen
{
    public readonly record struct Disk(int X, int Y);

    public static class PoissonDisk
    {
        public static List<Disk> Generate(IReadOnlyList<Disk> candidates, int count, int minSpacing, Random rand)
        {
            if (count <= 0)
                return [];

            var shuffled = new List<Disk>(candidates);
            for (int i = 0; i < shuffled.Count; i++)
            {
                int swapIndex = rand.Next(i, shuffled.Count);
                (shuffled[i], shuffled[swapIndex]) = (shuffled[swapIndex], shuffled[i]);
            }

            var placed = new List<Disk>(Math.Min(count, shuffled.Count));
            long minSpacingSquared = minSpacing * minSpacing;

            foreach (var candidate in shuffled)
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
