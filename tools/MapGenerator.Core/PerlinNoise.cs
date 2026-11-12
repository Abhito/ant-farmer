using System;
using System.Collections.Generic;

namespace AntFarmer.Gen
{
    // One layer of gradient noise (Millington §8.3.3). A shuffled table hashes each lattice point to a gradient,
    // so the pattern only repeats every 256 lattice cells, far beyond any map.
    public class PerlinOctave
    {
        private const int TableSize = 256;

        private readonly int[] permutation = new int[TableSize * 2];
        private readonly float[] gradientX = new float[TableSize];
        private readonly float[] gradientY = new float[TableSize];

        public PerlinOctave(Random rand)
        {
            for (int i = 0; i < TableSize; i++)
            {
                float angle = rand.NextSingle() * MathF.Tau;
                gradientX[i] = MathF.Cos(angle);
                gradientY[i] = MathF.Sin(angle);
                permutation[i] = i;
            }

            for (int i = TableSize - 1; i > 0; i--)
            {
                int swap = rand.Next(i + 1);
                (permutation[i], permutation[swap]) = (permutation[swap], permutation[i]);
            }

            for (int i = 0; i < TableSize; i++)
                permutation[TableSize + i] = permutation[i];
        }

        // Roughly -0.7 to 0.7. Changes over about one unit of x and y.
        public float Get(float x, float y)
        {
            int x0 = (int)MathF.Floor(x);
            int y0 = (int)MathF.Floor(y);
            float fx = x - x0;
            float fy = y - y0;

            float top = Lerp(Corner(x0, y0, fx, fy), Corner(x0 + 1, y0, fx - 1, fy), Fade(fx));
            float bottom = Lerp(Corner(x0, y0 + 1, fx, fy - 1), Corner(x0 + 1, y0 + 1, fx - 1, fy - 1), Fade(fx));
            return Lerp(top, bottom, Fade(fy));
        }

        private float Corner(int ix, int iy, float dx, float dy)
        {
            int g = permutation[permutation[ix & (TableSize - 1)] + (iy & (TableSize - 1))];
            return dx * gradientX[g] + dy * gradientY[g];
        }

        // Smooth blend so lattice lines don't show as creases.
        private static float Fade(float t) => t * t * t * (t * (t * 6f - 15f) + 10f);

        private static float Lerp(float a, float b, float t) => a + (b - a) * t;
    }

    // Octaves summed from coarse to fine: the first weight is the base scale, each next one has twice the frequency.
    public class PerlinNoise
    {
        readonly List<PerlinOctave> octaves;
        readonly float[] weights;

        public PerlinNoise(float[] weights, Random rand)
        {
            this.weights = weights;
            octaves = [];
            foreach (float _ in weights)
                octaves.Add(new PerlinOctave(rand));
        }

        public float Get(float x, float y)
        {
            float sum = 0;
            float frequency = 1f;
            for (int i = 0; i < octaves.Count; i++)
            {
                sum += octaves[i].Get(x * frequency, y * frequency) * weights[i];
                frequency *= 2f;
            }
            return sum;
        }
    }
}
