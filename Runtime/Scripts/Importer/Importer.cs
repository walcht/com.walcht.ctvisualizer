using System;
using System.Collections.Concurrent;
using System.IO;
using System.Runtime.InteropServices;
using System.Text;
using System.Threading.Tasks;
using K4os.Compression.LZ4;
using Newtonsoft.Json;
using UnityEngine;

namespace UnityCTVisualizer
{
    public enum ColorDepth
    {
        UINT8
    }

    public enum DownsamplingInterpolation
    {
        TRILINEAR
    }

    [StructLayout(LayoutKind.Sequential, Size = 20)]
    public struct ResidencyNode
    {
        public float center_x;     // 4 bytes
        public float center_y;     // 4 bytes
        public float center_z;     // 4 bytes
        public float side_halved;  // 4 bytes
        public uint data;          // 4 bytes
    }

    public struct VDHM
    {
        public int tolerance;
        public double penalty;
        public double measure;
    }


    [Serializable]
    public class CVDSMetadataInternal
    {
        [JsonProperty("original_dims")]
        public int[] OriginalDims { get; set; }

        [JsonProperty("chunk_size")]
        public int ChunkSize { get; set; }

        [JsonProperty("chunk_padding")]
        public bool ChunkPadding { get; set; }

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

        [JsonProperty("force_8bit_conversion")]
        public bool ConvertedToUInt8 { get; set; }

        [JsonProperty("lz4_compressed")]
        public bool Lz4Compressed { get; set; }

        [JsonProperty("decompressed_chunk_size_in_bytes")]
        public long DecompressedChunkSizeInBytes { get; set; }

        [JsonProperty("vdhms")]
        public double[][] VDHMs { get; set; }

        [JsonProperty("octree_nrb_nodes")]
        public long OctreeNbrNodes { get; set; }

        [JsonProperty("octree_max_depth")]
        public int OctreeMaxDepth { get; set; }

        [JsonProperty("octree_smallest_subdivision")]
        public float[] OctreeSmallestSubdivision { get; set; }

        [JsonProperty("octree_size_in_bytes")]
        public long OctreeSizeInBytes { get; set; }

        [JsonProperty("histogram_nbr_bins")]
        public int HistogramNbrBins { get; set; }

        [JsonProperty("voxel_dims")]
        public float[] VoxelDims { get; set; }

        [JsonProperty("euler_rotation")]
        public float[] EulerRotation { get; set; }
    }


    public class CVDSMetadata
    {
        private readonly CVDSMetadataInternal m_InternalMetadata;
        public CVDSMetadataInternal GetInternalMetadata() => m_InternalMetadata;

        public CVDSMetadata(string root_fp)
        {
            string metadata_fp = Path.Join(root_fp, "metadata.json");
            if (!Directory.Exists(root_fp))
            {
                throw new Exception($"provided SEARCH_CVDS directory path is not a valide directory path: {root_fp}");
            }
            if (!File.Exists(metadata_fp))
            {
                throw new Exception($"metadata.json file was not found in provided SEARCH_CVDS directory: {root_fp}");
            }
            bool deserializationError = false;
            StringBuilder error_sb = new();
            CVDSMetadataInternal metadata = JsonConvert.DeserializeObject<CVDSMetadataInternal>(
                File.ReadAllText(metadata_fp),
                new JsonSerializerSettings
                {
                    Error = (object sender, Newtonsoft.Json.Serialization.ErrorEventArgs args) =>
                    {
                        error_sb.Append(args.ErrorContext.Error.Message);
                        args.ErrorContext.Handled = true;
                        deserializationError = true;
                    }
                });
            if (deserializationError)
            {
                throw new Exception(error_sb.ToString());
            }

            // assign metadata properties/attributes
            RootFilepath = root_fp;

            if (metadata.OriginalDims.Length != 3)
                throw new Exception($"invalid original dimensions length {metadata.OriginalDims.Length}. Expected 3");
            if (metadata.OriginalDims[0] <= 0 || metadata.OriginalDims[1] <= 0 || metadata.OriginalDims[2] <= 0)
                throw new Exception($"invalid original dimensions {metadata.OriginalDims}. Expected dimensions > 0");
            Dims = new Vector3Int(metadata.OriginalDims[0], metadata.OriginalDims[1], metadata.OriginalDims[2]);

            if (metadata.ChunkSize < 32 || metadata.ChunkSize >= 1024)
                throw new Exception($"invalid chunk size {metadata.ChunkSize}. Expected [32, 1024[");
            if ((metadata.ChunkSize & (metadata.ChunkSize - 1)) != 0)
                throw new Exception($"invalid chunk size {metadata.ChunkSize}. Expected power of 2");
            ChunkSize = metadata.ChunkSize;
            ChunkPadding = metadata.ChunkPadding;

            if (metadata.NbrResolutionLvls <= 0)
                throw new Exception($"invalid number of resolution levels: {metadata.NbrResolutionLvls}");
            NbrResolutionLvls = metadata.NbrResolutionLvls;

            DecompressedSizeInBytes = metadata.DecompressedChunkSizeInBytes;

            switch (metadata.ColorDepth)
            {
                case 8:
                {
                    ColorDepth = ColorDepth.UINT8;
                    break;
                }
                default:
                {
                    throw new Exception($"unsupported/invalid color depth value: {metadata.ColorDepth}");
                }
            }

            Lz4Compressed = metadata.Lz4Compressed;

            if (metadata.VoxelDims.Length != 3)
                throw new Exception($"invalid voxel dims length {metadata.VoxelDims.Length}. Expected 3");
            if (metadata.VoxelDims[0] <= 0 || metadata.VoxelDims[1] <= 0 || metadata.VoxelDims[2] <= 0)
                throw new Exception($"invalid voxel dims {metadata.VoxelDims}. Expected dimensions > 0");
            VoxelDims = new Vector3(metadata.VoxelDims[0], metadata.VoxelDims[1], metadata.VoxelDims[2]);

            if (metadata.EulerRotation.Length != 3)
                throw new Exception($"invalid Euler rotation length {metadata.EulerRotation.Length}. Expected 3");
            EulerRotation = new Vector3(metadata.EulerRotation[0], metadata.EulerRotation[1], metadata.EulerRotation[2]);

            if (metadata.NbrChunksPerResolutionLvl.Length != metadata.NbrResolutionLvls)
                throw new Exception($"invalid number chunks per resolution level length {metadata.NbrChunksPerResolutionLvl.Length}");
            NbrChunksPerResolutionLvl = new Vector3Int[metadata.NbrChunksPerResolutionLvl.Length];

            for (int i = 0; i < metadata.NbrChunksPerResolutionLvl.Length; ++i)
            {
                if (metadata.NbrChunksPerResolutionLvl[i].Length != 3)
                    throw new Exception($"invalid number chunks per resolution level entry. Expected 3 elements");
                NbrChunksPerResolutionLvl[i] = new Vector3Int(metadata.NbrChunksPerResolutionLvl[i][0],
                    metadata.NbrChunksPerResolutionLvl[i][1],
                    metadata.NbrChunksPerResolutionLvl[i][2]);
            }

            // assign chunk filepaths for each resolution level
            ChunkFilepaths = new string[NbrResolutionLvls + 1][];
            for (int res_lvl = 0; res_lvl < NbrResolutionLvls; ++res_lvl)
            {
                string res_directory = Path.Join(RootFilepath, $"resolution_level_{res_lvl}");
                int nbr_chunks = NbrChunksPerResolutionLvl[res_lvl].x * NbrChunksPerResolutionLvl[res_lvl].y
                    * NbrChunksPerResolutionLvl[res_lvl].z;
                string[] chunk_fps = new string[nbr_chunks];
                for (int chunk_id = 0; chunk_id < nbr_chunks; ++chunk_id)
                {
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

            try
            {
                DownsamplingInter = (DownsamplingInterpolation)Enum.Parse(typeof(DownsamplingInterpolation), metadata.DownsamplingInter.ToUpper());
            }
            catch (Exception e)
            {
                throw new Exception($"invalid downsampling interpolation value {metadata.DownsamplingInter}. {e.Message}");
            }

            ConvertedToUInt8 = metadata.ConvertedToUInt8;

            // set octree properties
            OctreeMaxDepth = metadata.OctreeMaxDepth;
            OctreeNbrNodes = metadata.OctreeNbrNodes;
            OctreeSizeInBytes = metadata.OctreeSizeInBytes;
            OctreeSmallestSubdivision = new Vector3(metadata.OctreeSmallestSubdivision[0], metadata.OctreeSmallestSubdivision[1],
                metadata.OctreeSmallestSubdivision[2]);

            // set VDH measures
            VDHMs = new VDHM[metadata.VDHMs.Length];
            for (int i = 0; i < VDHMs.Length; ++i)
                VDHMs[i] = new VDHM()
                {
                    measure = metadata.VDHMs[i][2],
                    penalty = metadata.VDHMs[i][0],
                    tolerance = (int)metadata.VDHMs[i][0]
                };

            // set histogram properties
            HistogramNbrBins = metadata.HistogramNbrBins;

            m_InternalMetadata = metadata;
        }

        /// <summary>
        ///     The root filepath of the SEARCH_CVDS dataset. This is the directory containing the metadata.json file along
        ///     with resolution_lvl_n subfolders which containt volume chunks.
        /// </summary>
        public string RootFilepath { get; private set; }

        /// <summary>
        ///     Chunks filepaths grouped per resolution level.
        ///     Should be accessed as follows:
        ///         <code>ChunkFilepaths[resolution_lvl][chunk_id]</code>
        /// </summary>
        public string[][] ChunkFilepaths { get; private set; }

        /// <summary>
        ///     Original dimension of the volumetric datasets (i.e., without paddings).
        /// </summary>
        public Vector3Int Dims { get; private set; }

        /// <summary>
        ///     Chunk size. Anisotropic (i.e., with different dimensions) are not supported. Additionally, chunk
        ///     size is a power of two and should idealy be set to a value that allows for optimal filesystem read
        ///     speed.
        /// </summary>
        public int ChunkSize { get; private set; }

        /// <summary>
        ///     Whether padding is added to the chunks for the purpose of trillinear inter-brick interpolation.
        ///     If set to true, the actual chunk size is increased by 2.
        /// </summary>
        public bool ChunkPadding { get; private set; }

        /// <summary>
        ///     Number of chunks per each resolution level. Number of entries is equal to NbrResolutionLvls and each
        ///     entry holds the number of chunks along each dimension (nbr_chunks_x, nbr_chunks_y, nbr_chunks_z).
        /// </summary>
        public Vector3Int[] NbrChunksPerResolutionLvl { get; private set; }

        public long CompressedSizeInBytes { get; private set; }

        /// <summary>
        ///     Number of resolution levels. Resolution level 0 corresponds to highest resolution, resolution level 1
        ///     is downsampled once from resolution level 0, and so on.
        /// </summary>
        public int NbrResolutionLvls { get; private set; }

        /// <summary>
        ///     Which downsampling interpolation is used to generate coarser volume chunks. Currently only trillinear
        ///     interpolation (i.e., averaging) is supported. The usage of other downsampling interpolators introduces
        ///     significant challenges further down the pipeline.
        /// </summary>
        public DownsamplingInterpolation DownsamplingInter { get; private set; }

        /// <summary>
        ///     Color depth (or bits per pixel) of the attached chunks. Currently, only 8 bpp (i.e., uint_8) is
        ///     supported. 16 bpp will potentially be added in the future.
        /// </summary>
        public ColorDepth ColorDepth { get; private set; }

        /// <summary>
        ///     Whether the original volumetric dataset was converted to 8 bpp.
        /// </summary>
        public bool ConvertedToUInt8 { get; set; }

        /// <summary>
        ///     Whether the chunks are LZ4 compressed.
        /// </summary>
        public bool Lz4Compressed { get; private set; }

        /// <summary>
        ///     This is simply: ChunkSize * ChunkSize * ChunkSize * voxel_size_bytes with, for example,
        ///     voxel_size_bytes = 2 in case ColorDepth is UINT16
        /// </summary>
        public long DecompressedSizeInBytes { get; private set; }

        /// <summary>
        ///     Volumetric Dataset Homogeneity Measures (VDHM)s for different tolerance values.
        ///     The tolerance is simply the threshold under which if the difference between an octree node's min and max
        ///     values falls, the corresponding node is considered homogeneous.
        /// </summary>
        public VDHM[] VDHMs { get; private set; }

        /// <summary>
        ///     Total number of nodes in the residency octree.
        /// </summary>
        public long OctreeNbrNodes { get; private set; }

        /// <summary>
        ///     Inclusive max traversal depth of the residency octree. A smaller value can be chosen during runtime
        ///     for potentially better performance (i.e., smaller tree size and shorter traversal loop).
        /// </summary>
        public int OctreeMaxDepth { get; private set; }

        /// <summary>
        ///     Smallest spatial extent (in voxels) covered by a residency octree leaf node at depth <paramref name="OctreeMaxDepth"/>.
        ///     This gives an idea about the smallest skippable region and whether it makes sense, performance wise, to
        ///     skip at such granularity.
        /// </summary>
        public Vector3 OctreeSmallestSubdivision { get; private set; }

        /// <summary>
        ///     Total size of the residency octree in bytes.
        /// </summary>
        public long OctreeSizeInBytes { get; private set; }

        /// <summary>
        ///     Number of bins in the generated histogram.
        /// </summary>
        public int HistogramNbrBins { get; set; }

        /// <summary>
        ///     Physical dimensions in mm of a voxel (i.e., a cuboid corresponding to a sampled region of space).
        /// </summary>
        public Vector3 VoxelDims { get; private set; }

        public Vector3 EulerRotation { get; private set; }
    }

    public static class Importer
    {
        public static CVDSMetadata ImportMetadata(string dataset_path)
        {
            return new CVDSMetadata(dataset_path);
        }

        public static int BrickToChunkID(CVDSMetadata metadata, UInt32 brick_id, int brick_size)
        {
            int id = (int)(brick_id & 0x03FFFFFF);
            int resolution_lvl = (int)(brick_id >> 26);
            Vector3Int nbr_chunks = metadata.NbrChunksPerResolutionLvl[resolution_lvl];
            int ratio = metadata.ChunkSize / brick_size;
            int chunk_id = (id / (nbr_chunks.x * nbr_chunks.y * ratio * ratio * ratio)) * nbr_chunks.x * nbr_chunks.y +  // Z-axis offset
                ((id / (nbr_chunks.x * ratio * ratio)) % nbr_chunks.y) * nbr_chunks.x +  // Y-axis offset
                (id % (nbr_chunks.x * ratio)) / ratio;  // X-axis offset
            return chunk_id;
        }

        public static Vector3Int GetBrickOffsetWithinChunk(CVDSMetadata metadata, UInt32 brick_id, int brick_size)
        {
            int id = (int)(brick_id & 0x03FFFFFF);
            int resolution_lvl = (int)(brick_id >> 26);
            Vector3Int nbr_chunks = metadata.NbrChunksPerResolutionLvl[resolution_lvl];
            int ratio = metadata.ChunkSize / brick_size;
            int offset_x = ((id % (nbr_chunks.x * ratio)) % ratio) * brick_size;
            int offset_y = ((id / (nbr_chunks.x * ratio)) % ratio) * brick_size;
            int offset_z = ((id / (nbr_chunks.x * nbr_chunks.y * ratio * ratio)) % ratio) * brick_size;
            return new Vector3Int(offset_x, offset_y, offset_z);
        }

        public static int GetBytearrayOffset(int i, Vector3Int brick_offset_within_chunk, int chunk_size,
            int brick_size, int bpc)
        {
            return (brick_offset_within_chunk.z * chunk_size * chunk_size
                + (brick_offset_within_chunk.y + (i / brick_size) % brick_size + (i / (brick_size * brick_size)) * chunk_size) * chunk_size
                + brick_offset_within_chunk.x + i % brick_size) * bpc;
        }


        /// <summary>
        ///     Imports a single volume brick from its corresponding chunk into the provided memory cache.
        /// </summary>
        /// 
        /// <param name="metadata">
        ///     CVDS metadata used to retrieve chunk filepaths and check for chunk padding (and therefore inter-brick
        ///     interpolation support).
        /// </param>
        ///
        /// <param name="brick_id">
        ///     ID of the brick to be imported.
        /// </param>
        ///
        /// <param name="brick_size">
        ///     Brick size excluding potential padding.
        /// </param>
        ///
        /// <param name="cache">
        ///     CPU memory brick cache.
        /// </param>
        ///
        /// <param name="ignore_inter_brick_interpolation">
        ///     Whether to ignore inter-brick interpolation in case chunk padding is added.
        /// </param>
        /// 
        /// <remarks>
        ///     Import will fail if chunk size in bytes exceeds the maximum size for an object allowed
        ///     by .NET (i.e., 2GBs). No size checks are performed in this function.
        /// </remarks>
        public static void ImportBrick(CVDSMetadata metadata, UInt32 brick_id, int brick_size,
            MemoryCache<byte> cache, bool ignore_inter_brick_interpolation = false)
        {
            int resolution_lvl = (int)(brick_id >> 26);
            int chunk_id = BrickToChunkID(metadata, brick_id, brick_size);
            Vector3Int brick_offset_within_chunk = Importer.GetBrickOffsetWithinChunk(metadata, brick_id, brick_size);
            string chunk_fp = metadata.ChunkFilepaths[resolution_lvl][chunk_id];
            byte[] source_data = File.ReadAllBytes(chunk_fp);

            // TODO: add optimization for when chunk_size == BrickSize
            int chunk_size_offset = 0;
            int brick_size_offset = 0;
            if (metadata.ChunkPadding && !ignore_inter_brick_interpolation)
            {
                brick_size_offset = 2;
                chunk_size_offset = 2;
            }
            else if (metadata.ChunkPadding && ignore_inter_brick_interpolation)
            {
                // ignore the chunk padding
                brick_offset_within_chunk += Vector3Int.one;
                chunk_size_offset = 2;
            }
            int brick_size_cubed = (brick_size + brick_size_offset) * (brick_size + brick_size_offset) * (brick_size + brick_size_offset);
            int decompressed_size = (metadata.ChunkSize + chunk_size_offset) * (metadata.ChunkSize + chunk_size_offset) * (metadata.ChunkSize + chunk_size_offset);

            byte[] decompressed_data;
            if (metadata.Lz4Compressed)
            {
                decompressed_data = new byte[decompressed_size];

                int res = LZ4Codec.Decode(
                    source_data, 0, source_data.Length,
                    decompressed_data, 0, decompressed_data.Length
                );

                if (res != decompressed_size)
                {
                    throw new Exception("chunk decompression failed. " +
                        $"Unexpected decompressed data size: {res} != {decompressed_size}");
                }
            }
            else
            {
                decompressed_data = source_data;
            }

            byte[] data = new byte[brick_size_cubed];
            byte min = byte.MaxValue;
            byte max = byte.MinValue;
            for (int i = 0; i < brick_size_cubed; ++i)
            {
                int j = Importer.GetBytearrayOffset(i, brick_offset_within_chunk, metadata.ChunkSize + chunk_size_offset,
                    brick_size + brick_size_offset, sizeof(byte));
                data[i] = decompressed_data[j];
                min = Math.Min(min, data[i]);
                max = Math.Max(max, data[i]);
            }

            // avoid setting the costly data array in the memory cache
            if (min == max)
            {
                data = null;
            }

            cache.Set(brick_id, new CacheEntry<byte>(data, min, max));
        }

        public static void LoadAllBricksIntoCache(CVDSMetadata metadata, int brick_size, int resolution_lvl,
            MemoryCache<byte> cache, ConcurrentQueue<UInt32> brick_reply_queue, int nbr_importer_threads = -1, bool ignore_inter_brick_interpolation = true)
        {
            long total_nbr_bricks = metadata.NbrChunksPerResolutionLvl[resolution_lvl].x *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].y *
                metadata.NbrChunksPerResolutionLvl[resolution_lvl].z *
                (int)Math.Pow(metadata.ChunkSize / brick_size, 3);

            // update progress bar UI value and message
            ProgressHandlerEvents.OnRequestMaxProgressValueUpdate?.Invoke((int)total_nbr_bricks);
            ProgressHandlerEvents.OnRequestProgressMessageUpdate?.Invoke($"uploading {total_nbr_bricks} bricks to host memory cache ...");

            int nbr_threads = nbr_importer_threads > 0 ? nbr_importer_threads :
                Math.Max(Environment.ProcessorCount - 2, 1);
#if DEBUG
            UnityEngine.Debug.Log($"uploading {total_nbr_bricks} bricks to host memory cache ...");
#endif
            Parallel.For(0, total_nbr_bricks, new ParallelOptions()
            {
                TaskScheduler = new LimitedConcurrencyLevelTaskScheduler(nbr_threads)
            }, i =>
            {
                UInt32 brick_id = (UInt32)i | (UInt32)resolution_lvl << 26;
                ImportBrick(metadata, brick_id, brick_size, cache, ignore_inter_brick_interpolation);
                brick_reply_queue.Enqueue(brick_id);
                // increment progress value by 1
                ProgressHandlerEvents.OnRequestProgressValueIncrement?.Invoke();
            });
            ProgressHandlerEvents.OnRequestProgressMessageUpdate?.Invoke($"all {total_nbr_bricks} bricks uploaded");
        }

        public static void GenerateHomogeneousBrick<T>(UInt32 brick_id, int brick_size, T fill_value,
            MemoryCache<T> cache) where T : unmanaged
        {
            long brick_size_cubed = brick_size * brick_size * brick_size;
            T[] data = new T[brick_size_cubed];
            for (int i = 0; i < brick_size_cubed; ++i)
            {
                data[i] = fill_value;
            }
            cache.Set(brick_id, new CacheEntry<T>(data, fill_value, fill_value));
        }

        public static void GenerateGradientBrick(UInt32 brick_id, int brick_size, byte v0, byte v1,
            MemoryCache<byte> cache)
        {
            long brick_size_cubed = brick_size * brick_size * brick_size;
            byte[] data = new byte[brick_size_cubed];
            for (int i = 0; i < brick_size_cubed; ++i)
            {
                float t = (float)(i / (brick_size * brick_size)) / (brick_size - 1);
                data[i] = (byte)((1 - t) * v0 + t * v1);
            }
            cache.Set(brick_id, new CacheEntry<byte>(data, Math.Min(v0, v1), Math.Max(v0, v1)));
        }


        /// <summary>
        ///     Imports the residency octree from the provided SEARCH_CVDS metadata.
        /// </summary>
        /// 
        /// <param name="metadata">
        ///     SEARCH_CVDS metadata
        /// </param>
        /// 
        /// <returns>
        ///     Array of residency octree nodes.
        /// </returns>
        public static ResidencyNode[] ImportResidencyOctree(CVDSMetadata metadata)
        {
            string fp = Path.Join(metadata.RootFilepath, "residency_octree.bin");
            byte[] source_data = File.ReadAllBytes(fp);
#if DEBUG
            Debug.Assert(metadata.OctreeNbrNodes == (source_data.Length / Marshal.SizeOf<ResidencyNode>()),
                $"unexpected number of residency octree nodes from {fp}");
#endif
            ResidencyNode[] data = new ResidencyNode[metadata.OctreeNbrNodes];
            for (int i = 0, j = 0; i < source_data.Length; ++j, i += 20)
            {
                float center_x = BitConverter.ToSingle(source_data, i);
                float center_y = BitConverter.ToSingle(source_data, i + 4);
                float center_z = BitConverter.ToSingle(source_data, i + 8);
                float side_halved = BitConverter.ToSingle(source_data, i + 12);
                UInt32 _data = BitConverter.ToUInt32(source_data, i + 16);
                data[j] = new ResidencyNode()
                {
                    center_x = center_x,
                    center_y = center_y,
                    center_z = center_z,
                    side_halved = side_halved,
                    data = _data
                };
            }
            return data;
        }

        /// <summary>
        ///     Imports the generated histogram from the provided SEARCH_CVDS metadata.
        /// </summary>
        /// 
        /// <param name="metadata">
        ///     SEARCH_CVDS metadata. histogram.bin file is expected to be resident in the root directory of this SEARCH_CVDS
        /// </param>
        /// 
        /// <returns>
        ///     Array of binned densities where each entry is the number of densities that fall within its uniform range.
        /// </returns>
        public static UInt64[] ImportHistogram(CVDSMetadata metadata)
        {
            string fp = Path.Join(metadata.RootFilepath, "histogram.bin");
            byte[] source_data = File.ReadAllBytes(fp);
#if DEBUG
            Debug.Assert(metadata.HistogramNbrBins == (source_data.Length) / Marshal.SizeOf<UInt64>(),
                $"unexpected number of histogram bins from {fp}");
#endif
            UInt64[] data = new UInt64[metadata.HistogramNbrBins];

            for (int i = 0, j = 0; i < source_data.Length; i += 8, ++j)
            {
                UInt64 val = BitConverter.ToUInt64(source_data, i);
                data[j] = val;
            }
            return data;
        }
    }
}
