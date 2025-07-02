using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
using System.Linq;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TextureSubPlugin;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityCTVisualizer
{

    public enum RenderingMode
    {
        IC = 0,
        OOC_PT,
        OOC_HYBRID
    }


    /// <summary>
    ///     Describes wether a page table entry is mapped, unmapped, or homogeneous.
    /// </summary>
    public enum PageEntryFlag : byte
    {
        HOMOGENEOUS_PAGE_TABLE_ENTRY = 0,
        UNMAPPED_PAGE_TABLE_ENTRY = 1,
        MAPPED_PAGE_TABLE_ENTRY = 2,
    }


    public struct PageEntryAlphaChannelData
    {
        /// <summary>
        ///     Page entry's status flag (i.e., mapped, unmapped, or homogeneous)
        /// </summary>
        public PageEntryFlag flag;

        /// <summary>
        ///     Page entry's minimum data value (useful for determining its homogeneity)
        /// </summary>
        public byte min;

        /// <summary>
        ///     Page entry's maximum data value (useful for determining its homogeneity)
        /// </summary>
        public byte max;
    }

    [RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(MeshFilter))]
    public class VolumetricObject : MonoBehaviour
    {

        /////////////////////////////////
        // IN-CORE DVR SHADER IDs
        /////////////////////////////////
        private readonly int SHADER_BRICK_CACHE_TEX_ID = Shader.PropertyToID("_BrickCache");
        private readonly int SHADER_TFTEX_ID = Shader.PropertyToID("_TFColors");
        private readonly int SHADER_ALPHA_CUTOFF_ID = Shader.PropertyToID("_AlphaCutoff");
        private readonly int SHADER_INITIAL_STEP_SIZE_ID = Shader.PropertyToID("_InitialStepSize");


        /////////////////////////////////
        // OUT-OF-CORE DVR SHADER IDs
        /////////////////////////////////
        private readonly int SHADER_BRICK_REQUESTS_RANDOM_TEX_ID = Shader.PropertyToID("_BrickRequestsRandomTex");
        private readonly int SHADER_BRICK_REQUESTS_RANDOM_TEX_ST_ID = Shader.PropertyToID("_BrickRequestsRandomTex_ST");
        private readonly int SHADER_BRICK_REQUESTS_BUFFER_ID = Shader.PropertyToID("raw_brick_requests");
        private readonly int SHADER_NBR_BRICKS_PER_RES_LVL_ID = Shader.PropertyToID("nbr_bricks_per_res_lvl");
        private readonly int SHADER_RESIDENCY_OCTREE_BUFFER_ID = Shader.PropertyToID("residency_octree");
        private readonly int SHADER_OCTREE_START_DEPTH_ID = Shader.PropertyToID("_OctreeStartDepth");
        private readonly int SHADER_OCTREE_MAX_DEPTH_ID = Shader.PropertyToID("_MaxOctreeDepth");
        private readonly int SHADER_MAX_NBR_BRICK_REQUESTS_PER_RAY_ID = Shader.PropertyToID("_MaxNbrBrickRequestsPerRay");
        private readonly int SHADER_MAX_NBR_BRICK_REQUESTS_PER_FRAME_ID = Shader.PropertyToID("_MaxNbrBrickRequests");
        private readonly int SHADER_BRICK_SIZE_ID = Shader.PropertyToID("_BrickSize");
        private readonly int SHADER_VOLUME_DIMS_ID = Shader.PropertyToID("_VolumeDims");
        private readonly int SHADER_VOLUME_TEXEL_SIZE_ID = Shader.PropertyToID("_VolumeTexelSize");
        private readonly int SHADER_PAGE_DIR_DIMS_ID = Shader.PropertyToID("_PageDirDims");
        private readonly int SHADER_PAGE_DIR_TEX_ID = Shader.PropertyToID("_PageDir");
        private readonly int SHADER_PAGE_DIR_BASE_ID = Shader.PropertyToID("_PageDirBase");
        private readonly int SHADER_BRICK_CACHE_USAGE_ID = Shader.PropertyToID("brick_cache_usage");
        private readonly int SHADER_BRICK_CACHE_DIMS_ID = Shader.PropertyToID("_BrickCacheDims");
        private readonly int SHADER_BRICK_CACHE_NBR_BRICKS = Shader.PropertyToID("_BrickCacheNbrBricks");
        private readonly int SHADER_BRICK_CACHE_VOXEL_SIZE = Shader.PropertyToID("_BrickCacheVoxelSize");
        private readonly int SHADER_LOD_DISTANCES_SQUARED_ID = Shader.PropertyToID("_LODDistancesSquared");
        private readonly int SHADER_MAX_RES_LVL_ID = Shader.PropertyToID("_MaxResLvl");
        private readonly int SHADER_HOMOGENEITY_TOLERANCE_ID = Shader.PropertyToID("_HomogeneityTolerance");


        /////////////////////////////////
        // CONSTANT DEFINES
        /////////////////////////////////
        public static readonly UInt32 INVALID_BRICK_ID = 0x80000000;
        public static readonly UInt32 MAX_ALLOWED_NBR_RESOLUTION_LVLS = 6;
        private const float MM_TO_METERS = 0.001f;


        private VolumetricDataset m_volume_dataset = null;
        public VolumetricDataset GetVolumetricDataset() => m_volume_dataset;

        private ITransferFunction m_transfer_function = null;

        /////////////////////////////////
        // COROUTINES
        /////////////////////////////////
        private Coroutine m_interpolation_method_update;

        /////////////////////////////////
        // PARAMETERS
        /////////////////////////////////
        private RenderingMode m_rendering_mode;
        private Texture2D m_brick_requests_random_tex = null;
        private byte[] m_brick_requests_random_tex_data;
        private int m_tex_plugin_format = (int)TextureSubPlugin.Format.UR8;
        private PipelineParams m_PipelineParams;

        // this is the size of a brick without taking into consideration a potential padding
        private int m_brick_size;
        private int m_brick_size_cubed;

        // this is the actual size of a brick including padding for inter-brick interpolation
        // in case inter-brick interpolation is supported, this will be set to m_brick_size + 2 otherwise it is
        // the same as m_brick_size.
        private int m_actual_brick_size;
        private int m_actual_brick_size_cubed;


        /////////////////////////////////
        // CPU MEMORY BRICK CACHE
        /////////////////////////////////
        private MemoryCache<byte> m_cpu_cache;

        public Material IC_MAT;
        public Material OOC_HYBRID_MAT;
        public Material OOC_PT_MAT;


        /////////////////////////////////
        // OBJECT POOLS
        /////////////////////////////////
        private UnmanagedObjectPool<TextureSubImage3DParams> m_tex_params_pool;


        private readonly ConcurrentQueue<UInt32> m_brick_reply_queue = new();

        private ComputeBuffer m_residency_octree_cb;
        private ResidencyNode[] m_residency_octree_data;
        private ComputeBuffer m_brick_requests_cb;
        private ComputeBuffer m_brick_cache_usage_cb;
        private bool m_is_brick_cache_nativaly_created = false;
        private UInt32[] m_brick_requests_default_data;

        // timestamp to keep for caches LRU eviction scheme. Do not set initially to 0
        // because 0 is reserved for empty slots.
        private UInt64 m_timestamp = 1;


        /////////////////////////////////
        // BRICK CACHE
        /////////////////////////////////
        private Texture3D m_brick_cache = null;
        private Vector3Int m_gpu_brick_cache_size;
        private Vector3Int m_gpu_brick_cache_nbr_bricks;
        private UInt32 m_brick_cache_texture_id = 0;
        private IntPtr m_brick_cache_ptr = IntPtr.Zero;
        private float m_brick_cache_size_mb;
        private Vector4 m_brick_cache_voxel_size;

        /////////////////////////////////
        // BRICK CACHE USAGE
        /////////////////////////////////
        struct BrickCacheUsage
        {
            public Int32 brick_cache_idx;
            public UInt32 brick_id;
            public UInt64 timestamp;
            public byte brick_min;
            public byte brick_max;
        }

        /// <summary>
        ///     Holds CPU-side unsorted brick cache usage information.
        /// </summary>
        private BrickCacheUsage[] m_brick_cache_usage;

        /// <summary>
        ///     Holds CPU-side sorted brick cache usage information.
        /// </summary>
        private BrickCacheUsage[] m_brick_cache_usage_sorted;

        /// <summary>
        ///     Holds temporary brick cache usage data retrieved from the GPU.
        /// </summary>
        private UInt32[] m_brick_cache_usage_tmp;

        /// <summary>
        ///    GPU-side brick cache usage default buffer data. This is used to reset
        ///    the GPU-side brick cache usage buffer before the start of next frame
        ///    rendering.
        /// </summary>
        private UInt32[] m_brick_cache_usage_default_data;


        /////////////////////////////////
        // PAGE TABLES
        /////////////////////////////////
        private Texture3D m_page_dir = null;

        /// <summary>
        ///     Each entry is defined over 4 32-bit floats:
        ///         0 =>    if flag == MAPPED: normalized position x in the brick cache
        ///                 else: holds garbage
        ///         1 =>    if flag == MAPPED: normalized position y in the brick cache
        ///                 else: holds garbage
        ///         2 =>    if flag == MAPPED: normalized position z in the brick cache
        ///                 else: holds garbage
        ///         3 =>    1st byte holds flag
        ///                 if flag != UNMAPPED: 2nd byte holds min and 3nd byte holds max
        /// </summary>
        private int m_page_dir_stride = 4;

        private float[] m_page_dir_data;
        private Vector4[] m_page_dir_base;
        private Vector4[] m_page_dir_dims;

        /// <summary>
        ///     If the homogeneity tolerance changes, then this dirty flag should be set so that all the entries
        ///     of the page table have to be checked against the new homogeneity tolerance.     
        /// </summary>
        private bool m_pt_requires_homogeneity_update = false;

        /// <summary>
        ///     If this is set to true, the CPU page table data has to be uploaded to the GPU.
        /// </summary>
        private bool m_page_dir_dirty = true;

        /////////////////////////////////
        // OPTIMIZATION STRUCTURES
        /////////////////////////////////

        /// <summary>
        ///     This is only used to avoid per-frame heap allocations and to speak up residency
        ///     octree updates.
        /// </summary>
        HashSet<int> m_octree_changed_node_indices;

        HashSet<UInt32> m_brick_cache_brick_residency;
        Queue<GameObject> m_brick_wireframes_pool;
        List<GameObject> m_brick_wireframes_in_use;

        /////////////////////////////////
        // CACHED COMPONENTS
        /////////////////////////////////
        private Transform m_transform;
        private Material m_material;
        private CVDSMetadata m_metadata;
        private Vector4[] m_nbr_bricks_per_res_lvl;
        private bool m_vis_params_dirty = false;
        private Vector4[] m_VolumeDimsPerResLvl;


        /////////////////////////////////
        // DEBUGGING
        /////////////////////////////////
        public GameObject m_BrickWireframePrefab;
        public bool InstantiateBrickWireframes = false;
        public bool ForceNativeTextureCreation = true;
        private Mesh m_wireframe_cube_mesh;


        /////////////////////////////////
        // EVENTS
        /////////////////////////////////
        public static event Action OnNoMoreBrickRequests;
        public static event Action<float, long> OnInCoreAllBricksLoadedToCPUCache;
        public static event Action<float, long> OnInCoreAllBricksLoadedToGPUCache;


        private float m_alpha_cutoff;
        private float m_sampling_quality_factor;
        private float[] m_lod_distances_squared = new float[MAX_ALLOWED_NBR_RESOLUTION_LVLS];
        private byte m_homogeneity_tolerance;

        private bool m_IsAlreadyInitialized = false;


        private void Awake()
        {
            m_transform = GetComponent<Transform>();
        }


        private Vector3Int GetGPUBrickCacheDims(int size_mbs, int brick_size)
        {
            if (size_mbs >= ((double)SystemInfo.maxTexture3DSize * SystemInfo.maxTexture3DSize * SystemInfo.maxTexture3DSize) / (1024.0 * 1024.0))
            {
                throw new Exception("provided brick cache size is greater or equal to the size of the maximal allowed 3D texture on the GPU");
            }

            float brick_size_mbs = (brick_size * brick_size * brick_size) / (1024.0f * 1024.0f);
            int x = 1;
            int y = 1;
            int z = 1;
            float accm_size_mbs = 0;

            while (accm_size_mbs < size_mbs)
            {
                // start by increasing along the X axis - each increase adds a YZ plane of bricks
                if (x < SystemInfo.maxTexture3DSize)
                {
                    ++x;
                    accm_size_mbs += brick_size_mbs * (y * z);
                }

                if (accm_size_mbs >= size_mbs)
                    break;

                // then increase along the Y axis - each increase adds an XZ plane of bricks
                if (y < SystemInfo.maxTexture3DSize)
                {
                    ++y;
                    accm_size_mbs += brick_size_mbs * (x * z);
                }

                if (accm_size_mbs >= size_mbs)
                    break;

                // then increase along the Z axis - each increase adds an XY plane of bricks
                if (z < SystemInfo.maxTexture3DSize)
                {
                    ++z;
                    accm_size_mbs += brick_size_mbs * (x * y);
                }
            }
            return new Vector3Int(
                x * brick_size,
                y * brick_size,
                z * brick_size
            );
        }


        public void Init(VolumetricDataset volumetricDataset, PipelineParams pipelineParams, DebugginParams debuggingParams)
        {
            if (m_IsAlreadyInitialized)
                throw new Exception("volumetric object is already initialized! Use a new instance instead.");

            if (volumetricDataset == null)
                throw new ArgumentNullException("the provided volumetric dataset should be a non-null reference");

            m_metadata = volumetricDataset.Metadata;
            m_PipelineParams = pipelineParams;
            m_brick_size = pipelineParams.BrickSize;
            m_brick_size_cubed = m_brick_size * m_brick_size * m_brick_size;
            m_actual_brick_size = m_brick_size;
            if (m_metadata.ChunkPadding)
            {
                m_actual_brick_size += 2;
            }
            m_actual_brick_size_cubed = m_actual_brick_size * m_actual_brick_size * m_actual_brick_size;
            m_rendering_mode = pipelineParams.RenderingMode;
            m_volume_dataset = volumetricDataset;

            if (debuggingParams.RandomSeedValid)
            {
                UnityEngine.Random.InitState(debuggingParams.RandomSeed);
            }

            // set debuggin parameters
            InstantiateBrickWireframes = debuggingParams.BrickWireframes;
            if (debuggingParams.Benchmark)
            {
                switch (pipelineParams.RenderingMode)
                {
                    case RenderingMode.OOC_PT:
                    case RenderingMode.OOC_HYBRID:
                    {
                        gameObject.AddComponent(typeof(OOCBenchmarkSetup));
                        break;
                    }

                    case RenderingMode.IC:
                    {
                        gameObject.AddComponent(typeof(ICBenchmarkSetup));
                        break;
                    }
                }
            }

            // set volume dimensions
            m_VolumeDimsPerResLvl = new Vector4[m_metadata.NbrResolutionLvls];
            for (int i = 0; i < m_VolumeDimsPerResLvl.Length; ++i)
            {
                m_VolumeDimsPerResLvl[i] = new Vector4(Mathf.Ceil(m_metadata.Dims.x / (float)(1 << i)),
                    Mathf.Ceil(m_metadata.Dims.y / (float)(1 << i)),
                    Mathf.Ceil(m_metadata.Dims.z / (float)(1 << i)));
            }

            // debugging stuff ...
            m_wireframe_cube_mesh = WireframeCubeMesh.GenerateMesh();

            if (m_rendering_mode == RenderingMode.IC)
            {
                m_cpu_cache = new MemoryCache<byte>(pipelineParams.CPUBrickCacheSizeMBs, m_brick_size_cubed);

                // assign material
                m_material = GetComponent<Renderer>().material = IC_MAT;

                // initialize the brick cache
                m_gpu_brick_cache_size = new Vector3Int(
                    m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].x * m_metadata.ChunkSize,
                    m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].y * m_metadata.ChunkSize,
                    m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].z * m_metadata.ChunkSize
                );

                m_gpu_brick_cache_nbr_bricks = new(m_gpu_brick_cache_size.x / m_brick_size,
                    m_gpu_brick_cache_size.y / m_brick_size, m_gpu_brick_cache_size.z / m_brick_size);

                // enable the progress bar
                ProgressHandlerEvents.OnRequestActivate?.Invoke(true);
                Task t = Task.Run(() =>
                    {
                        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

                        Importer.LoadAllBricksIntoCache(m_volume_dataset.Metadata, m_brick_size, m_PipelineParams.InCoreMaxResolutionLvl,
                            m_cpu_cache, m_brick_reply_queue, m_PipelineParams.MaxNbrImporterThreads);

                        stopwatch.Stop();
                        float elapsed = stopwatch.ElapsedMilliseconds;
#if DEBUG
                        Debug.Log($"uploading to host memory cache took: {elapsed / 1000.0f}s");
#endif
                        long total_nbr_bricks = m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].x *
                            m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].y *
                            m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].z *
                            (int)Math.Pow(m_metadata.ChunkSize / m_brick_size, 3);
                        OnInCoreAllBricksLoadedToCPUCache?.Invoke(elapsed, total_nbr_bricks);
                    });
                t.ContinueWith(t =>
                {
#if DEBUG
                    Debug.LogException(t.Exception);
#endif
                }, TaskContinuationOptions.OnlyOnFaulted);

                // scale mesh to match correct dimensions of the original volumetric data
                m_transform.localScale = new Vector3(
                     MM_TO_METERS * m_metadata.VoxelDims.x * m_gpu_brick_cache_size.x,
                     MM_TO_METERS * m_metadata.VoxelDims.y * m_gpu_brick_cache_size.y,
                     MM_TO_METERS * m_metadata.VoxelDims.z * m_gpu_brick_cache_size.z
                );

                // finally start the loop
                StartCoroutine(InCoreLoop());
            }
            else if (m_rendering_mode == RenderingMode.OOC_PT || m_rendering_mode == RenderingMode.OOC_HYBRID)
            {
                m_cpu_cache = new MemoryCache<byte>(pipelineParams.CPUBrickCacheSizeMBs, m_actual_brick_size_cubed);

                // clear any previously set UAVs
                Graphics.ClearRandomWriteTargets();

                m_gpu_brick_cache_size = GetGPUBrickCacheDims(m_PipelineParams.GPUBrickCacheSizeMBs, m_actual_brick_size);

                m_gpu_brick_cache_nbr_bricks = new(m_gpu_brick_cache_size.x / m_actual_brick_size,
                    m_gpu_brick_cache_size.y / m_actual_brick_size, m_gpu_brick_cache_size.z / m_actual_brick_size);

                // assign the material
                m_material = GetComponent<Renderer>().material = m_rendering_mode == RenderingMode.OOC_PT ? OOC_PT_MAT : OOC_HYBRID_MAT;

                // initialize the brick requests buffer and data
                InitializeBrickRequestsBuffer();

                // initialize the brick cache usage buffer
                InitializeBrickCacheUsage();

                // initialize brick requests random texture
                InitializeBrickRequestsRandomTex();

                // initialize the page table(s)
                InitializePageDirectory();

                SetOOCShaderProperties();

                ScaleOOCMesh();

                InitializeBrickWireframesPool();

                if (m_rendering_mode == RenderingMode.OOC_PT)
                {
                    // finally start the loop
                    StartCoroutine(OOCPTLoop());
                }
                else // m_rendering_mode == RenderingMode.OCC_HYBRID
                {
                    m_brick_cache_brick_residency = new HashSet<UInt32>();

                    // load and initialize the residency octree compute buffer
                    InitializeResidencyOctree();

                    // finally start the loop
                    StartCoroutine(OOCHybridLoop());
                }
            }


            // avoid overflow errors
            m_brick_cache_size_mb = (m_gpu_brick_cache_size.x / 1024.0f) * (m_gpu_brick_cache_size.y / 1024.0f)
                * m_gpu_brick_cache_size.z;

#if DEBUG
            // log useful info
            Debug.Log($"rendering mode set to: {m_rendering_mode}");
            Debug.Log($"number of frames in flight: {QualitySettings.maxQueuedFrames}");
            Debug.Log($"brick cache size dimensions: {m_gpu_brick_cache_size}");
            Debug.Log($"brick cache size: {m_brick_cache_size_mb}MB");
#endif
            // initialize object pools
            m_tex_params_pool = new(m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame);

            // rotate the volume according to provided Euler angles
            m_transform.localRotation = Quaternion.Euler(m_metadata.EulerRotation);

            StartCoroutine(InternalInit());

            m_IsAlreadyInitialized = true;
        }


        public PipelineParams GetPipelineParams()
        {
            if (!m_IsAlreadyInitialized)
            {
                throw new Exception("attempt at retrieving pipeline parameters before they are set");
            }

            return m_PipelineParams;
        }


        private void CreateBrickCacheTexture3D()
        {
            m_brick_cache = new Texture3D(m_gpu_brick_cache_size.x, m_gpu_brick_cache_size.y, m_gpu_brick_cache_size.z,
                 TextureFormat.R8, mipChain: false, createUninitialized: false);  // TODO: set back to true

            // set texture wrapping to Clamp to remove edge/face artifacts
            m_brick_cache.wrapModeU = TextureWrapMode.Clamp;
            m_brick_cache.wrapModeV = TextureWrapMode.Clamp;
            m_brick_cache.wrapModeW = TextureWrapMode.Clamp;

            m_brick_cache.filterMode = FilterMode.Bilinear;

            m_brick_cache_ptr = m_brick_cache.GetNativeTexturePtr();

            Assert.AreNotEqual(m_brick_cache_ptr, IntPtr.Zero);
        }

        private IEnumerator CreateNativeBrickCacheTexture3D()
        {
            // make sure that you do not create a resource during a render pass
            yield return new WaitForEndOfFrame();

            CommandBuffer cmd_buffer = new();
            CreateTexture3DParams args = new()
            {
                texture_id = m_brick_cache_texture_id,
                width = (UInt32)m_gpu_brick_cache_size.x,
                height = (UInt32)m_gpu_brick_cache_size.y,
                depth = (UInt32)m_gpu_brick_cache_size.z,
                format = m_tex_plugin_format,
            };
            IntPtr args_ptr = Marshal.AllocHGlobal(Marshal.SizeOf<CreateTexture3DParams>());
            Marshal.StructureToPtr(args, args_ptr, false);
            cmd_buffer.IssuePluginEventAndData(API.GetRenderEventFunc(),
                (int)TextureSubPlugin.Event.CreateTexture3D, args_ptr);
            Graphics.ExecuteCommandBuffer(cmd_buffer);

            // removing this crashes this whole crap ...
            yield return new WaitForEndOfFrame();

            Marshal.FreeHGlobal(args_ptr);

            m_brick_cache_ptr = API.RetrieveCreatedTexture3D(m_brick_cache_texture_id);
            if (m_brick_cache_ptr == IntPtr.Zero)
            {
                throw new NullReferenceException("native bricks cache pointer is nullptr " +
                    "make sure that your platform supports native code plugins");
            }

            m_brick_cache = Texture3D.CreateExternalTexture(m_gpu_brick_cache_size.x, m_gpu_brick_cache_size.y,
                m_gpu_brick_cache_size.z, TextureFormat.R8, mipChain: false, nativeTex: m_brick_cache_ptr);

            // this has to be overwritten for Vulkan to work because Unity expects a VkImage* for the nativeTex
            // paramerter not a VkImage. GetNativeTexturePtr does not actually return a VkImage* as it claims
            // but rather a VkImage => This is probably a bug.
            // (see https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture3D.CreateExternalTexture.html)
            m_brick_cache_ptr = m_brick_cache.GetNativeTexturePtr();

            m_is_brick_cache_nativaly_created = true;
        }

        private void InitializeBrickRequestsBuffer()
        {
            m_brick_requests_cb = new ComputeBuffer(m_PipelineParams.MaxNbrBrickRequestsPerFrame, sizeof(UInt32));

            m_brick_requests_default_data = new UInt32[m_PipelineParams.MaxNbrBrickRequestsPerFrame];
            for (int i = 0; i < m_brick_requests_default_data.Length; ++i)
            {
                m_brick_requests_default_data[i] = INVALID_BRICK_ID;
            }

            m_material.SetBuffer(SHADER_BRICK_REQUESTS_BUFFER_ID, m_brick_requests_cb);

            Graphics.SetRandomWriteTarget(1, m_brick_requests_cb, true);

            GPUResetBrickRequests();
#if DEBUG
            Debug.Log("brick requests buffer successfully initialized");
#endif
        }


        private void InitializeBrickCacheUsage()
        {
            int nbr_bricks = m_gpu_brick_cache_nbr_bricks.x * m_gpu_brick_cache_nbr_bricks.y * m_gpu_brick_cache_nbr_bricks.z;
            int brick_cache_usage_size = Mathf.CeilToInt(nbr_bricks / 32.0f);

            m_brick_cache_usage_cb = new ComputeBuffer(brick_cache_usage_size, sizeof(UInt32));
            m_brick_cache_usage_default_data = new UInt32[brick_cache_usage_size];
            m_brick_cache_usage_tmp = new UInt32[brick_cache_usage_size];

            m_brick_cache_usage_sorted = new BrickCacheUsage[nbr_bricks];
            m_brick_cache_usage = new BrickCacheUsage[nbr_bricks];
            for (int i = 0; i < brick_cache_usage_size; ++i)
            {
                m_brick_cache_usage_default_data[i] = 0u;
                for (int j = 0; (j < 32) && (i * 32 + j < m_brick_cache_usage.Length); ++j)
                {
                    int brick_idx = i * 32 + j;
                    m_brick_cache_usage[brick_idx] = new BrickCacheUsage()
                    {
                        brick_id = INVALID_BRICK_ID,    // invalid brick ID => free slot
                        brick_cache_idx = brick_idx,
                        timestamp = 0,                  // 0 so that when sorted, free slots are placed first
                        brick_min = 0,
                        brick_max = 0
                    };
                }
            }
            m_material.SetBuffer(SHADER_BRICK_CACHE_USAGE_ID, m_brick_cache_usage_cb);
            Graphics.SetRandomWriteTarget(2, m_brick_cache_usage_cb, true);
            GPUResetBrickCacheUsage();
#if DEBUG
            Debug.Log($"brick cache usage buffer elements count: {brick_cache_usage_size}");
            Debug.Log($"brick cache usage size: {brick_cache_usage_size * sizeof(UInt32) / 1024.0f} KB");
            Debug.Log("brick cache usage successfully initialized");
#endif
        }


        private void InitializeResidencyOctree()
        {
            // import the residency octree from filesystem
            m_residency_octree_data = Importer.ImportResidencyOctree(m_volume_dataset.Metadata);

            m_octree_changed_node_indices = new HashSet<int>();

            int residency_octree_nodes_count = (int)((Mathf.Pow(8, m_PipelineParams.OctreeMaxDepth + 1) - 1) / 7);
            m_residency_octree_cb = new ComputeBuffer(residency_octree_nodes_count,
                Marshal.SizeOf<ResidencyNode>(), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            m_material.SetBuffer(SHADER_RESIDENCY_OCTREE_BUFFER_ID, m_residency_octree_cb);
            m_material.SetInteger(SHADER_OCTREE_START_DEPTH_ID, m_PipelineParams.OctreeStartDepth);
            m_material.SetInteger(SHADER_OCTREE_MAX_DEPTH_ID, m_PipelineParams.OctreeMaxDepth);

            GPUUpdateResidencyOctree(new List<BrickCacheUsage>(), new List<BrickCacheUsage>());
#if DEBUG
            Debug.Log($"residency octree node struct size: {Marshal.SizeOf<ResidencyNode>()} bytes");
            Debug.Log($"max residency octree depth: {m_PipelineParams.OctreeMaxDepth}");
            Debug.Log("residency octree loaded successfully");
#endif
        }


        private void InitializeBrickRequestsRandomTex()
        {
            m_brick_requests_random_tex = new Texture2D(m_PipelineParams.BrickRequestsRandomTexSize, m_PipelineParams.BrickRequestsRandomTexSize,
                TextureFormat.R8, mipChain: false, linear: true, createUninitialized: true);

            m_brick_requests_random_tex.wrapModeU = TextureWrapMode.Repeat;
            m_brick_requests_random_tex.wrapModeV = TextureWrapMode.Repeat;

            m_brick_requests_random_tex.filterMode = FilterMode.Point;

            m_brick_requests_random_tex_data = new byte[m_brick_requests_random_tex.width * m_brick_requests_random_tex.height];
            m_material.SetTexture(SHADER_BRICK_REQUESTS_RANDOM_TEX_ID, m_brick_requests_random_tex);

            for (int i = 0; i < m_brick_requests_random_tex_data.Length; ++i)
            {
                // we want random number from [0, 254] because 255 causes the normalized value to be 1.0 which
                // breaks array indexing in the out-of-core DVR shader
                m_brick_requests_random_tex_data[i] = (byte)UnityEngine.Random.Range(0, 255);
            }
            m_brick_requests_random_tex.SetPixelData(m_brick_requests_random_tex_data, 0);
            m_brick_requests_random_tex.Apply();
#if DEBUG
            Debug.Log("brick requests random texture successfully initialized");
#endif
        }


        /// <summary>
        ///     Initializes the top level page directory.
        /// </summary>
        /// 
        /// <exception cref="Exception">
        ///     thrown when the target platform does not support RGBAFloat texture format
        /// </exception>
        private void InitializePageDirectory()
        {
            m_page_dir_base = new Vector4[m_metadata.NbrResolutionLvls];
            m_page_dir_dims = new Vector4[m_metadata.NbrResolutionLvls];

            int accm_x = 0;
            for (int i = 0; i < m_metadata.NbrResolutionLvls; ++i)
            {
                m_page_dir_dims[i] = new Vector4(
                    Mathf.Ceil(m_metadata.Dims.x / (float)(m_brick_size << i)),
                    Mathf.Ceil(m_metadata.Dims.y / (float)(m_brick_size << i)),
                    Mathf.Ceil(m_metadata.Dims.z / (float)(m_brick_size << i))
                );

                m_page_dir_base[i] = new Vector4(
                    accm_x,
                    (int)m_page_dir_dims[0].y - (int)m_page_dir_dims[i].y,
                    0
                );

                accm_x += (int)m_page_dir_dims[i].x;
            }

            Vector3Int page_dir_dims = new(accm_x, (int)m_page_dir_dims[0].y, (int)m_page_dir_dims[0].z);

            // initialize CPU-side page table(s) data
            int page_dir_data_size = page_dir_dims.x * page_dir_dims.y * page_dir_dims.z * 4;
#if DEBUG
            Debug.Log($"page dir [x, y, z]: [{page_dir_dims.x}, {page_dir_dims.y}, {page_dir_dims.z}]");
            Debug.Log($"page dir total number of page entries: {page_dir_dims.x * page_dir_dims.y * page_dir_dims.z}");
#endif
            m_page_dir_data = new float[page_dir_data_size];
            // set the alpha components to UNMAPPED
            for (int i = 0; i < page_dir_data_size; i += 4)
            {
                SetPageEntryAlphaChannelData(i, PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY);
            }

            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
            {
                throw new Exception("your system does not support RGBAFloat - float32 per channel texture format.");
            }

            m_page_dir = new Texture3D(page_dir_dims.x, page_dir_dims.y, page_dir_dims.z,
                TextureFormat.RGBAFloat, mipChain: false, createUninitialized: true);

            // set this according to the used texture format
            m_page_dir_stride = 4;

            m_page_dir.wrapModeU = TextureWrapMode.Clamp;
            m_page_dir.wrapModeV = TextureWrapMode.Clamp;
            m_page_dir.wrapModeW = TextureWrapMode.Clamp;

            // set shader properties
            m_material.SetVectorArray(SHADER_PAGE_DIR_BASE_ID, m_page_dir_base);
            m_material.SetVectorArray(SHADER_PAGE_DIR_DIMS_ID, m_page_dir_dims);
            m_material.SetTexture(SHADER_PAGE_DIR_TEX_ID, m_page_dir);

            // load the initial page table texture data into the GPU
            m_page_dir.SetPixelData(m_page_dir_data, mipLevel: 0);
            m_page_dir.Apply();
        }

        private void SetOOCShaderProperties()
        {
            // create an array to hold nbr bricks per resolution level
            m_nbr_bricks_per_res_lvl = new Vector4[m_metadata.NbrResolutionLvls];
            for (int i = 0; i < m_nbr_bricks_per_res_lvl.Length; ++i)
            {
                int r = m_metadata.ChunkSize / m_brick_size;
                m_nbr_bricks_per_res_lvl[i] = new Vector4(m_metadata.NbrChunksPerResolutionLvl[i].x,
                    m_metadata.NbrChunksPerResolutionLvl[i].y,
                    m_metadata.NbrChunksPerResolutionLvl[i].z) * r;
            }
            m_material.SetVectorArray(SHADER_NBR_BRICKS_PER_RES_LVL_ID, m_nbr_bricks_per_res_lvl);

            m_material.SetVectorArray(SHADER_VOLUME_DIMS_ID, m_VolumeDimsPerResLvl);

            Vector3 volume_texel_size = new(1.0f / m_metadata.Dims.x,
                        1.0f / m_metadata.Dims.y, 1.0f / m_metadata.Dims.z);
            m_material.SetVector(SHADER_VOLUME_TEXEL_SIZE_ID, volume_texel_size);

            // set remaining shader variables
            m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_PER_RAY_ID, m_PipelineParams.MaxNbrBrickRequestsPerRay);
            m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_PER_FRAME_ID, m_PipelineParams.MaxNbrBrickRequestsPerFrame);
            m_material.SetInteger(SHADER_BRICK_SIZE_ID, m_brick_size);
            m_material.SetInteger(SHADER_MAX_RES_LVL_ID, m_metadata.NbrResolutionLvls - 1);

            m_brick_cache_voxel_size = new(1.0f / m_gpu_brick_cache_size.x,
                        1.0f / m_gpu_brick_cache_size.y, 1.0f / m_gpu_brick_cache_size.z);
            m_material.SetVector(SHADER_BRICK_CACHE_VOXEL_SIZE, m_brick_cache_voxel_size);

            m_material.SetVector(SHADER_BRICK_CACHE_DIMS_ID, new Vector3(m_gpu_brick_cache_size.x,
                m_gpu_brick_cache_size.y, m_gpu_brick_cache_size.z));

            Vector3 brick_cache_nbr_bricks = new(m_gpu_brick_cache_nbr_bricks.x,
                        m_gpu_brick_cache_nbr_bricks.y, m_gpu_brick_cache_nbr_bricks.z);
            m_material.SetVector(SHADER_BRICK_CACHE_NBR_BRICKS, brick_cache_nbr_bricks);
        }

        private void ScaleOOCMesh()
        {
            // scale mesh to match correct dimensions of the original volumetric data
            m_transform.localScale = new Vector3(
                 MM_TO_METERS * m_metadata.VoxelDims.x * m_metadata.Dims.x,
                 MM_TO_METERS * m_metadata.VoxelDims.y * m_metadata.Dims.y,
                 MM_TO_METERS * m_metadata.VoxelDims.z * m_metadata.Dims.z
            );
        }


        private void InitializeBrickWireframesPool()
        {
            int max_nbr_brick_wireframes = m_gpu_brick_cache_nbr_bricks.x * m_gpu_brick_cache_nbr_bricks.y * m_gpu_brick_cache_nbr_bricks.z;
            m_brick_wireframes_pool = new(max_nbr_brick_wireframes);
            m_brick_wireframes_in_use = new(max_nbr_brick_wireframes);
            for (int i = 0; i < max_nbr_brick_wireframes; ++i)
            {
                var go = GameObject.Instantiate(m_BrickWireframePrefab, gameObject.transform, false);
                go.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
                go.SetActive(false);
                m_brick_wireframes_pool.Enqueue(go);
            }
        }


        private IEnumerator InternalInit()
        {
#if DEBUG
            Debug.Log("VolumetricObject: waiting for volume dataset and transfer function to be non null ...");
#endif
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null));
#if DEBUG
            Debug.Log("VolumetricObject: started internal init");
#endif
            // create the brick cache texture(s) natively in case of OpenGL/Vulkan to overcome
            // the 2GBs Unity/.NET Texture3D size limit. For Direct3D11/12 we don't have to create the textures
            // using the native plugin since these APIs already impose a 2GBs per-resource limit
            //
            // Important: if the texture is created using Unity's Texture3D with createUninitialized set to true
            // and you try to visualize some uninitailized blocks you might observe some artifacts (duh?!)
            if (ForceNativeTextureCreation)
            {
#if DEBUG
                Debug.Log("forcing native 3D texture creation");
#endif
                yield return CreateNativeBrickCacheTexture3D();
            }
            else if (m_brick_cache_size_mb < 2048)
            {
#if DEBUG
                Debug.Log($"requested brick cache size {m_brick_cache_size_mb}MB is less than 2GB."
                    + " Using Unity's API to create the 3D texture");
#endif
                CreateBrickCacheTexture3D();
            }
            else
            {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan &&
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore &&
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3)
                    throw new NotImplementedException("multiple 3D texture brick caches are not supported."
                        + " Choose a smaller than 2GB brick cache or use a different graphics API (e.g., Vulkan/OpenGLCore)");
                yield return CreateNativeBrickCacheTexture3D();
            }

            m_material.SetTexture(SHADER_BRICK_CACHE_TEX_ID, m_brick_cache);
        }


        private void OnEnable()
        {
            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange += OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODDistancesChange += OnModelLODDistancesChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange += OnModelHomogeneityChange;
        }

        private void OnDisable()
        {
            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange -= OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODDistancesChange -= OnModelLODDistancesChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange -= OnModelHomogeneityChange;

            // clear UAV targets
            Graphics.ClearRandomWriteTargets();

            if (m_brick_requests_cb != null)
            {
                m_brick_requests_cb.Release();
                m_brick_requests_cb.Dispose();
                m_brick_requests_cb = null;
            }

            // avoid GPU resources leak ...
            if (m_is_brick_cache_nativaly_created)
            {
                // create a command buffer in which graphics commands will be submitted
                CommandBuffer cmd_buffer = new();

                DestroyTexture3DParams args = new ()
                {
                    texture_id = m_brick_cache_texture_id,
                };

                IntPtr p_args = Marshal.AllocHGlobal(Marshal.SizeOf<CreateTexture3DParams>());
                Marshal.StructureToPtr(args, p_args, false);
                cmd_buffer.IssuePluginEventAndData(TextureSubPlugin.API.GetRenderEventFunc(),
                    (int)TextureSubPlugin.Event.DestroyTexture3D, p_args);

                // execute the command buffer immediately
                Graphics.ExecuteCommandBuffer(cmd_buffer);

                // yeah ... idk, couldn't find anything better ...
                System.Threading.Thread.Sleep(1000);

                Marshal.FreeHGlobal(p_args);
            }

            StopAllCoroutines();
        }


        /// <summary>
        ///     In-core approach that loads all bricks into the 3D texture. Use this when the whole
        ///     3D dataset can fit into a GPU's 3D texture resource.
        /// </summary>
        /// 
        /// <remark>
        ///     Note that the limitations for this may depend on the graphics API used. For instance,
        ///     for D3D11/12 you can't create a 3D texture larger than 2GBs.
        /// </remark>
        /// 
        /// <exception cref="NotImplementedException">
        ///     Thrown in case ColorDepth is neither UR8 nor UR16.
        /// </exception>
        public IEnumerator InCoreLoop()
        {
            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null)
                && (m_brick_cache != null));

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
#if DEBUG
            Debug.Log("started loading all volume bricks into GPU ...");
#endif

            long nbr_bricks_uploaded = 0;

            long total_nbr_bricks = m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].x *
                m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].y *
                m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].z *
                (int)Math.Pow(m_metadata.ChunkSize / m_brick_size, 3);

            int nbr_bricks_uploaded_per_frame = 0;

            CommandBuffer cmd_buffer = new();

            GCHandle[] handles = new GCHandle[m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame];

            Vector3 brick_scale = new(
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].x * m_metadata.ChunkSize),
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].y * m_metadata.ChunkSize),
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].z * m_metadata.ChunkSize)
            );

            while (nbr_bricks_uploaded < total_nbr_bricks)
            {
                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                GPUUpdateVisualizationParams();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {
                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned to a
                    // fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);
                    Assert.IsNotNull(brick);

                    if (brick.data == null)
                    {
                        byte[] data = new byte[m_brick_size_cubed];
                        for (int i = 0; i < m_brick_size_cubed; ++i)
                        {
                            data[i] = brick.min;
                        }
                        handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(data, GCHandleType.Pinned);
                    }
                    else
                    {
                        handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                    }


                    // compute where the brick offset within the brick cache
                    m_volume_dataset.ComputeVolumeOffset(brick_id, m_brick_size, out Int32 x, out Int32 y, out Int32 z);

                    // allocate the plugin call's arguments struct
                    TextureSubImage3DParams args = new()
                    {
                        texture_handle = m_brick_cache_ptr,
                        xoffset = x,
                        yoffset = y,
                        zoffset = z,
                        width = m_brick_size,
                        height = m_brick_size,
                        depth = m_brick_size,
                        data_ptr = handles[nbr_bricks_uploaded_per_frame].AddrOfPinnedObject(),
                        level = 0,
                        format = m_tex_plugin_format
                    };
                    m_tex_params_pool.Acquire(args, out IntPtr arg_ptr);
                    cmd_buffer.IssuePluginEventAndData(API.GetRenderEventFunc(), (int)TextureSubPlugin.Event.TextureSubImage3D,
                        arg_ptr);

                    if (InstantiateBrickWireframes)
                    {
                        GameObject brick_wireframe = Instantiate(m_BrickWireframePrefab, gameObject.transform, false);
                        brick_wireframe.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
                        brick_wireframe.transform.localPosition = new Vector3(
                            (x / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].x * m_metadata.ChunkSize) - 0.5f) + brick_scale.x / 2.0f,
                            (y / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].y * m_metadata.ChunkSize) - 0.5f) + brick_scale.y / 2.0f,
                            (z / (float)(m_metadata.NbrChunksPerResolutionLvl[m_PipelineParams.InCoreMaxResolutionLvl].z * m_metadata.ChunkSize) - 0.5f) + brick_scale.z / 2.0f
                        );
                        brick_wireframe.transform.localScale = brick_scale;
                        brick_wireframe.name = $"brick_{brick_id & 0x03FFFFFF}_res_lvl_{brick_id >> 26}";
                    }
                    ++nbr_bricks_uploaded_per_frame;

                }  // END WHILE

                // we execute the command buffer instantly
                Graphics.ExecuteCommandBuffer(cmd_buffer);
                cmd_buffer.Clear();

            }  // END WHILE

            float elapsed = stopwatch.ElapsedMilliseconds;
#if DEBUG
            Debug.Log($"uploading all {total_nbr_bricks} bricks to GPU took: {elapsed / 1000.0f}s");
#endif
            OnInCoreAllBricksLoadedToGPUCache?.Invoke(elapsed, total_nbr_bricks);

            while (true)
            {
                yield return new WaitForEndOfFrame();
                GPUUpdateVisualizationParams();
            }
        }


        /// <summary>
        ///     Retrieves brick requests from the GPU. Retreived brick IDs may not be unique
        ///     especially with large brick sizes and small brick request random textures.
        ///     The filled-up brick requests array should be filtered before proceding to
        ///     importing the actual bricks/chunks.
        /// </summary>
        /// 
        /// <param name="brick_requests">
        ///     array that will be overwritten with brick requests retrieved from the GPU.
        ///     Array should have the length of MaxNbrBrickRequestsPerFrame.
        /// </param>
        private void GPUGetBrickRequests(UInt32[] brick_requests)
        {
            m_brick_requests_cb.GetData(brick_requests);
        }


        /// <summary>
        ///     Filters raw brick requests fetched from the GPU by removing duplicates, 
        ///     invalid brick IDs, requests that are already in flight (i.e., already
        ///     dispatched and being processed), and IDs that are already mapped.
        /// </summary>
        /// 
        /// <param name="raw_brick_requests">
        ///     Array of raw brick requests retrieved from the GPU.
        /// </param>
        /// 
        /// <param name="filtered_brick_requests">
        ///     Set that will be filled with filtered brick requests. Ideally, the set
        ///     should be initialized with the same capacity as the supplied
        ///     <paramref name="raw_brick_requests"/>.
        /// </param>
        private void FilterBrickRequests(UInt32[] raw_brick_requests, HashSet<UInt32> filtered_brick_requests)
        {
            filtered_brick_requests.Clear();
            foreach (UInt32 brick_id in raw_brick_requests)
            {
                if (brick_id == INVALID_BRICK_ID)
                {
                    continue;
                }
                PageEntryFlag page_entry_flag = ExtractPageEntryAlphaChannelData(GetPageTableIndex(brick_id)).flag;
                // cleanup brick requests
                if (!m_cpu_cache.Contains(brick_id) && !m_in_flight_brick_imports.ContainsKey(brick_id)
                    && page_entry_flag == PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY)
                {
                    filtered_brick_requests.Add(brick_id);
                }
                else if (m_cpu_cache.Contains(brick_id) && !m_in_flight_brick_imports.ContainsKey(brick_id)
                    && page_entry_flag == PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY && !m_brick_reply_queue.Contains(brick_id))
                {
                    // update the internal LRU timestamp so that this doesn't get discarded
                    m_cpu_cache.Get(brick_id);
                    m_brick_reply_queue.Enqueue(brick_id);
                }
            }
        }


        private void CheckBrickRequestsCompletionStatus(HashSet<UInt32> filtered_brick_requests)
        {
            // if no more new brick requests are dispatched and also no bricks are currently
            // being processed
            if (filtered_brick_requests.Count == 0 && m_in_flight_brick_imports.Count == 0)
            {
                OnNoMoreBrickRequests?.Invoke();
            }
        }


        private void GPURandomizeBrickRequestsTexOffset()
        {
            m_material.SetVector(SHADER_BRICK_REQUESTS_RANDOM_TEX_ST_ID, new Vector4(
                1,
                1,
                UnityEngine.Random.Range(-1.0f, 1.0f),
                UnityEngine.Random.Range(-1.0f, 1.0f)
            ));
        }


        // holds in-flight (i.e., in the process of being imported) bricks
        // this is samantically a "ConcurrentHashSet" so ignore the values are ignored
        private readonly ConcurrentDictionary<UInt32, byte> m_in_flight_brick_imports = new();


        /// <summary>
        ///     Dispatches async brick imports for provided brick requests.
        /// </summary>
        /// 
        /// <param name="filtered_brick_requests">
        ///     Collection of newly requested brick IDs to try to load into the CPU-memory cache.
        /// </param>
        private void ImportBricksIntoMemoryCache(HashSet<UInt32> filtered_brick_requests)
        {
            if (filtered_brick_requests.Count == 0)
            {
                return;
            }

            int nbr_threads = m_PipelineParams.MaxNbrImporterThreads > 0 ? m_PipelineParams.MaxNbrImporterThreads:
                Math.Max(Environment.ProcessorCount - 2, 1);

            // make sure to copy the brick requests data in the main thread and not
            // inside the Task.Run callback!
            UInt32[] brick_ids = new UInt32[filtered_brick_requests.Count];
            filtered_brick_requests.CopyTo(brick_ids);

            foreach (var brick_id in brick_ids)
            {
                // save this brick IDs so that future imports know it is being imported
                // this is semantically a HashSet, the value 0 is completely arbitrary
                m_in_flight_brick_imports[brick_id] = 0;
            }

            // TODO: remove brick id from in flight brick imports in case of task failure
            Task t = Task.Run(() =>
            {
                Parallel.For(0, brick_ids.Length, new ParallelOptions()
                {
                    TaskScheduler = new LimitedConcurrencyLevelTaskScheduler(nbr_threads)
                }, i =>
                {
                    Importer.ImportBrick(m_metadata, brick_ids[i], m_brick_size, m_cpu_cache);
                    m_brick_reply_queue.Enqueue(brick_ids[i]);
                });
            });
            t.ContinueWith(t =>
            {
#if DEBUG
                Debug.LogException(t.Exception);
#endif
            }, TaskContinuationOptions.OnlyOnFaulted);
        }


        /// <summary>
        ///     Out-of-core virtual memory (i.e., page table) loop. This loop handles on-demand
        ///     GPU brick requests, manages the different caches, and synchronizes CPU-GPU resources.
        /// </summary>
        public IEnumerator OOCPTLoop()
        {
            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null)
                && (m_brick_cache != null));
#if DEBUG
            Debug.Log("[OOC PT] started handling GPU brick requests");
#endif

            long nbr_bricks_uploaded = 0;
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame];

            UInt32[] raw_brick_requests = new UInt32[m_PipelineParams.MaxNbrBrickRequestsPerFrame];
            HashSet<UInt32> filtered_brick_requests = new (m_PipelineParams.MaxNbrBrickRequestsPerFrame);

            // add offset for inter-brick interpolation support
            Vector3 brick_cache_slot_offset = Vector3.zero;
            if (m_metadata.ChunkPadding)
            {
                brick_cache_slot_offset = new Vector3(m_brick_cache_voxel_size.x, m_brick_cache_voxel_size.y, m_brick_cache_voxel_size.z);
            }

            while (true)
            {
                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                // in case the homogeneity tolerance has changed - update the page table entries accordingly
                UpdatePageTablesHomogeneity();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // get bricks requested by GPU in this frame and import them into bricks memory cache
                GPUGetBrickRequests(raw_brick_requests);
                GPUResetBrickRequests();
                FilterBrickRequests(raw_brick_requests, filtered_brick_requests);
                CheckBrickRequestsCompletionStatus(filtered_brick_requests);
                ImportBricksIntoMemoryCache(filtered_brick_requests);

                // update CPU-side brick cache usage from the GPU and reset it on the GPU
                GPUGetBrickCacheUsage();
                GPUResetBrickCacheUsage();

                GPURandomizeBrickRequestsTexOffset();
                GPUUpdateVisualizationParams();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {
                    m_in_flight_brick_imports.TryRemove(brick_id, out byte _);

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned
                    // to a fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);

                    // in case the brick is homogeneous, avoid adding it the cache and just update the
                    // corresponding page entry
                    if ((brick.max - brick.min) <= m_homogeneity_tolerance)
                    {
                        m_page_dir_dirty = true;
                        UpdatePageTablesHomogeneousBrick(brick_id, brick.min, brick.max);
                        continue;
                    }

                    // LRU cache eviction scheme; pick least recently used brick cache slot
                    int brick_cache_idx = m_brick_cache_usage_sorted[nbr_bricks_uploaded_per_frame].brick_cache_idx;
                    BrickCacheUsage evicted_slot = m_brick_cache_usage[brick_cache_idx];
                    if (evicted_slot.timestamp == m_timestamp)
                    {
#if DEBUG
                        Debug.LogWarning("circular brick requests detected!\n" +
                            "This is caused by an insufficient GPU brick cache size for the set viusalization parameters.\n" +
                            "To solve this, consider increasing the GPU brick cache size or adjusting the visualization parameters for a lower visual quality.");
#endif
                        continue;
                    }
                    // check if we are evicting a slot that was used a while ago but is no-longer in-use
                    if (evicted_slot.brick_id != INVALID_BRICK_ID)
                    {
                        // set the alpha component to UNMAPPED so the shader knows
                        SetPageEntryAlphaChannelData(GetPageTableIndex(evicted_slot.brick_id), PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY);
                    }

                    BrickCacheUsage added_slot = new()
                    {
                        brick_id = brick_id,
                        // the brick was needed a couple of frames ago - therefore do NOT set the timestamp to current
                        // timestamp and let the shader decide whether it actually still needs it or not.
                        timestamp = 0,
                        brick_cache_idx = brick_cache_idx,
                        brick_min = brick.min,
                        brick_max = brick.max
                    };
                    m_brick_cache_usage[brick_cache_idx] = added_slot;

                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3 brick_cache_slot_pos_normalized);
                    int page_dir_enty_idx = GetPageTableIndex(brick_id);
                    m_page_dir_data[page_dir_enty_idx] = brick_cache_slot_pos_normalized.x + brick_cache_slot_offset.x;
                    m_page_dir_data[page_dir_enty_idx + 1] = brick_cache_slot_pos_normalized.y + brick_cache_slot_offset.y;
                    m_page_dir_data[page_dir_enty_idx + 2] = brick_cache_slot_pos_normalized.z + brick_cache_slot_offset.z;
                    SetPageEntryAlphaChannelData(page_dir_enty_idx, PageEntryFlag.MAPPED_PAGE_TABLE_ENTRY, added_slot.brick_min, added_slot.brick_max);
                    m_page_dir_dirty = true;

                    // allocate the plugin call's arguments struct
                    handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3Int brick_cache_slot_pos);
                    TextureSubImage3DParams args = new()
                    {
                        texture_handle = m_brick_cache_ptr,
                        xoffset = brick_cache_slot_pos.x,
                        yoffset = brick_cache_slot_pos.y,
                        zoffset = brick_cache_slot_pos.z,
                        width = m_actual_brick_size,
                        height = m_actual_brick_size,
                        depth = m_actual_brick_size,
                        data_ptr = handles[nbr_bricks_uploaded_per_frame].AddrOfPinnedObject(),
                        level = 0,
                        format = m_tex_plugin_format
                    };
                    m_tex_params_pool.Acquire(args, out IntPtr arg_ptr);
                    cmd_buffer.IssuePluginEventAndData(API.GetRenderEventFunc(), (int)TextureSubPlugin.Event.TextureSubImage3D,
                        arg_ptr);

                    ++nbr_bricks_uploaded_per_frame;

                }  // END WHILE

                // we execute the command buffer instantly
                Graphics.ExecuteCommandBuffer(cmd_buffer);
                cmd_buffer.Clear();

                GPUUpdatePageTables();

                // increase timestamp
                ++m_timestamp;

            }  // END WHILE

        }  // END COROUTINE


        /// <summary>
        ///     Out-of-core hybrid (i.e., virtual memory and octree acceleration structure) loop. This loop handles
        ///     on-demand GPU brick requests, manages the different caches, and synchronizes CPU-GPU resources.
        /// </summary>
        public IEnumerator OOCHybridLoop()
        {
            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null)
                && (m_brick_cache != null));
#if DEBUG
            Debug.Log("[OOC PT + OCTREE] started handling GPU brick requests");
#endif

            long nbr_bricks_uploaded = 0;
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame];

            UInt32[] raw_brick_requests = new UInt32[m_PipelineParams.MaxNbrBrickRequestsPerFrame];
            HashSet<UInt32> filtered_brick_requests = new (m_PipelineParams.MaxNbrBrickRequestsPerFrame);

            // add offset for inter-brick interpolation support
            Vector3 brick_cache_slot_offset = Vector3.zero;
            if (m_metadata.ChunkPadding)
            {
                brick_cache_slot_offset = new Vector3(m_brick_cache_voxel_size.x, m_brick_cache_voxel_size.y, m_brick_cache_voxel_size.z);
            }

            List<BrickCacheUsage> brick_cache_added_slots = new();
            List<BrickCacheUsage> brick_cache_evicted_slots = new();

            while (true)
            {
                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                // in case the homogeneity tolerance has changed - update the page table entries accordingly
                UpdatePageTablesHomogeneity();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // get bricks requested by GPU in this frame and import them into bricks memory cache
                GPUGetBrickRequests(raw_brick_requests);
                GPUResetBrickRequests();
                FilterBrickRequests(raw_brick_requests, filtered_brick_requests);
                CheckBrickRequestsCompletionStatus(filtered_brick_requests);
                ImportBricksIntoMemoryCache(filtered_brick_requests);

                // update CPU-side brick cache usage from the GPU and reset it on the GPU
                GPUGetBrickCacheUsage();
                GPUResetBrickCacheUsage();

                GPURandomizeBrickRequestsTexOffset();
                GPUUpdateVisualizationParams();

                brick_cache_added_slots.Clear();
                brick_cache_evicted_slots.Clear();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < m_PipelineParams.MaxNbrGPUBrickUploadsPerFrame &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {
                    m_in_flight_brick_imports.TryRemove(brick_id, out byte _);

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned
                    // to a fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);

                    // in case the brick is homogeneous, avoid adding it the cache and just update the
                    // corresponding page entry
                    if ((brick.max - brick.min) <= m_homogeneity_tolerance)
                    {
                        m_page_dir_dirty = true;
                        UpdatePageTablesHomogeneousBrick(brick_id, brick.min, brick.max);
                        continue;
                    }

                    // LRU cache eviction scheme; pick least recently used brick cache slot
                    int brick_cache_idx = m_brick_cache_usage_sorted[nbr_bricks_uploaded_per_frame].brick_cache_idx;
                    BrickCacheUsage evicted_slot = m_brick_cache_usage[brick_cache_idx];
                    if (evicted_slot.timestamp == m_timestamp)
                    {
#if DEBUG
                        Debug.LogWarning("circular brick requests detected!\n" +
                            "This is caused by an insufficient GPU brick cache size for the set viusalization parameters.\n" +
                            "To solve this, consider increasing the GPU brick cache size or adjusting the visualization parameters for a lower visual quality.");
#endif
                        continue;
                    }
                    // check if we are evicting a slot that was used a while ago but is no-longer in-use
                    if (evicted_slot.brick_id != INVALID_BRICK_ID)
                    {
                        // set the alpha component to UNMAPPED so the shader knows
                        SetPageEntryAlphaChannelData(GetPageTableIndex(evicted_slot.brick_id), PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY);
                        brick_cache_evicted_slots.Add(evicted_slot);
                    }

                    BrickCacheUsage added_slot = new()
                    {
                        brick_id = brick_id,
                        // the brick was needed a couple of frames ago - therefore do NOT set the timestamp to current
                        // timestamp and let the shader decide whether it actually still needs it or not.
                        timestamp = 0,
                        brick_cache_idx = brick_cache_idx,
                        brick_min = brick.min,
                        brick_max = brick.max
                    };
                    m_brick_cache_usage[brick_cache_idx] = added_slot;
                    brick_cache_added_slots.Add(added_slot);

                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3 brick_cache_slot_pos_normalized);
                    int page_dir_enty_idx = GetPageTableIndex(brick_id);
                    m_page_dir_data[page_dir_enty_idx] = brick_cache_slot_pos_normalized.x + brick_cache_slot_offset.x;
                    m_page_dir_data[page_dir_enty_idx + 1] = brick_cache_slot_pos_normalized.y + brick_cache_slot_offset.y;
                    m_page_dir_data[page_dir_enty_idx + 2] = brick_cache_slot_pos_normalized.z + brick_cache_slot_offset.z;
                    SetPageEntryAlphaChannelData(page_dir_enty_idx, PageEntryFlag.MAPPED_PAGE_TABLE_ENTRY, added_slot.brick_min, added_slot.brick_max);
                    m_page_dir_dirty = true;

                    // allocate the plugin call's arguments struct
                    handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3Int brick_cache_slot_pos);
                    TextureSubImage3DParams args = new()
                    {
                        texture_handle = m_brick_cache_ptr,
                        xoffset = brick_cache_slot_pos.x,
                        yoffset = brick_cache_slot_pos.y,
                        zoffset = brick_cache_slot_pos.z,
                        width = m_actual_brick_size,
                        height = m_actual_brick_size,
                        depth = m_actual_brick_size,
                        data_ptr = handles[nbr_bricks_uploaded_per_frame].AddrOfPinnedObject(),
                        level = 0,
                        format = m_tex_plugin_format
                    };
                    m_tex_params_pool.Acquire(args, out IntPtr arg_ptr);
                    cmd_buffer.IssuePluginEventAndData(API.GetRenderEventFunc(), (int)TextureSubPlugin.Event.TextureSubImage3D,
                        arg_ptr);

                    ++nbr_bricks_uploaded_per_frame;

                }  // END WHILE

                // we execute the command buffer instantly
                Graphics.ExecuteCommandBuffer(cmd_buffer);
                cmd_buffer.Clear();

                GPUUpdatePageTables();

                if ((brick_cache_added_slots.Count > 0) || (brick_cache_evicted_slots.Count > 0))
                {
                    // make sure to update the brick cache bricks residency HashSet before updating the residency octree!
                    UpdateBrickCacheResidencyHashSet(brick_cache_added_slots, brick_cache_evicted_slots);
                    GPUUpdateResidencyOctree(brick_cache_added_slots, brick_cache_evicted_slots);
                }

                // increase timestamp
                ++m_timestamp;

            }  // END WHILE

        }  // END COROUTINE


        /// <summary>
        ///     Adds a wireframe object to denote the spatial extent of the provided brick.
        ///     Useful for debugging purposes and observing out-of-core GPU brick loading.
        /// </summary>
        private void OOCAddBrickWireframeObject(UInt32 brick_id)
        {
            int id = (int)(brick_id & 0x03FFFFFF);
            int res_lvl = (int)(brick_id >> 26);

            Vector3 brick_scale = new(
                m_brick_size / m_VolumeDimsPerResLvl[res_lvl].x,
                m_brick_size / m_VolumeDimsPerResLvl[res_lvl].y,
                m_brick_size / m_VolumeDimsPerResLvl[res_lvl].z
            );

            // transition to Unity's Texture3D coordinate system
            Vector4 nbr_bricks = m_nbr_bricks_per_res_lvl[res_lvl];
            int x = m_brick_size * (id % (int)nbr_bricks.x);
            int y = m_brick_size * ((id / (int)nbr_bricks.x) % (int)nbr_bricks.y);
            int z = m_brick_size * (id / ((int)nbr_bricks.x * (int)nbr_bricks.y));

            GameObject brick_wireframe = m_brick_wireframes_pool.Dequeue();
            brick_wireframe.transform.localPosition = new Vector3(
                (x / m_VolumeDimsPerResLvl[res_lvl].x - 0.5f) + brick_scale.x / 2.0f,
                (y / m_VolumeDimsPerResLvl[res_lvl].y - 0.5f) + brick_scale.y / 2.0f,
                (z / m_VolumeDimsPerResLvl[res_lvl].z - 0.5f) + brick_scale.z / 2.0f
            );
            brick_wireframe.transform.localScale = brick_scale;
            // brick_wireframe.name = $"brick_{brick_id & 0x03FFFFFF}_res_lvl_{brick_id >> 26}";
            brick_wireframe.SetActive(true);
            m_brick_wireframes_in_use.Add(brick_wireframe);
        }


        /// <summary>
        ///     Resets the GPU brick requests structured buffer. Should be called at the end
        ///     of each frame and before the execution of the next frame.
        /// </summary>
        private void GPUResetBrickRequests()
        {
            m_brick_requests_cb.SetData(m_brick_requests_default_data);
        }


        /// <summary>
        ///     Retrieves brick cache usage data from the GPU and updates the CPU-side
        ///     brick cache usage array. The just-updated brick cache usage array is
        ///     then sorted according to the timestamp at which the brick was lastly
        ///     used.
        /// </summary>
        private void GPUGetBrickCacheUsage()
        {
            m_brick_cache_usage_cb.GetData(m_brick_cache_usage_tmp);

            // in case we want to instantiate brick wireframes (useful for debugging)
            if (InstantiateBrickWireframes)
            {
                foreach (var go in m_brick_wireframes_in_use)
                {
                    go.SetActive(false);
                    m_brick_wireframes_pool.Enqueue(go);
                }
                m_brick_wireframes_in_use.Clear();
            }

            for (int i = 0; i < m_brick_cache_usage_tmp.Length; ++i)
            {
                if (m_brick_cache_usage_tmp[i] == 0)
                {
                    continue;
                }

                for (int j = 0; (j < 32) && (i * 32 + j < m_brick_cache_usage.Length); ++j)
                {
                    int brick_idx = i * 32 + j;
                    // filter unused brick slots
                    if (((m_brick_cache_usage_tmp[i] >> j) & 0x1) == 1)
                    {
                        m_brick_cache_usage[brick_idx].timestamp = m_timestamp;

                        // in case we want to instantiate brick wireframes (useful for debugging)
                        if (InstantiateBrickWireframes)
                        {
                            OOCAddBrickWireframeObject(m_brick_cache_usage[brick_idx].brick_id);
                        }
                    }
                }
            }
            // sort the just-updated brick cache usage array by timestamp.
            // empty brick slots should be at the head of the array
            m_brick_cache_usage.CopyTo(m_brick_cache_usage_sorted, 0);
            Array.Sort(m_brick_cache_usage_sorted, (BrickCacheUsage a, BrickCacheUsage b)
                => a.timestamp.CompareTo(b.timestamp));
        }


        private void GPUResetBrickCacheUsage()
        {
            m_brick_cache_usage_cb.SetData(m_brick_cache_usage_default_data);
        }


        /// <summary>
        ///     Gets the index of the slot in the brick cache usage buffer corresponding to the provided normalized brick
        ///     cache slot coordinates
        /// </summary>
        /// 
        /// <param name="x">
        ///     normalized x coordinate in the brick cache
        /// </param>
        ///
        /// <param name="y">
        ///     normalized y coordinate in the brick cache
        /// </param>
        /// 
        /// <param name="z">
        ///     normalized z coordinate in the brick cache
        /// </param>
        /// 
        /// <returns>
        ///     Correpsonding brick cache usage buffer slot index
        /// </returns>
        private int GetBrickCacheUsageIndex(float x, float y, float z)
        {
            return (m_gpu_brick_cache_nbr_bricks.x * m_gpu_brick_cache_nbr_bricks.y * Mathf.RoundToInt(z * m_gpu_brick_cache_nbr_bricks.z)) +
                   (m_gpu_brick_cache_nbr_bricks.x * Mathf.RoundToInt(y * m_gpu_brick_cache_nbr_bricks.y)) +
                   Mathf.RoundToInt(x * m_gpu_brick_cache_nbr_bricks.x);
        }


        private void GetBrickCacheSlotPosition(int brick_cache_idx, out Vector3 pos)
        {
            pos = new Vector3(
                (brick_cache_idx % m_gpu_brick_cache_nbr_bricks.x) * m_actual_brick_size / (float)m_brick_cache.width,
                ((brick_cache_idx / m_gpu_brick_cache_nbr_bricks.x) % m_gpu_brick_cache_nbr_bricks.y) * m_actual_brick_size / (float)m_brick_cache.height,
                (brick_cache_idx / (m_gpu_brick_cache_nbr_bricks.x * m_gpu_brick_cache_nbr_bricks.y)) * m_actual_brick_size / (float)m_brick_cache.depth
            );
        }


        private void GetBrickCacheSlotPosition(int brick_cache_idx, out Vector3Int pos)
        {
            pos = new Vector3Int(
                (brick_cache_idx % m_gpu_brick_cache_nbr_bricks.x) * m_actual_brick_size,
                ((brick_cache_idx / m_gpu_brick_cache_nbr_bricks.x) % m_gpu_brick_cache_nbr_bricks.y) * m_actual_brick_size,
                (brick_cache_idx / (m_gpu_brick_cache_nbr_bricks.x * m_gpu_brick_cache_nbr_bricks.y)) * m_actual_brick_size
            );
        }


        private void PropagateResidencyOctreeUpdate(int changed_node_idx)
        {
            // propagate to parents
            for (int curr_node_idx = changed_node_idx; curr_node_idx > 0;)
            {
                // move to parent node
                int parent_node_idx = Mathf.FloorToInt((curr_node_idx - 1) / 8);
                if ((m_residency_octree_data[parent_node_idx].data >> 16)
                    == (m_residency_octree_data[curr_node_idx].data >> 16))
                    break;
                m_residency_octree_data[parent_node_idx].data
                    |= m_residency_octree_data[curr_node_idx].data & 0xFFFF0000;
                curr_node_idx = parent_node_idx;
            }
        }


        private void PropagateResidencyOctreeUpdates(HashSet<int> changed_node_indices)
        {
            foreach (int idx in changed_node_indices)
                PropagateResidencyOctreeUpdate(idx);
        }


        private void _GetOverlappingLeafNodes(int node_idx, Vector3 brick_extent_min, Vector3 brick_extent_max,
            ref List<int> node_indices)
        {
            // >= or > ?
            var node = m_residency_octree_data[node_idx];
            if (
                ((node.center_x + node.side_halved) >= brick_extent_min.x) &&
                ((node.center_x - node.side_halved) <= brick_extent_max.x) &&
                ((node.center_y + node.side_halved) >= brick_extent_min.y) &&
                ((node.center_y - node.side_halved) <= brick_extent_max.y) &&
                ((node.center_z + node.side_halved) >= brick_extent_min.z) &&
                ((node.center_z - node.side_halved) <= brick_extent_max.z)
            )
            {
                // check if this is a leaf node
                if ((8 * node_idx + 1) >= m_residency_octree_data.Length)
                {
                    node_indices.Add(node_idx);
                    return;
                }
                // otherwise, in case this is not a leaf node, recursively do the extent checks on the 8 children
                for (int i = 1; i <= 8; ++i)
                {
                    int child_idx = 8 * node_idx + i;
                    _GetOverlappingLeafNodes(child_idx, brick_extent_min, brick_extent_max, ref node_indices);
                }
            }
            // exit in case provided brick's spatial extent does not overlap with that of this node
        }


        private void GetOverlappingLeafNodes(UInt32 brick_id, ref List<int> node_indices)
        {

            node_indices.Clear();
            CVDSMetadata metadata = m_volume_dataset.Metadata;

            // brick_id
            int id = (int)(brick_id & 0x03FFFFFF);
            int res_lvl = (int)(brick_id >> 26);

            // transition to Unity's Texture3D coordinate system
            // TODO: cache these
            int nbr_bricks_x = metadata.NbrChunksPerResolutionLvl[res_lvl].x * metadata.ChunkSize / m_brick_size;
            int nbr_bricks_y = metadata.NbrChunksPerResolutionLvl[res_lvl].y * metadata.ChunkSize / m_brick_size;

            float b = m_brick_size << res_lvl;
            Vector3 brick_sides = new(
                b / metadata.Dims.x,
                b / metadata.Dims.y,
                b / metadata.Dims.z
            );

            Vector3 brick_extent_min = new(
                (id % nbr_bricks_x) * brick_sides.x,
                ((id / nbr_bricks_x) % nbr_bricks_y) * brick_sides.y,
                (id / (nbr_bricks_x * nbr_bricks_y)) * brick_sides.z
            );
            Vector3 brick_extent_max = brick_extent_min + brick_sides;

            // TODO: seriously? recursive shit in a per-frame call? optimize this crap
            _GetOverlappingLeafNodes(
                node_idx: 0,
                brick_extent_min: brick_extent_min,
                brick_extent_max: brick_extent_max,
                node_indices: ref node_indices
            );

        }


        /// <summary>
        ///     Gets bricks overlapping with the provided octree node in the provided resolution
        ///     level.
        /// </summary>
        /// <param name="leaf_node_idx"></param>
        /// <param name="res_lvl"></param>
        /// <param name="brick_ids">Overlapping bricks are added to this list. The list is initially cleared.</param>
        private void GetOverlappingBricks(int leaf_node_idx, int res_lvl, ref List<UInt32> brick_ids)
        {

            brick_ids.Clear();
            var volume_dims = m_volume_dataset.Metadata.Dims;

            // TODO: cache this constant shit
            int nbr_bricks_x = m_volume_dataset.Metadata.NbrChunksPerResolutionLvl[res_lvl].x
                * m_volume_dataset.Metadata.ChunkSize / m_brick_size;
            int nbr_bricks_y = m_volume_dataset.Metadata.NbrChunksPerResolutionLvl[res_lvl].y
                * m_volume_dataset.Metadata.ChunkSize / m_brick_size;

            float s = m_residency_octree_data[leaf_node_idx].side_halved;
            float _brick_size = m_brick_size << res_lvl;  // => m_brick_size * Math.Pow(2, res_lvl)
            Vector3 b = new(_brick_size / volume_dims.x, _brick_size / volume_dims.y,
                _brick_size / volume_dims.z);
            Vector3Int offset = new(
                Mathf.FloorToInt((m_residency_octree_data[leaf_node_idx].center_x - s) / b.x),
                Mathf.FloorToInt((m_residency_octree_data[leaf_node_idx].center_y - s) / b.y),
                Mathf.FloorToInt((m_residency_octree_data[leaf_node_idx].center_z - s) / b.z)
            );
            Vector3Int node = new(
                Mathf.CeilToInt((m_residency_octree_data[leaf_node_idx].center_x + s) / b.x) - offset.x,
                Mathf.CeilToInt((m_residency_octree_data[leaf_node_idx].center_y + s) / b.y) - offset.y,
                Mathf.CeilToInt((m_residency_octree_data[leaf_node_idx].center_z + s) / b.z) - offset.z
            );
            for (int i = 0; i < node.z; ++i)
            {
                int idx = (offset.z + i) * nbr_bricks_x * nbr_bricks_y + offset.y * nbr_bricks_x + offset.x;
                for (int j = 0; j < node.y; ++j)
                {
                    for (int k = 0; k < node.x; ++k)
                    {
                        UInt32 brick_id = (UInt32)(idx + j * nbr_bricks_x + k) | ((UInt32)res_lvl << 26);
                        brick_ids.Add(brick_id);
                    }
                }
            }

        }


        private List<UInt32> m_overlapping_bricks = new();
        private List<int> m_leaf_node_indices = new();

        private void GPUUpdateResidencyOctree(List<BrickCacheUsage> added_slots, List<BrickCacheUsage> evicted_slots)
        {

            m_octree_changed_node_indices.Clear();

            foreach (var added_slot in added_slots)
            {
                GetOverlappingLeafNodes(added_slot.brick_id, ref m_leaf_node_indices);
                // mark overlapping leaf nodes as partially mapped in the brick's res level
                int res_lvl = (int)(added_slot.brick_id >> 26);
                foreach (int idx in m_leaf_node_indices)
                {
                    UInt32 new_data = m_residency_octree_data[idx].data | (1u << (res_lvl + 16));
                    if (m_residency_octree_data[idx].data != new_data)
                    {
                        m_residency_octree_data[idx].data = new_data;
                        m_octree_changed_node_indices.Add(idx);
                    }
                }
            }

            foreach (var evicted_slot in evicted_slots)
            {
                GetOverlappingLeafNodes(evicted_slot.brick_id, ref m_leaf_node_indices);
                int res_lvl = (int)(evicted_slot.brick_id >> 26);
                // check if overlapping nodes are still partially mapped
                foreach (int idx in m_leaf_node_indices)
                {
                    GetOverlappingBricks(idx, res_lvl, ref m_overlapping_bricks);
                    bool at_least_one_brick_in_brick_cache = false;
                    foreach (UInt32 id in m_overlapping_bricks)
                    {
                        if (m_brick_cache_brick_residency.Contains(id) /* brick is resident in the brick cache */)
                        {
                            at_least_one_brick_in_brick_cache = true;
                            break;
                        }
                    }
                    // in case no brick in res_lvl overlapping with this node exists in the
                    // brick cache => clear the associated bit from the node's bitmask
                    if (!at_least_one_brick_in_brick_cache)
                    {
                        m_residency_octree_data[idx].data &= ~(1u << (res_lvl + 16));
                        m_octree_changed_node_indices.Add(idx);
                    }
                }
            }

            // propagate the (potential) changes made in the leaf nodes up the tree
            PropagateResidencyOctreeUpdates(m_octree_changed_node_indices);

            // set GPU residency octree buffer data
            m_residency_octree_cb.SetData(m_residency_octree_data);

        }


        /// <summary>
        ///     Computes the page directory (top level page table) index in the underlying
        ///     flattened data array that corresponds to the provided <paramref name="brick_id"/>.
        /// </summary>
        /// 
        /// <param name="brick_id">
        ///     Brick ID whose corresponding index in the page directory is computed.
        /// </param>
        /// 
        /// <returns>
        ///     Index of the page directory entry in the underlying flattened data array.
        /// </returns>
        /// 
        /// <exception cref="Exception">
        ///     Thrown when provided <paramref name="brick_id"/> is outside the range covered
        ///     by the page directory.
        /// </exception>
        private int GetPageTableIndex(UInt32 brick_id)
        {
            int id = (int)(brick_id & 0x03FFFFFF);
            int res_lvl = (int)(brick_id >> 26);

            Vector3Int nbr_bricks = new((int)m_nbr_bricks_per_res_lvl[res_lvl].x,
                (int)m_nbr_bricks_per_res_lvl[res_lvl].y, (int)m_nbr_bricks_per_res_lvl[res_lvl].z);

            int x = id % nbr_bricks.x;
            int y = (id / nbr_bricks.x) % nbr_bricks.y;
            int z = id / (nbr_bricks.x * nbr_bricks.y);

            if ((x >= m_page_dir_dims[res_lvl].x) || (y >= m_page_dir_dims[res_lvl].y)
                || (z >= m_page_dir_dims[res_lvl].z))
            {
                throw new Exception("provided brick is outside the range of bricks covered by the page table(s)");
            }

            int idx = (m_page_dir.width * m_page_dir.height) * ((int)m_page_dir_base[res_lvl].z + z)
                + m_page_dir.width * ((int)m_page_dir_base[res_lvl].y + y)
                + (int)m_page_dir_base[res_lvl].x + x;

            return idx * m_page_dir_stride;
        }


        /// <summary>
        ///     Extracts the page table entry's data (i.e., status flag, min, and max)
        ///     from the provided page entry index.
        /// </summary>
        /// 
        /// <param name="idx">
        ///     Index of the page entry in the underlying flattened data array.
        /// </param>
        /// 
        /// <returns>
        ///     Struct of the data stored in the provided page entry's alpha channel.
        /// </returns>
        private PageEntryAlphaChannelData ExtractPageEntryAlphaChannelData(int idx)
        {
            UInt32 d;
            float alpha = m_page_dir_data[idx + 3];

            // C# doesn't have a reinterpret_cast operator
            unsafe
            {
                float* fRef = &alpha;
                d = *(UInt32*)(fRef);
            }
            return new PageEntryAlphaChannelData()
            {
                flag = (PageEntryFlag)(d & 0x000000FF),
                min = (byte)((d >> 8) & 0x000000FF),
                max = (byte)((d >> 16) & 0x000000FF),
            };
        }


        /// <summary>
        ///     Sets the page directory entry's alpha channel (i.e., fourth float32 value) to
        ///     the provided page entry flag and min/max values.
        /// </summary>
        /// 
        /// <param name="idx">
        ///     Index in the page directory data array corresponding to the desired page directory
        ///     entry.
        /// </param>
        /// 
        /// <param name="flag">
        ///     Mapping flag to be set for the desired page directory entry.
        /// </param>
        /// 
        /// <param name="min">
        ///     Minimum value of the region covered by the page directory entry to be set.
        /// </param>
        /// 
        /// <param name="max">
        ///     Maximum value of the region covered by the page directory entry to be set.
        /// </param>
        private void SetPageEntryAlphaChannelData(int idx, PageEntryFlag flag, byte min = 0, byte max = 0)
        {
            float alpha;
            // because there is no "reinterpret_cast" in C#
            unsafe
            {
                UInt32 a = (byte)flag | (UInt32)(min << 8) | (UInt32)(max << 16);
                UInt32* aRef = &a;
                alpha = *(float*)(aRef);
            }
            m_page_dir_data[idx + 3] = alpha;
        }


        /// <summary>
        ///     Sweeps through the page table(s) entries and checks the minmax values against
        ///     the homogeneity tolerance value. Since this is an expensive operation, an
        ///     internal dirty flag is checked before proceeding.
        /// </summary>
        private void UpdatePageTablesHomogeneity()
        {
            // perform homogeneity check on all non-unmapped page table entries.
            if (m_pt_requires_homogeneity_update)
            {
                for (int i = 0; i < m_page_dir_data.Length; i += 4)
                {
                    PageEntryAlphaChannelData page_entry_data = ExtractPageEntryAlphaChannelData(i);
                    // if page entry is no longer considered homogeneous
                    if ((page_entry_data.flag == PageEntryFlag.HOMOGENEOUS_PAGE_TABLE_ENTRY) && ((page_entry_data.max - page_entry_data.min) > m_homogeneity_tolerance))
                    {
                        // at some point in some future frame, this PT entry will get mapped
                        // in other words, just unmap it and let the LRU cache system handle the rest.
                        SetPageEntryAlphaChannelData(i, PageEntryFlag.UNMAPPED_PAGE_TABLE_ENTRY, 0, 0);
                    }
                    // else if page entry entry became homogeneous
                    else if ((page_entry_data.flag == PageEntryFlag.MAPPED_PAGE_TABLE_ENTRY) && ((page_entry_data.max - page_entry_data.min) <= m_homogeneity_tolerance))
                    {
                        // the brick cache slot has to be updated to reflect an empty brick cache slot
                        int slotIdx = GetBrickCacheUsageIndex(m_page_dir_data[i], m_page_dir_data[i + 1], m_page_dir_data[i + 2]);
                        m_brick_cache_usage[slotIdx].brick_id = INVALID_BRICK_ID;
                        m_brick_cache_usage[slotIdx].brick_min = 0;
                        m_brick_cache_usage[slotIdx].brick_max = 0;
                        m_brick_cache_usage[slotIdx].timestamp = 0;

                        // set flag to HOMOGENEOUS and let the LRU scheme drop the corresponding
                        // brick cache slot in some future frame if it needs to.
                        m_page_dir_data[i] = ((float)page_entry_data.min + page_entry_data.max) / (2.0f * 255.0f);
                        SetPageEntryAlphaChannelData(i, PageEntryFlag.HOMOGENEOUS_PAGE_TABLE_ENTRY, page_entry_data.min, page_entry_data.max);
                    }
                }
                m_page_dir_dirty = true;
                m_pt_requires_homogeneity_update = false;
            }
        }


        /// <summary>
        ///     Sends the CPU-side page table(s) data to the GPU. Since this is an expensive operation,
        ///     an internal dirty flag is checked before proceeding.
        /// </summary>
        private void GPUUpdatePageTables()
        {
            if (m_page_dir_dirty)
            {
                m_page_dir.SetPixelData(m_page_dir_data, mipLevel: 0);
                m_page_dir.Apply();

                m_page_dir_dirty = false;
            }
        }


        /// <summary>
        ///     Updates the page table entry corresponding to the provided brick so that
        ///     it reflects a homogeneous entry.
        /// </summary>
        /// 
        /// <param name="brick_id">
        ///     brick ID
        /// </param>
        /// 
        /// <param name="_min">
        ///     brick min value (usually density)
        /// </param>
        /// 
        /// <param name="_max">
        ///     brick max value (usually density)
        /// </param>
        private void UpdatePageTablesHomogeneousBrick(UInt32 brick_id, byte _min, byte _max)
        {
            int idx = GetPageTableIndex(brick_id);
            // x channel has to be set to the homogeneous value
            // on the shader side, retrive the value directly using page_entry.x (i.e., no need for [page_entry.x / 255.0f]
            // to convert to the correct value)
            m_page_dir_data[idx] = ((float)_min + _max) / (2.0f * 255.0f);
            SetPageEntryAlphaChannelData(idx, PageEntryFlag.HOMOGENEOUS_PAGE_TABLE_ENTRY, _min, _max);
        }


        private void UpdateBrickCacheResidencyHashSet(List<BrickCacheUsage> added_slots, List<BrickCacheUsage> evicted_slots)
        {
            foreach (var evicted_slot in evicted_slots)
            {
                m_brick_cache_brick_residency.Remove(evicted_slot.brick_id);
            }
            foreach (var added_slot in added_slots)
            {
                m_brick_cache_brick_residency.Add(added_slot.brick_id);
            }
        }


        private void OnModelAlphaCutoffChange(float value)
        {
            m_vis_params_dirty = true;
            m_alpha_cutoff = value;
        }


        private void OnModelSamplingQualityFactorChange(float value)
        {
            m_vis_params_dirty = true;
            m_sampling_quality_factor = value;
        }


        private void OnModelLODDistancesChange(List<float> distances)
        {
            m_vis_params_dirty = true;
            for (int i = 0; i < distances.Count; ++i)
            {
                m_lod_distances_squared[i] = distances[i] * distances[i];
            }
            for (int i = distances.Count; i < MAX_ALLOWED_NBR_RESOLUTION_LVLS; ++i)
            {
                m_lod_distances_squared[i] = 0;
            }
        }


        private void GPUUpdateVisualizationParams()
        {
            if (!m_vis_params_dirty)
                return;

            m_material.SetFloat(SHADER_ALPHA_CUTOFF_ID, m_alpha_cutoff);
            switch (m_rendering_mode)
            {
                case RenderingMode.IC:
                {
                    m_material.SetFloat(SHADER_INITIAL_STEP_SIZE_ID, 1.0f / (Mathf.Max(m_metadata.Dims.x, m_metadata.Dims.y, m_metadata.Dims.z) * m_sampling_quality_factor));
                    break;
                }
                case RenderingMode.OOC_PT:
                case RenderingMode.OOC_HYBRID:
                {
                    m_material.SetFloat(SHADER_INITIAL_STEP_SIZE_ID, 1.0f / (Mathf.Max(m_VolumeDimsPerResLvl[0].x, m_VolumeDimsPerResLvl[0].y, m_VolumeDimsPerResLvl[0].z) * m_sampling_quality_factor));
                    m_material.SetFloatArray(SHADER_LOD_DISTANCES_SQUARED_ID, m_lod_distances_squared);
                    m_material.SetInteger(SHADER_HOMOGENEITY_TOLERANCE_ID, m_homogeneity_tolerance);
                    break;
                }
            }
            m_vis_params_dirty = false;
        }


        private void OnModelTFChange(TF tf, ITransferFunction tf_so)
        {
            m_transfer_function = tf_so;
            m_material.SetTexture(SHADER_TFTEX_ID, m_transfer_function.GetColorLookupTex());
        }


        private void OnModelInterpolationChange(INTERPOLATION value)
        {
            if (m_interpolation_method_update != null)
            {
                StopCoroutine(m_interpolation_method_update);
                m_interpolation_method_update = null;
            }
            switch (value)
            {
                case INTERPOLATION.NEAREST_NEIGHBOR:
                m_interpolation_method_update = StartCoroutine(UpdateWhenBrickCacheReady(
                    () =>
                    {
                        m_brick_cache.filterMode = FilterMode.Point;
                        m_interpolation_method_update = null;
                    }
                    ));
                break;

                case INTERPOLATION.TRILLINEAR:
                m_interpolation_method_update = StartCoroutine(UpdateWhenBrickCacheReady(
                    () =>
                    {
                        m_brick_cache.filterMode = FilterMode.Bilinear;
                        m_interpolation_method_update = null;
                    }));
                break;

                default:
                throw new Exception(value.ToString());
            }
        }


        private void OnModelHomogeneityChange(byte value)
        {
            if (m_homogeneity_tolerance == value)
                return;
            m_homogeneity_tolerance = value;
            m_pt_requires_homogeneity_update = true;
        }


        private void OnTransferFunctionTexChange(Texture2D newTex)
        {
            m_material.SetTexture(SHADER_TFTEX_ID, newTex);
        }


        private IEnumerator UpdateWhenBrickCacheReady(Action clbk)
        {
            yield return new WaitUntil(() => m_brick_cache != null);
            clbk();
        }
    }
}
