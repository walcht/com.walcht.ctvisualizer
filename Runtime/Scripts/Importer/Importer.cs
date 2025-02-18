using System;
using System.Collections.Concurrent;
using System.Diagnostics;
using System.IO;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using Newtonsoft.Json;
using UnityEngine;
using UnityEngine.Assertions;

namespace UnityCTVisualizer {
    public enum ColorDepth {
        UINT8, UINT16
    }

    public enum DownsamplingInterpolation {
        TRILINEAR
    }

    public class CVDSMetadata {
        internal class CVDSMetadataInternal {
            [JsonProperty("original_dims")]
            public int[] OriginalDims { get; set; }

            [JsonProperty("chunk_size")]
            public int ChunkSize { get; set; }

            [JsonProperty("nbr_chunks_per_resolution_lvl")]
            public int[][] NbrChunksPerResolutionLvl { get; set; }

            [JsonProperty("total_nbr_chunks")]
            public int[] TotalNbrChunks { get; set; }

            [JsonProperty("nbr_resolution_lvls")]
            public int NbrResolutionLvls { get; set; }

            [JsonProperty("downsampling_inter")]
            public string DownsamplingInter { get; set; }

            [JsonProperty("color_depth")]
            public int ColorDepth { get; set; }

            [JsonProperty("lz4_compressed")]
            public bool Lz4Compressed { get; set; }
            [JsonProperty("decompressed_chunk_size_in_bytes")]
            public long DecompressedChunkSizeInBytes { get; set; }

            [JsonProperty("voxel_dims")]
            public float[] VoxelDims { get; set; }

            [JsonProperty("euler_rotation")]
            public float[] EulerRotation { get; set; }
        }
        public CVDSMetadata(string root_fp) {
            string metadata_fp = Path.Join(root_fp, "metadata.json");
            if (!File.Exists(metadata_fp)) {
                throw new FileNotFoundException($"metadata.json file was not found in provided CVDS directory: {root_fp}");
            }
            CVDSMetadataInternal metadata = JsonConvert.DeserializeObject<CVDSMetadataInternal>(File.ReadAllText(metadata_fp));
            // assign metadata properties/attributes
            RootFilepath = root_fp;
            Dims = new Vector3Int(metadata.OriginalDims[0], metadata.OriginalDims[1], metadata.OriginalDims[2]);
            ChunkSize = metadata.ChunkSize;
            NbrResolutionLvls = metadata.NbrResolutionLvls;
            DecompressedSizeInBytes = metadata.DecompressedChunkSizeInBytes;
            switch (metadata.ColorDepth) {
                case 8: {
                    ColorDepth = ColorDepth.UINT8;
                    break;
                }
                case 16: {
                    ColorDepth = ColorDepth.UINT16;
                    break;
                }
                default: {
                    throw new Exception($"unexpected color depth value: {metadata.ColorDepth}");
                }
            }
            Lz4Compressed = metadata.Lz4Compressed;
            VoxelDims = new Vector3(metadata.VoxelDims[0], metadata.VoxelDims[1], metadata.VoxelDims[2]);
            EulerRotation = new Vector3(metadata.EulerRotation[0], metadata.EulerRotation[1], metadata.EulerRotation[2]);
            NbrChunksPerResolutionLvl = new Vector3Int[metadata.NbrChunksPerResolutionLvl.Length];
            for (int i = 0; i < metadata.NbrChunksPerResolutionLvl.Length; ++i) {
                Assert.IsTrue(metadata.NbrChunksPerResolutionLvl[i].Length == 3, "expected 3 elements (x, y, z) in array element");
                NbrChunksPerResolutionLvl[i] = new Vector3Int(metadata.NbrChunksPerResolutionLvl[i][0],
                    metadata.NbrChunksPerResolutionLvl[i][1],
                    metadata.NbrChunksPerResolutionLvl[i][2]);
            }
            // assign chunk filepaths for each resolution level
            ChunkFilepaths = new string[NbrResolutionLvls + 1][];
            for (int res_lvl = 0; res_lvl < NbrResolutionLvls; ++res_lvl) {
                string res_directory = Path.Join(RootFilepath, $"resolution_level_{res_lvl}");
                int nbr_chunks = NbrChunksPerResolutionLvl[res_lvl].x * NbrChunksPerResolutionLvl[res_lvl].y
                    * NbrChunksPerResolutionLvl[res_lvl].z;
                string[] chunk_fps = new string[nbr_chunks];
                for (int chunk_id = 0; chunk_id < nbr_chunks; ++chunk_id) {
                    string chunk_fp;
                    if (!Lz4Compressed)
                        chunk_fp = Path.Join(res_directory, $"chunk_{chunk_id}.cvds");
                    else
                        chunk_fp = Path.Join(res_directory, $"chunk_{chunk_id}.cvds.lz4");
                    if (!File.Exists(chunk_fp))
                        throw new Exception($"chunk filepath does not exist: {chunk_fp}");
                    chunk_fps[chunk_id] = chunk_fp;
                }
                ChunkFilepaths[res_lvl] = chunk_fps;
            }
            DownsamplingInter = (DownsamplingInterpolation)Enum.Parse(typeof(DownsamplingInterpolation), metadata.DownsamplingInter.ToUpper());
        }
        public Vector3Int Dims { get; private set; }
        public int ChunkSize { get; private set; }
        public long CompressedSizeInBytes { get; private set; }

        /// <summary>
        ///     This is simply: ChunkSize * ChunkSize * ChunkSize * voxel_size_bytes
        ///     with, for example, voxel_size_bytes = 2 in case ColorDepth is UINT16
        /// </summary>
        public long DecompressedSizeInBytes { get; private set; }

        /// <summary>
        ///     Number of resolution levels up-to and including this number.
        ///     Resolution level 0 corresponds to highest resolution, resolution level 1
        ///     is downsampled once from resolution level 0, and so on.
        /// </summary>
        public int NbrResolutionLvls { get; private set; }

        public ColorDepth ColorDepth { get; private set; }

        public bool Lz4Compressed { get; private set; }

        /// <summary>
        ///     Physical dimensions in mm of a voxel (i.e., a 3D cube corresponding
        ///     to a sampled region of space).
        /// </summary>
        public Vector3 VoxelDims { get; private set; }
        public Vector3 EulerRotation { get; private set; }

        /// <summary>
        ///     Which downsampling interpolation is used to generate coarser volume chunks.
        ///     Currently only trillinear interpolation (i.e., averaging) is supported.
        ///     The usage of other downsampling interpolators introduces significant challenges
        ///     further down the pipeline.
        /// </summary>
        public DownsamplingInterpolation DownsamplingInter { get; private set; }

        /// <summary>
        /// 
        /// </summary>
        public Vector3Int[] NbrChunksPerResolutionLvl { get; private set; }
        public string RootFilepath { get; private set; }

        /// <summary>
        ///     Chunks filepaths grouped per resolution level. Should be accessed as
        ///     follows: ChunkFilepaths[resolution_lvl][chunk_id]
        /// </summary>
        public string[][] ChunkFilepaths { get; private set; }

    }

    public static class Importer {
        public static CVDSMetadata ImportMetadata(string dataset_path) {
            try {
                return new CVDSMetadata(dataset_path);
            } catch (Exception e) {
                throw new FileLoadException($"Failed to extract CVDS dataset from provided dataset directory, reason: {e.Message}");
            }
        }

        public static int BrickToChunkID(CVDSMetadata metadata, UInt32 brick_id, int brick_size) {
            int id = (int)(brick_id & 0x03FFFFFF);
            int resolution_lvl = (int)(brick_id >> 26);
            Vector3Int nbr_chunks = metadata.NbrChunksPerResolutionLvl[resolution_lvl];
            int ratio = metadata.ChunkSize / brick_size;
            int chunk_id = (id / (nbr_chunks.x * nbr_chunks.y * ratio * ratio * ratio)) * nbr_chunks.x * nbr_chunks.y +  // Z-axis offset
                ((id / (nbr_chunks.x * ratio * ratio)) % nbr_chunks.y) * nbr_chunks.x +  // Y-axis offset
                (id % (nbr_chunks.x * ratio)) / ratio;  // X-axis offset
            return chunk_id;
        }

        public static Vector3Int GetBrickOffsetWithinChunk(CVDSMetadata metadata, UInt32 brick_id, int brick_size) {
            int id = (int)(brick_id & 0x03FFFFFF);
            int resolution_lvl = (int)(brick_id >> 26);
            Vector3Int nbr_chunks = metadata.NbrChunksPerResolutionLvl[resolution_lvl];
            int ratio = metadata.ChunkSize / brick_size;
            int offset_x = ((id % (nbr_chunks.x * ratio)) % ratio) * brick_size;
            int offset_y = ((id / (nbr_chunks.x * ratio)) % ratio) * brick_size;
            int offset_z = ((id / (nbr_chunks.x * nbr_chunks.y * ratio * ratio)) % ratio) * brick_size;
            return new Vector3Int(offset_x, offset_y, offset_z);
        }

        public static int GetBytearrayOffset(int i, int x, int y, int z, int c, int b, int bpc) {
            return (z * c * c + (y + (i / b) % b + (i / (b * b)) * c) * c + x + i % b) * bpc;
        }


        /// <summary>
        ///     Imports a single volume brick from its corresponding chunk into the provided
        ///     memory cache
        /// </summary>
        /// 
        /// <param name="metadata"></param>
        /// <param name="brick_id"></param>
        /// <param name="brick_size"></param>
        /// <param name="cache"></param>
        /// 
        /// <remarks>
        ///     Import will fail if chunk size in bytes exceeds the maximum size for an object allowed
        ///     by .NET (i.e., 2GBs). No size checks are performed in this function.
        /// </remarks>
        public static void ImportBrick(CVDSMetadata metadata, UInt32 brick_id, int brick_size,
            MemoryCache<UInt16> cache) {
            int resolution_lvl = (int)(brick_id >> 26);
            int chunk_id = Importer.BrickToChunkID(metadata, brick_id, brick_size);
            Vector3Int brick_offset_within_chunk = Importer.GetBrickOffsetWithinChunk(metadata, brick_id, brick_size);
            string chunk_fp = metadata.ChunkFilepaths[resolution_lvl][chunk_id];
            byte[] source_data = File.ReadAllBytes(chunk_fp);
            long brick_size_cubed = brick_size * brick_size * brick_size;
            byte[] decompressed_data;
            if (metadata.Lz4Compressed) {
                decompressed_data = new byte[metadata.DecompressedSizeInBytes];
                LZ4Codec.Decode(source: source_data, target: decompressed_data);
            } else {
                decompressed_data = source_data;
            }
            UInt16[] data = new UInt16[brick_size_cubed];
            UInt16 min = UInt16.MaxValue;
            UInt16 max = UInt16.MinValue;
            for (int i = 0; i < brick_size_cubed; ++i) {
                int j = Importer.GetBytearrayOffset(i, brick_offset_within_chunk.x, brick_offset_within_chunk.y,
                    brick_offset_within_chunk.z, metadata.ChunkSize, brick_size, sizeof(UInt16));
                data[i] = BitConverter.ToUInt16(decompressed_data, j);
                min = Math.Min(min, data[i]);
                max = Math.Max(max, data[i]);
            }
            cache.Set(brick_id, new CacheEntry<UInt16>(data, min, max));
        }

        public static void ImportBrick(CVDSMetadata metadata, UInt32 brick_id, int brick_size,
            MemoryCache<byte> cache) {
            int resolution_lvl = (int)(brick_id >> 26);
            int chunk_id = BrickToChunkID(metadata, brick_id, brick_size);
            Vector3Int brick_offset_within_chunk = Importer.GetBrickOffsetWithinChunk(metadata, brick_id, brick_size);
            string chunk_fp = metadata.ChunkFilepaths[resolution_lvl][chunk_id];
            byte[] source_data = File.ReadAllBytes(chunk_fp);
            // TODO: add optimization for when chunk_size == brick_size
            long brick_size_cubed = brick_size * brick_size * brick_size;
            byte[] decompressed_data;
            if (metadata.Lz4Compressed) {
                decompressed_data = new byte[metadata.DecompressedSizeInBytes * 2];
                LZ4Codec.Decode(source: source_data, target: decompressed_data);
            } else {
                decompressed_data = source_data;
            }
            byte[] data = new byte[brick_size_cubed];
            byte min = byte.MaxValue;
            byte max = byte.MinValue;
            for (int i = 0; i < brick_size_cubed; ++i) {
                int j = Importer.GetBytearrayOffset(i, brick_offset_within_chunk.x, brick_offset_within_chunk.y,
                    brick_offset_within_chunk.z, metadata.ChunkSize, brick_size, sizeof(byte));
                data[i] = decompressed_data[j];
                min = Math.Min(min, data[i]);
                max = Math.Max(max, data[i]);
            }
            cache.Set(brick_id, new CacheEntry<byte>(data, min, max));
        }

        public static void LoadAllBricksIntoCache(CVDSMetadata metadata, int brick_size, int resolution_lvl,
            MemoryCache<byte> cache, ConcurrentQueue<UInt32> brick_reply_queue,
            IProgressHandler progressHandler = null) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long total_nbr_bricks = metadata.NbrChunksPerResolutionLvl[resolution_lvl].x *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].y *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].z *
                (int)Math.Pow(metadata.ChunkSize / brick_size, 3);
            if (progressHandler != null) {
                progressHandler.MaxProgressValue = (int)total_nbr_bricks;
                progressHandler.Message = $"uploading {total_nbr_bricks} bricks to host memory cache ...";
            }
            UnityEngine.Debug.Log($"uploading {total_nbr_bricks} bricks to host memory cache ...");
            Parallel.For(0, total_nbr_bricks, new ParallelOptions() {
                TaskScheduler = new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount - 2)
            }, i => {
                UInt32 brick_id = (UInt32)i | (UInt32)resolution_lvl << 26;
                ImportBrick(metadata, brick_id, brick_size, cache);
                brick_reply_queue.Enqueue(brick_id);
                if (progressHandler != null) {
                    progressHandler.IncrementProgress();
                }
            });
            stopwatch.Stop();
            if (progressHandler != null) {
                progressHandler.Message = $"all {total_nbr_bricks} bricks uploaded in {stopwatch.Elapsed.TotalSeconds:0.00}s";
            }
            UnityEngine.Debug.Log($"uploading to host memory cache took: {stopwatch.Elapsed}s");
        }

        public static void LoadAllBricksIntoCache(CVDSMetadata metadata, int brick_size, int resolution_lvl,
            MemoryCache<UInt16> cache, ConcurrentQueue<UInt32> brick_reply_queue,
            IProgressHandler progressHandler = null) {
            Stopwatch stopwatch = Stopwatch.StartNew();
            long total_nbr_bricks = metadata.NbrChunksPerResolutionLvl[resolution_lvl].x *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].y *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].z *
                (int)Math.Pow(metadata.ChunkSize / brick_size, 3);
            if (progressHandler != null) {
                progressHandler.MaxProgressValue = (int)total_nbr_bricks;
                progressHandler.Message = $"uploading {total_nbr_bricks} bricks to host memory cache ...";
            }
            UnityEngine.Debug.Log($"uploading {total_nbr_bricks} bricks to CPU memory cache ...");
            Parallel.For(0, total_nbr_bricks, new ParallelOptions() {
                TaskScheduler = new LimitedConcurrencyLevelTaskScheduler(Environment.ProcessorCount - 2)
            }, i => {
                UInt32 brick_id = (UInt32)i | (UInt32)resolution_lvl << 26;
                ImportBrick(metadata, brick_id, brick_size, cache);
                brick_reply_queue.Enqueue(brick_id);
                if (progressHandler != null) {
                    progressHandler.IncrementProgress();
                }
            });
            stopwatch.Stop();
            if (progressHandler != null) {
                progressHandler.Message = $"all {total_nbr_bricks} bricks uploaded in {stopwatch.Elapsed.TotalSeconds:0.00}s";
            }
            UnityEngine.Debug.Log($"uploading to CPU memory cache took: {stopwatch.Elapsed}s");
        }

        public static void GenerateHomogeneousBrick<T>(UInt32 brick_id, int brick_size, T fill_value,
            MemoryCache<T> cache) where T : unmanaged {
            long brick_size_cubed = brick_size * brick_size * brick_size;
            T[] data = new T[brick_size_cubed];
            for (int i = 0; i < brick_size_cubed; ++i) {
                data[i] = fill_value;
            }
            cache.Set(brick_id, new CacheEntry<T>(data, fill_value, fill_value));
        }

        public static void GenerateGradientBrick(UInt32 brick_id, int brick_size, byte v0, byte v1,
            MemoryCache<byte> cache) {
            long brick_size_cubed = brick_size * brick_size * brick_size;
            byte[] data = new byte[brick_size_cubed];
            for (int i = 0; i < brick_size_cubed; ++i) {
                float t = (float)(i / (brick_size * brick_size)) / (brick_size - 1);
                data[i] = (byte)((1 - t) * v0 + t * v1);
            }
            cache.Set(brick_id, new CacheEntry<byte>(data, Math.Min(v0, v1), Math.Max(v0, v1)));
        }

    }
}
