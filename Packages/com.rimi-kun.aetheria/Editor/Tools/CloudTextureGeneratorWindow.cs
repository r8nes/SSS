#if UNITY_EDITOR
using System.IO;
using System.Threading.Tasks;
using UnityEditor;
using UnityEngine;
using UnityEngine.Rendering;

namespace Aetheria.Editor
{
    public sealed class CloudTextureGeneratorWindow : EditorWindow
    {
        private const int ShapeResolution = 128;
        private const int DetailResolution = 32;

        private const int ShapePerlinBaseFrequency = 4;
        private const int ShapePerlinOctaves = 4;

        private const float NoiseWeight0 = 0.625f;
        private const float NoiseWeight1 = 0.25f;
        private const float NoiseWeight2 = 0.125f;

        private static readonly float[] GradientX =
            { 1f, -1f, 1f, -1f, 1f, -1f, 1f, -1f, 0f, 0f, 0f, 0f };

        private static readonly float[] GradientY =
            { 1f, 1f, -1f, -1f, 0f, 0f, 0f, 0f, 1f, -1f, 1f, -1f };

        private static readonly float[] GradientZ =
            { 0f, 0f, 0f, 0f, 1f, 1f, -1f, -1f, 1f, 1f, -1f, -1f };

        [MenuItem("Tools/Aetheria/Generate Cloud Volume Noise")]
        private static void Open()
        {
            GetWindow<CloudTextureGeneratorWindow>("Cloud Volume Noise").Show();
        }

        private void OnGUI()
        {
            EditorGUILayout.LabelField("Aetheria Cloud Volume Noise", EditorStyles.boldLabel);
            EditorGUILayout.Space(4f);
            EditorGUILayout.HelpBox(
                "Generates two unassigned Texture3D assets in the project Assets folder.\n\n" +
                "CloudShapeNoise.asset — 128³ R8 UNorm\n" +
                "R: final decoded low-frequency cloud shape\n\n" +
                "CloudDetailNoise.asset — 32³ R8 UNorm\n" +
                "R: precombined detail Worley fBm\n\n" +
                "After generation, assign both assets manually in AetheriaResources.",
                MessageType.Info);

            EditorGUILayout.LabelField("Output", GetOutputPath());
            EditorGUILayout.Space(8f);

            if (GUILayout.Button("Generate Cloud Texture3D Assets", GUILayout.Height(40f)))
                Generate();
        }

        private void Generate()
        {
            string outputPath = GetOutputPath();
            EnsureFolder(outputPath);

            try
            {
                EditorUtility.DisplayProgressBar(
                    "Aetheria Cloud Volume Noise",
                    "Generating CloudShapeNoise.asset",
                    0f);
                byte[][] shapeMipChain = BuildShapeMipChain();
                BuildShapeTexture(outputPath, shapeMipChain);

                EditorUtility.DisplayProgressBar(
                    "Aetheria Cloud Volume Noise",
                    "Generating CloudDetailNoise.asset",
                    0.9f);
                BuildDetailTexture(outputPath);

                AssetDatabase.SaveAssets();
                AssetDatabase.Refresh();
            }
            finally
            {
                EditorUtility.ClearProgressBar();
            }

            EditorUtility.DisplayDialog(
                "Aetheria Cloud Volume Noise",
                "Generated:\n" +
                $"{outputPath}/CloudShapeNoise.asset\n" +
                $"{outputPath}/CloudDetailNoise.asset\n\n" +
                "Assign both assets in AetheriaResources.",
                "OK");
        }

        private static void BuildShapeTexture(string outputPath, byte[][] mipChain)
        {
            var texture = CreateTexture3D(
                ShapeResolution,
                TextureFormat.R8,
                "CloudShapeNoise",
                FilterMode.Trilinear);

            for (int mipLevel = 0; mipLevel < mipChain.Length; ++mipLevel)
                texture.SetPixelData(mipChain[mipLevel], mipLevel);

            texture.Apply(updateMipmaps: false, makeNoLongerReadable: true);
            SaveTexture3D(outputPath, texture, "CloudShapeNoise");
        }

        private static void BuildDetailTexture(string outputPath)
        {
            const int resolution = DetailResolution;
            var pixels = new byte[resolution * resolution * resolution];

            Parallel.For(0, resolution, z =>
            {
                int sliceOffset = z * resolution * resolution;
                for (int y = 0; y < resolution; y++)
                {
                    int rowOffset = sliceOffset + y * resolution;
                    for (int x = 0; x < resolution; x++)
                    {
                        float u = (x + 0.5f) / resolution;
                        float v = (y + 0.5f) / resolution;
                        float w = (z + 0.5f) / resolution;

                        float worley2 = Worley3D(u, v, w, 2);
                        float worley4 = Worley3D(u, v, w, 4);
                        float worley8 = Worley3D(u, v, w, 8);
                        float worley16 = Worley3D(u, v, w, 16);

                        byte octave0 = Quantize(Fbm3(worley2, worley4, worley8));
                        byte octave1 = Quantize(Fbm3(worley4, worley8, worley16));
                        byte octave2 = Quantize(Fbm2(worley8, worley16));
                        pixels[rowOffset + x] = Quantize(CombineQuantizedNoise(
                            octave0,
                            octave1,
                            octave2));
                    }
                }
            });

            var texture = CreateTexture3D(
                resolution,
                TextureFormat.R8,
                "CloudDetailNoise",
                FilterMode.Trilinear);
            texture.SetPixelData(pixels, 0);
            texture.Apply(updateMipmaps: true, makeNoLongerReadable: true);
            SaveTexture3D(outputPath, texture, "CloudDetailNoise");
        }

        private static byte[][] BuildShapeMipChain()
        {
            byte[] basePixels = BuildShapePixels();
            int mipCount = CoreUtils.GetMipCount(ShapeResolution);
            var mipChain = new byte[mipCount][];
            mipChain[0] = basePixels;

            int sourceResolution = ShapeResolution;
            for (int mipLevel = 1; mipLevel < mipCount; ++mipLevel)
            {
                mipChain[mipLevel] = BuildAverageMip(mipChain[mipLevel - 1], sourceResolution);
                sourceResolution >>= 1;
            }

            return mipChain;
        }

        private static byte[] BuildShapePixels()
        {
            const int resolution = ShapeResolution;
            var pixels = new byte[resolution * resolution * resolution];

            Parallel.For(0, resolution, z =>
            {
                int sliceOffset = z * resolution * resolution;
                for (int y = 0; y < resolution; y++)
                {
                    int rowOffset = sliceOffset + y * resolution;
                    for (int x = 0; x < resolution; x++)
                    {
                        float u = (x + 0.5f) / resolution;
                        float v = (y + 0.5f) / resolution;
                        float w = (z + 0.5f) / resolution;
                        float worley4 = Worley3D(u, v, w, 4);
                        float worley8 = Worley3D(u, v, w, 8);
                        float worley16 = Worley3D(u, v, w, 16);
                        float worley32 = Worley3D(u, v, w, 32);
                        float worley64 = Worley3D(u, v, w, 64);
                        float perlinFbm = PerlinFbm(u, v, w, ShapePerlinOctaves, ShapePerlinBaseFrequency);
                        float perlinWorley = RemapUnclamped(
                            perlinFbm, 0f, 1f, Fbm3(worley4, worley8, worley16), 1f);

                        byte worleyOctave0 = Quantize(Fbm3(worley8, worley16, worley32));
                        byte worleyOctave1 = Quantize(Fbm3(worley16, worley32, worley64));
                        byte worleyOctave2 = Quantize(Fbm2(worley32, worley64));
                        byte quantizedPerlinWorley = Quantize(perlinWorley);
                        byte quantizedLowFrequencyWorley = Quantize(CombineQuantizedNoise(
                            worleyOctave0,
                            worleyOctave1,
                            worleyOctave2));
                        pixels[rowOffset + x] = Quantize(DecodeCloudShapeNoise(
                            quantizedPerlinWorley,
                            quantizedLowFrequencyWorley));
                    }
                }
            });

            return pixels;
        }

        private static byte[] BuildAverageMip(byte[] source, int sourceResolution)
        {
            int destinationResolution = sourceResolution >> 1;
            var destination = new byte[destinationResolution * destinationResolution * destinationResolution];
            int sourceSliceSize = sourceResolution * sourceResolution;
            int destinationSliceSize = destinationResolution * destinationResolution;

            Parallel.For(0, destinationResolution, z =>
            {
                int sourceZ = z << 1;
                for (int y = 0; y < destinationResolution; y++)
                {
                    int sourceY = y << 1;
                    for (int x = 0; x < destinationResolution; x++)
                    {
                        int sourceX = x << 1;
                        int sum = 0;
                        for (int dz = 0; dz <= 1; dz++)
                        for (int dy = 0; dy <= 1; dy++)
                        for (int dx = 0; dx <= 1; dx++)
                        {
                            int sourceIndex = (sourceZ + dz) * sourceSliceSize
                                + (sourceY + dy) * sourceResolution + sourceX + dx;
                            sum += source[sourceIndex];
                        }

                        destination[z * destinationSliceSize + y * destinationResolution + x] =
                            (byte)((sum + 4) >> 3);
                    }
                }
            });

            return destination;
        }

        private static float DecodeCloudShapeNoise(byte perlinWorleyByte, byte lowFrequencyWorleyByte)
        {
            const float inverseByteMax = 1f / byte.MaxValue;
            float perlinWorley = perlinWorleyByte * inverseByteMax;
            float lowFrequencyWorley = lowFrequencyWorleyByte * inverseByteMax;
            return Mathf.Clamp01((perlinWorley + 1f - lowFrequencyWorley) / (2f - lowFrequencyWorley));
        }

        private static Texture3D CreateTexture3D(
            int resolution,
            TextureFormat format,
            string textureName,
            FilterMode filterMode)
        {
            if (!SystemInfo.SupportsTextureFormat(format))
                throw new System.NotSupportedException($"Texture format {format} is not supported on this editor platform.");

            return new Texture3D(resolution, resolution, resolution, format, true)
            {
                name = textureName,
                wrapModeU = TextureWrapMode.Repeat,
                wrapModeV = TextureWrapMode.Repeat,
                wrapModeW = TextureWrapMode.Repeat,
                filterMode = filterMode,
                anisoLevel = 1
            };
        }

        private static void SaveTexture3D(string outputPath, Texture3D generatedTexture, string assetName)
        {
            string assetPath = $"{outputPath}/{assetName}.asset";
            var existingObject = AssetDatabase.LoadMainAssetAtPath(assetPath);
            if (existingObject && !(existingObject is Texture3D))
            {
                CoreUtils.Destroy(generatedTexture);
                throw new IOException($"Asset at {assetPath} is not a Texture3D.");
            }

            var existingTexture = existingObject as Texture3D;
            if (existingTexture)
            {
                EditorUtility.CopySerialized(generatedTexture, existingTexture);
                CoreUtils.Destroy(generatedTexture);
                EditorUtility.SetDirty(existingTexture);
            }
            else
            {
                AssetDatabase.CreateAsset(generatedTexture, assetPath);
            }
        }

        // These are the former runtime decode weights. Packing their result here
        // removes the dot product without removing any source octave contribution.
        private static float Fbm3(float octave0, float octave1, float octave2)
        {
            return Mathf.Clamp01(
                octave0 * NoiseWeight0 +
                octave1 * NoiseWeight1 +
                octave2 * NoiseWeight2);
        }

        private static float CombineQuantizedNoise(byte octave0, byte octave1, byte octave2)
        {
            const float inverseByteMax = 1f / byte.MaxValue;
            return Mathf.Clamp01(
                (octave0 * NoiseWeight0 + octave1 * NoiseWeight1 + octave2 * NoiseWeight2)
                * inverseByteMax);
        }

        // The next octave would reach the texel-grid frequency, so the highest
        // packed channel uses two octaves rather than injecting aliased detail.
        private static float Fbm2(float octave0, float octave1)
        {
            return Mathf.Clamp01(octave0 * 0.75f + octave1 * 0.25f);
        }

        private static float PerlinFbm(
            float u,
            float v,
            float w,
            int octaveCount,
            int baseFrequency)
        {
            float value = 0f;
            float amplitude = 1f;
            float amplitudeSum = 0f;
            int frequency = baseFrequency;

            for (int octave = 0; octave < octaveCount; octave++)
            {
                value += Perlin3D(
                    u * frequency,
                    v * frequency,
                    w * frequency,
                    frequency) * amplitude;
                amplitudeSum += amplitude;
                amplitude *= 0.5f;
                frequency *= 2;
            }

            // Improved Perlin with the 12 edge gradients used below has an
            // observed peak near 0.7. This scaling fills useful UNorm range
            // without clipping for the canonical 128³ configuration.
            const float inverseExpectedPeak = 1f / 0.7f;
            return Mathf.Clamp01(
                value / amplitudeSum * 0.5f * inverseExpectedPeak + 0.5f);
        }

        private static float Perlin3D(float x, float y, float z, int period)
        {
            int cellX = Mathf.FloorToInt(x);
            int cellY = Mathf.FloorToInt(y);
            int cellZ = Mathf.FloorToInt(z);

            float localX = x - cellX;
            float localY = y - cellY;
            float localZ = z - cellZ;

            float fadeX = Fade(localX);
            float fadeY = Fade(localY);
            float fadeZ = Fade(localZ);

            uint x0 = (uint)PositiveModulo(cellX, period);
            uint x1 = (uint)PositiveModulo(cellX + 1, period);
            uint y0 = (uint)PositiveModulo(cellY, period);
            uint y1 = (uint)PositiveModulo(cellY + 1, period);
            uint z0 = (uint)PositiveModulo(cellZ, period);
            uint z1 = (uint)PositiveModulo(cellZ + 1, period);

            float g000 = GradientDot(Pcg3D(x0, y0, z0).X, localX, localY, localZ);
            float g100 = GradientDot(Pcg3D(x1, y0, z0).X, localX - 1f, localY, localZ);
            float g010 = GradientDot(Pcg3D(x0, y1, z0).X, localX, localY - 1f, localZ);
            float g110 = GradientDot(Pcg3D(x1, y1, z0).X, localX - 1f, localY - 1f, localZ);
            float g001 = GradientDot(Pcg3D(x0, y0, z1).X, localX, localY, localZ - 1f);
            float g101 = GradientDot(Pcg3D(x1, y0, z1).X, localX - 1f, localY, localZ - 1f);
            float g011 = GradientDot(Pcg3D(x0, y1, z1).X, localX, localY - 1f, localZ - 1f);
            float g111 = GradientDot(Pcg3D(x1, y1, z1).X, localX - 1f, localY - 1f, localZ - 1f);

            float lower = Mathf.Lerp(
                Mathf.Lerp(g000, g100, fadeX),
                Mathf.Lerp(g010, g110, fadeX),
                fadeY);
            float upper = Mathf.Lerp(
                Mathf.Lerp(g001, g101, fadeX),
                Mathf.Lerp(g011, g111, fadeX),
                fadeY);
            return Mathf.Lerp(lower, upper, fadeZ);
        }

        private static float Worley3D(float u, float v, float w, int cellCount)
        {
            float scaledX = u * cellCount;
            float scaledY = v * cellCount;
            float scaledZ = w * cellCount;

            int cellX = Mathf.FloorToInt(scaledX);
            int cellY = Mathf.FloorToInt(scaledY);
            int cellZ = Mathf.FloorToInt(scaledZ);

            float localX = scaledX - cellX;
            float localY = scaledY - cellY;
            float localZ = scaledZ - cellZ;
            float minimumSquaredDistance = float.MaxValue;

            for (int offsetX = -1; offsetX <= 1; offsetX++)
            for (int offsetY = -1; offsetY <= 1; offsetY++)
            for (int offsetZ = -1; offsetZ <= 1; offsetZ++)
            {
                uint neighbourX = (uint)PositiveModulo(cellX + offsetX, cellCount);
                uint neighbourY = (uint)PositiveModulo(cellY + offsetY, cellCount);
                uint neighbourZ = (uint)PositiveModulo(cellZ + offsetZ, cellCount);
                var featureHash = Pcg3D(neighbourX, neighbourY, neighbourZ);

                float featureX = HashToUnitFloat(featureHash.X);
                float featureY = HashToUnitFloat(featureHash.Y);
                float featureZ = HashToUnitFloat(featureHash.Z);

                float deltaX = offsetX + featureX - localX;
                float deltaY = offsetY + featureY - localY;
                float deltaZ = offsetZ + featureZ - localZ;
                float squaredDistance =
                    deltaX * deltaX +
                    deltaY * deltaY +
                    deltaZ * deltaZ;
                minimumSquaredDistance = Mathf.Min(
                    minimumSquaredDistance,
                    squaredDistance);
            }

            return Mathf.Clamp01(1f - Mathf.Sqrt(minimumSquaredDistance));
        }

        private static float GradientDot(uint hash, float x, float y, float z)
        {
            int gradientIndex = (int)(hash % 12u);
            return GradientX[gradientIndex] * x +
                GradientY[gradientIndex] * y +
                GradientZ[gradientIndex] * z;
        }

        private readonly struct HashVector
        {
            public readonly uint X;
            public readonly uint Y;
            public readonly uint Z;

            public HashVector(uint x, uint y, uint z)
            {
                X = x;
                Y = y;
                Z = z;
            }
        }

        private static HashVector Pcg3D(uint x, uint y, uint z)
        {
            unchecked
            {
                x = x * 1664525u + 1013904223u;
                y = y * 1664525u + 1013904223u;
                z = z * 1664525u + 1013904223u;

                x += y * z;
                y += z * x;
                z += x * y;

                x ^= x >> 16;
                y ^= y >> 16;
                z ^= z >> 16;

                x += y * z;
                y += z * x;
                z += x * y;

                return new HashVector(x, y, z);
            }
        }

        private static float HashToUnitFloat(uint value)
        {
            return (value >> 8) * (1f / 16777216f);
        }

        private static float Fade(float value)
        {
            return value * value * value *
                (value * (value * 6f - 15f) + 10f);
        }

        private static float RemapUnclamped(
            float value,
            float sourceMinimum,
            float sourceMaximum,
            float targetMinimum,
            float targetMaximum)
        {
            float normalized =
                (value - sourceMinimum) /
                (sourceMaximum - sourceMinimum);
            return targetMinimum + normalized * (targetMaximum - targetMinimum);
        }

        private static byte Quantize(float value)
        {
            return (byte)Mathf.Clamp(
                Mathf.RoundToInt(Mathf.Clamp01(value) * byte.MaxValue),
                byte.MinValue,
                byte.MaxValue);
        }

        private static int PositiveModulo(int value, int modulus)
        {
            return (value % modulus + modulus) % modulus;
        }

        private static string GetOutputPath() =>
            "Assets/Aetheria/Generated/VolumetricCloudsNoise";

        private static void EnsureFolder(string path)
        {
            string[] segments = path.Split('/');
            string current = segments[0];
            for (int index = 1; index < segments.Length; index++)
            {
                string next = $"{current}/{segments[index]}";
                if (!AssetDatabase.IsValidFolder(next))
                    AssetDatabase.CreateFolder(current, segments[index]);
                current = next;
            }
        }
    }
}
#endif
