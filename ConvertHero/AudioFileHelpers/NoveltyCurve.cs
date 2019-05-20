﻿using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace ConvertHero.AudioFileHelpers
{
    public enum WeightType
    {
        Flat,
        Triangle,
        InverseTriangle,
        Parabola,
        InverseParabola,
        Linear,
        Quadratic,
        InverseQuadratic,
        Hybrid
    }
    public class NoveltyCurve
    {
        private WeightType type;

        private bool normalize;

        private int frameRate;

        private int meanSize;

        private float[] weightCurve = null;

        public NoveltyCurve(WeightType type, int frameRate, bool normalize)
        {
            this.type = type;
            this.frameRate = frameRate;
            this.normalize = normalize;
            this.meanSize = (int)Math.Max(2, 0.1 * this.frameRate);
        }

        public float[] ComputeAll(List<float[]> frames)
        {
            int nFrames = frames.Count;
            int nBands = frames[0].Length;
            float[] novelty = new float[nFrames];
            

            // compute novelty for each sub-band
            List<float[]> frequencies = MathHelpers.Transpose(frames);
            List<float[]> noveltyBands = new List<float[]>();
            for(int bandIndex = 0; bandIndex < nBands; bandIndex++)
            {
                noveltyBands.Add(NoveltyFunction(frequencies[bandIndex], 1000, this.meanSize));
            }

            noveltyBands = MathHelpers.Transpose(noveltyBands);

            if(this.type == WeightType.Hybrid)
            {
                float[] aweights = GetWeightCurve(nBands, WeightType.Flat);
                float[] bweights = GetWeightCurve(nBands, WeightType.Quadratic);
                float[] cweights = GetWeightCurve(nBands, WeightType.Linear);
                float[] dweights = GetWeightCurve(nBands, WeightType.InverseQuadratic);

                float[] bnovelty = new float[nFrames];
                float[] cnovelty = new float[nFrames];
                float[] dnovelty = new float[nFrames];

                for(int frame = 0; frame < nFrames; frame++)
                {
                    for(int band = 0; band < nBands; band++)
                    {
                        novelty[frame] += aweights[band] * noveltyBands[frame][band];
                        bnovelty[frame] += bweights[band] * noveltyBands[frame][band];
                        cnovelty[frame] += cweights[band] * noveltyBands[frame][band];
                        dnovelty[frame] += dweights[band] * noveltyBands[frame][band];
                    }
                }

                for(int frame = 0; frame < nFrames; frame++)
                {
                    novelty[frame] *= bnovelty[frame];
                    novelty[frame] *= cnovelty[frame];
                    novelty[frame] *= dnovelty[frame];
                }
            }
            else
            {
                if(this.weightCurve == null)
                {
                    this.weightCurve = GetWeightCurve(nBands, this.type);
                }

                for (int frame = 0; frame < nFrames; frame++)
                {
                    for (int band = 0; band < nBands; band++)
                    {
                        novelty[frame] += this.weightCurve[band] * noveltyBands[frame][band];
                    }
                }
            }

            // Return the moving average of the novely
            MovingAverage av = new MovingAverage(meanSize);
            return av.Compute(novelty);
        }

        private float[] GetWeightCurve(int size, WeightType type)
        {
            float[] result = new float[size];
            int halfSize = size / 2;
            int squareHalfSize = halfSize * halfSize;
            int squareSize = size * size;

            switch(type)
            {
                case WeightType.Triangle:
                    for (int i = 0; i < halfSize; i++)
                    {
                        result[i] = result[size - 1 - i] = i + 1;
                    }

                    // handle single peak on odd sized triangles
                    if(size % 2 == 1)
                    {
                        result[halfSize] = size / 2;
                    }

                    break;
                case WeightType.InverseTriangle:
                    for (int i = 0; i < halfSize; i++)
                    {
                        result[i] = result[size - 1 - i] = halfSize - i;
                    }

                    break;
                case WeightType.Parabola:
                    for (int i = 0; i < halfSize; i++)
                    {
                        result[i] = result[size - 1 - i] = (halfSize - i) * (halfSize - i);
                    }
                    break;
                case WeightType.InverseParabola:
                    for (int i = 0; i < halfSize; i++)
                    {
                        result[i] = result[size - 1 - i] = squareHalfSize - (halfSize - i) * (halfSize - i) + 1;
                    }

                    if (size % 2 == 1)
                    {
                        result[halfSize] = squareHalfSize;
                    }
                    break;
                case WeightType.Linear:
                    for (int i = 0; i < size; i++)
                    {
                        result[i] = i + 1;
                    }
                    break;
                case WeightType.Quadratic:
                    for (int i = 0; i < size; i++)
                    {
                        result[i] = (i * i) + 1;
                    }
                    break;
                case WeightType.InverseQuadratic:
                    for (int i = 0; i < size; i++)
                    {
                        result[i] = squareSize - (i * i);
                    }
                    break;
                case WeightType.Flat:
                default:
                    for(int i = 0; i < size; i++)
                    {
                        result[i] = 1.0f;
                    }
                    break;
            }

            return result;
        }

        private float[] NoveltyFunction(float[] input, int scalar, int meanSize)
        {
            int size = input.Length;
            int dsize = size - 1;

            float[] logInput = new float[size];
            float[] novelty = new float[size];
            for(int i = 0; i < size; i++)
            {
                logInput[i] = (float)Math.Log10(1 + scalar * input[i]);
            }

            // Differentiate the input vector and keep only the positive changes
            for(int i = 1; i < size; i++)
            {
                float d = logInput[i] - logInput[i - 1];
                if(d > 0)
                {
                    novelty[i - 1] = d;
                }
            }

            // Subtract local mean
            for(int i = 0; i < dsize; i++)
            {
                int start = i - meanSize / 2;
                int end = i + meanSize / 2;
                start = Math.Max(0, start);
                end = Math.Min(dsize, end);

                float m = 0f;
                for(int j = start; j < end; j++)
                {
                    m += novelty[j];
                }
                m /= Math.Max(1,(end - start));

                if (novelty[i] < m)
                {
                    novelty[i] = 0;
                }
                else
                {
                    novelty[i] -= m;
                }
            }

            if (this.normalize)
            {
                float max = novelty.Max();
                if(max != 0)
                {
                    for(int i = 0; i < novelty.Length; i++)
                    {
                        novelty[i] /= max;
                    }
                }
            }

            MovingAverage av = new MovingAverage(meanSize);
            float[] result = av.Compute(novelty);
            return result;
        }
    }
}
