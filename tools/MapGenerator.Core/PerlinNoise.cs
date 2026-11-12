using System;
using System.Collections.Generic;

namespace AntFarmer.Gen
{
    public class PerlinOctave
    {
        readonly float[][][] gradient;
        readonly int size;

        public PerlinOctave(int size, Random rand)
        {
            this.size = size;
            gradient = new float[size + 1][][];
            for (int i = 0; i < size + 1; i++)
            {
                gradient[i] = new float[size + 1][];
                for (int j = 0; j < size + 1; j++)
                {
                    gradient[i][j] = new float[2];
                    gradient[i][j][0] = rand.NextSingle() * 2 - 1;
                    gradient[i][j][1] = rand.NextSingle() * 2 - 1;
                }
            }
        }

        public float ScaledHeight(int ix, int iy, float x, float y)
        {
            int gx = ((ix % (size + 1)) + size + 1) % (size + 1);
            int gy = ((iy % (size + 1)) + size + 1) % (size + 1);

            float dx = x - ix, dy = y - iy;
            return dx * gradient[gx][gy][0] + dy * gradient[gx][gy][1];
        }

        public float Get(float x, float y)
        {
            float scaledX = x / size;
            float scaledY = y / size;

            int ix = (int)scaledX;
            int iy = (int)scaledY;

            float px = scaledX - ix;
            float py = scaledY - iy;

            float tl = ScaledHeight(ix, iy, scaledX, scaledY);
            float tr = ScaledHeight(ix + 1, iy, scaledX, scaledY);
            float t = Single.Lerp(tl, tr, px);

            float bl = ScaledHeight(ix, iy + 1, scaledX, scaledY);
            float br = ScaledHeight(ix + 1, iy + 1, scaledX, scaledY);
            float b = Single.Lerp(bl, br, px);

            return Single.Lerp(t, b, py);
        }
    }

    public class PerlinNoise
    {
        readonly List<PerlinOctave> octaves;
        readonly float[] weights;

        public PerlinNoise(float[] weights, Random rand)
        {
            this.weights = weights;
            octaves = [];
            int size = 1;
            foreach(float weight in weights)
            {
                octaves.Add(new PerlinOctave(size, rand));
                size *= 2;
            }
        }

        public float Get(float x, float y)
        {
            float sum = 0;
            for(int i = 0; i < octaves.Count; i++)
            {
                sum += octaves[i].Get(x, y) * weights[i];
            }
            return sum;
        }
    }
}
