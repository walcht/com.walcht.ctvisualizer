using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Collections.Generic;
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
        IC,
        OOC_PT,
        OOC_HYBRID
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
        private readonly int SHADER_SAMPLING_QUALITY_FACTOR_ID = Shader.PropertyToID("_SamplingQualityFactor");


        /////////////////////////////////
        // OUT-OF-CORE DVR SHADER IDs
        /////////////////////////////////
        private readonly int SHADER_BRICK_REQUESTS_RANDOM_TEX_ID = Shader.PropertyToID("_BrickRequestsRandomTex");
        private readonly int SHADER_BRICK_REQUESTS_RANDOM_TEX_ST_ID = Shader.PropertyToID("_BrickRequestsRandomTex_ST");
        private readonly int SHADER_BRICK_REQUESTS_BUFFER_ID = Shader.PropertyToID("brick_requests");
        private readonly int SHADER_NBR_CHUNKS_PER_RES_LVL_ID = Shader.PropertyToID("nbr_chunks_per_res_lvl");  // TODO: remove
        private readonly int SHADER_NBR_BRICKS_PER_RES_LVL_ID = Shader.PropertyToID("nbr_bricks_per_res_lvl");
        private readonly int SHADER_RESIDENCY_OCTREE_BUFFER_ID = Shader.PropertyToID("residency_octree");
        private readonly int SHADER_OCTREE_START_DEPTH_ID = Shader.PropertyToID("_OctreeStartDepth");
        private readonly int SHADER_MAX_OCTREE_DEPTH_ID = Shader.PropertyToID("_MaxOctreeDepth");
        private readonly int SHADER_MAX_NBR_BRICK_REQUESTS_PER_RAY_ID = Shader.PropertyToID("_MaxNbrBrickRequestsPerRay");
        private readonly int SHADER_MAX_NBR_BRICK_REQUESTS_ID = Shader.PropertyToID("_MaxNbrBrickRequests");
        private readonly int SHADER_BRICK_SIZE_ID = Shader.PropertyToID("_BrickSize");
        private readonly int SHADER_CHUNK_SIZE_ID = Shader.PropertyToID("_ChunkSize");
        private readonly int SHADER_VOLUME_DIMS_ID = Shader.PropertyToID("_VolumeDims");
        private readonly int SHADER_VOLUME_TEXEL_SIZE_ID = Shader.PropertyToID("_VolumeTexelSize");
        private readonly int SHADER_PAGE_DIR_DIMS_ID = Shader.PropertyToID("_PageDirDims");
        private readonly int SHADER_PAGE_DIR_TEX_ID = Shader.PropertyToID("_PageDir");
        private readonly int SHADER_PAGE_DIR_BASE_ID = Shader.PropertyToID("_PageDirBase");
        private readonly int SHADER_BRICK_CACHE_USAGE_ID = Shader.PropertyToID("brick_cache_usage");
        private readonly int SHADER_BRICK_CACHE_DIMS_ID = Shader.PropertyToID("_BrickCacheDims");
        private readonly int SHADER_BRICK_CACHE_NBR_BRICKS = Shader.PropertyToID("_BrickCacheNbrBricks");
        private readonly int SHADER_BRICK_CACHE_VOXEL_SIZE = Shader.PropertyToID("_BrickCacheVoxelSize");
        private readonly int SHADER_LOD_QUALITY_FACTOR_ID = Shader.PropertyToID("_LODQualityFactor");
        private readonly int SHADER_MAX_RES_LVL_ID = Shader.PropertyToID("_MaxResLvl"); // _MaxResLvl

        private int m_max_octree_depth;
        private int m_octree_start_depth = 0;
        private readonly int MAX_NBR_BRICK_REQUESTS_PER_RAY = 4;
        private readonly int MAX_NBR_BRICK_REQUESTS_PER_FRAME = 16;
        private readonly int MAX_NBR_BRICK_UPLOADS_PER_FRAME = 4;
        public static readonly UInt32 INVALID_BRICK_ID = 0x80000000;
        private readonly UInt16 MAPPED_PAGE_TABLE_ENTRY = 2;
        private readonly UInt16 UNMAPPED_PAGE_TABLE_ENTRY = 1;
        private readonly UInt16 HOMOGENEOUS_PAGE_TABLE_ENTRY = 0;


        private VolumetricDataset m_volume_dataset = null;
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
        private int m_brick_requests_random_tex_size = 64;
        private byte[] m_brick_requests_random_tex_data;
        private TextureFormat m_brick_cache_format;
        private int m_tex_plugin_format = (int)TextureSubPlugin.Format.UR8;
        private int m_brick_size;
        private int m_resolution_lvl;  // only for in-core rendering
        private float MM_TO_METERS = 0.001f;

        /////////////////////////////////
        // CPU MEMORY BRICK CACHE
        /////////////////////////////////
        private MemoryCache<byte> m_cpu_cache;

        public Material m_DVR_in_core_mat;
        public Material m_DVR_out_of_core_hybrid_mat;
        public Material m_DVR_out_of_core_page_table_mat;


        /////////////////////////////////
        // OBJECT POOLS
        /////////////////////////////////
        private UnmanagedObjectPool<TextureSubImage3DParams> m_tex_params_pool;


        private ConcurrentQueue<UInt32> m_brick_reply_queue = new();

        private ComputeBuffer m_residency_octree_cb;
        private ResidencyNode[] m_residency_octree_data;
        private ComputeBuffer m_brick_requests_cb;
        private ComputeBuffer m_brick_cache_usage_cb;
        private bool m_is_brick_cache_nativaly_created = false;
        private UInt32[] m_brick_requests_default_data;

        // timestamp to keep for caches LRU eviction scheme. Do not set initially to 0
        // because 0 is reserved for empty slots.
        private System.UInt64 m_timestamp = 1;


        /////////////////////////////////
        // BRICK CACHE
        /////////////////////////////////
        private Texture3D m_brick_cache = null;
        private Vector3Int m_brick_cache_size;
        private Vector3Int m_brick_cache_nbr_bricks;
        private UInt32 m_brick_cache_texture_id = 0;
        private IntPtr m_brick_cache_ptr = IntPtr.Zero;
        private float m_brick_cache_size_mb;

        /////////////////////////////////
        // BRICK CACHE USAGE
        /////////////////////////////////
        struct BrickCacheUsage
        {
            public Int32 brick_cache_idx;
            public UInt32 brick_id;
            public UInt64 timestamp;
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
        private float[] m_brick_cache_usage_tmp;

        /// <summary>
        ///    GPU-side brick cache usage default buffer data. This is used to reset
        ///    the GPU-side brick cache usage buffer before the start of next frame
        ///    rendering.
        /// </summary>
        private float[] m_brick_cache_usage_default_data;


        /////////////////////////////////
        // PAGE TABLES
        /////////////////////////////////
        private Texture3D m_page_dir = null;
        private float[] m_page_dir_data;
        private Vector4[] m_page_dir_base;
        private Vector4[] m_page_dir_dims;

        /////////////////////////////////
        // OPTIMIZATION STRUCTURES
        /////////////////////////////////

        /// <summary>
        ///     This is only used to avoid per-frame heap allocations and to speak up residency
        ///     octree updates.
        /// </summary>
        HashSet<int> m_octree_changed_node_indices;

        HashSet<UInt32> m_brick_cache_brick_residency;

        /////////////////////////////////
        // CACHED COMPONENTS
        /////////////////////////////////
        private Transform m_transform;
        private Material m_material;
        private CVDSMetadata m_metadata;
        private Vector4[] m_nbr_bricks_per_res_lvl;
        private int m_nbr_brick_importer_threads = -1;
        private bool m_vis_params_dirty = false;


        /////////////////////////////////
        // DEBUGGING
        /////////////////////////////////
        [SerializeField] private GameObject m_brick_wireframe;
        private Mesh m_wireframe_cube_mesh;
        public bool InstantiateBrickWireframes = false;
        public bool ForceNativeTextureCreation = true;


        /////////////////////////////////
        // EVENTS
        /////////////////////////////////
        public static event Action OnNoMoreBrickRequests;


        private void Awake()
        {
            m_transform = GetComponent<Transform>();
        }


        public void Init(VolumetricDataset volumetricDataset, int brick_size, RenderingMode rendering_mode,
            Vector3Int brick_cache_size, int resolution_lvl = 0, int cpu_memory_cache_mb = 4096,
            int max_nbr_brick_importer_threads = -1)
        {

            if (volumetricDataset == null)
                throw new ArgumentNullException("the provided volumetric dataset should be a non-null reference");

            m_brick_size = brick_size;
            m_rendering_mode = rendering_mode;
            m_volume_dataset = volumetricDataset;
            m_metadata = volumetricDataset.Metadata;
            m_resolution_lvl = resolution_lvl;
            m_nbr_brick_importer_threads = max_nbr_brick_importer_threads;

            m_brick_cache_format = TextureFormat.R8;
            m_cpu_cache = new MemoryCache<byte>(cpu_memory_cache_mb, m_brick_size * m_brick_size * m_brick_size);

            switch (m_rendering_mode)
            {

                case RenderingMode.IC:
                {
                    // check parameter constraints
                    if (m_resolution_lvl < 0 || m_resolution_lvl >= m_metadata.NbrResolutionLvls)
                    {
                        throw new Exception($"invalid provided resolution level for in-core rendering: {m_resolution_lvl}");
                    }

                    // assign material
                    m_material = GetComponent<Renderer>().material = m_DVR_in_core_mat;

                    // initialize the brick cache
                    m_brick_cache_size = new Vector3Int(
                        m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * m_metadata.ChunkSize,
                        m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * m_metadata.ChunkSize,
                        m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * m_metadata.ChunkSize
                    );

                    m_brick_cache_nbr_bricks = new(m_brick_cache_size.x / m_brick_size,
                        m_brick_cache_size.y / m_brick_size, m_brick_cache_size.z / m_brick_size);

                    // enable the progress bar
                    ProgressHandlerEvents.OnRequestActivate?.Invoke(true);
                    Task t = Task.Run(() =>
                    {
                        Importer.LoadAllBricksIntoCache(m_volume_dataset.Metadata, m_brick_size, m_resolution_lvl,
                            m_cpu_cache, m_brick_reply_queue, m_nbr_brick_importer_threads);
                    });
                    t.ContinueWith(t => { Debug.LogException(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);

                    // finally start the loop
                    StartCoroutine(InCoreLoop());

                    break;
                }

                case RenderingMode.OOC_HYBRID:
                {

                    // check parameter constraints
                    if ((brick_cache_size.x <= 0) || (brick_cache_size.y <= 0) || (brick_cache_size.z <= 0)
                        || !Mathf.IsPowerOfTwo(brick_cache_size.x) || !Mathf.IsPowerOfTwo(brick_cache_size.y)
                        || !Mathf.IsPowerOfTwo(brick_cache_size.z) || ((brick_cache_size.x % m_brick_size) != 0)
                        || ((brick_cache_size.y % m_brick_size) != 0) || ((brick_cache_size.z % m_brick_size) != 0))
                    {
                        throw new Exception($"invalid provided brick cache dimension size for out-of-core rendering: {brick_cache_size}");
                    }
                    if ((MAX_NBR_BRICK_REQUESTS_PER_FRAME % MAX_NBR_BRICK_REQUESTS_PER_RAY) != 0)
                    {
                        throw new Exception("MAX_NBR_BRICK_REQUESTS_PER_FRAME has to be a multiple of MAX_NBR_BRICK_REQUESTS_PER_RAY");
                    }

                    // assign material
                    m_material = GetComponent<Renderer>().material = m_DVR_out_of_core_hybrid_mat;

                    m_brick_cache_size = brick_cache_size;

                    m_brick_cache_nbr_bricks = new(m_brick_cache_size.x / m_brick_size,
                        m_brick_cache_size.y / m_brick_size, m_brick_cache_size.z / m_brick_size);

                    m_brick_cache_brick_residency = new HashSet<UInt32>();

                    // clear any previously set UAVs
                    Graphics.ClearRandomWriteTargets();

                    // initialize the brick requests buffer and data
                    InitializeBrickRequestsBuffer();

                    // initialize the brick cache usage buffer
                    InitializeBrickCacheUsage();

                    // load and initialize the residency octree compute buffer
                    InitializeResidencyOctree();

                    // initialize brick requests random texture
                    InitializeBrickRequestsRandomTex();

                    // initialize the page table(s)
                    InitializePageDirectory();

                    // initialize remaining shader variables/properties

                    // create an array to hold nbr chunks per resolution level
                    Vector4[] nbr_chunks_per_res_lvl = new Vector4[m_metadata.NbrChunksPerResolutionLvl.Length];
                    for (int i = 0; i < m_metadata.NbrChunksPerResolutionLvl.Length; ++i)
                        nbr_chunks_per_res_lvl[i] = new Vector4(m_metadata.NbrChunksPerResolutionLvl[i].x,
                            m_metadata.NbrChunksPerResolutionLvl[i].y, m_metadata.NbrChunksPerResolutionLvl[i].z);
                    m_material.SetVectorArray(SHADER_NBR_CHUNKS_PER_RES_LVL_ID, nbr_chunks_per_res_lvl);

                    // set remaining shader variables
                    m_material.SetInteger(SHADER_OCTREE_START_DEPTH_ID, m_octree_start_depth);
                    m_material.SetInteger(SHADER_MAX_OCTREE_DEPTH_ID, m_max_octree_depth);
                    m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_PER_RAY_ID, MAX_NBR_BRICK_REQUESTS_PER_RAY);
                    m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_ID, MAX_NBR_BRICK_REQUESTS_PER_FRAME);
                    m_material.SetInteger(SHADER_BRICK_SIZE_ID, m_brick_size);
                    m_material.SetInteger(SHADER_CHUNK_SIZE_ID, m_metadata.ChunkSize);
                    m_material.SetVector(SHADER_VOLUME_DIMS_ID, new Vector3(m_metadata.Dims.x, m_metadata.Dims.y, m_metadata.Dims.z));
                    m_material.SetVector(SHADER_BRICK_CACHE_DIMS_ID, new Vector3(m_brick_cache_size.x, m_brick_cache_size.y, m_brick_cache_size.z));
                    m_material.SetVector(SHADER_BRICK_CACHE_NBR_BRICKS, new Vector3(m_brick_cache_nbr_bricks.x, m_brick_cache_nbr_bricks.y, m_brick_cache_nbr_bricks.z));
                    m_material.SetInteger(SHADER_MAX_RES_LVL_ID, m_metadata.NbrResolutionLvls - 1);

                    // scale mesh to match correct dimensions of the original volumetric data
                    m_transform.localScale = new Vector3(
                         MM_TO_METERS * m_metadata.VoxelDims.x * Mathf.Ceil(m_metadata.Dims.x / (1 << m_resolution_lvl)),
                         MM_TO_METERS * m_metadata.VoxelDims.y * Mathf.Ceil(m_metadata.Dims.y / (1 << m_resolution_lvl)),
                         MM_TO_METERS * m_metadata.VoxelDims.z * Mathf.Ceil(m_metadata.Dims.z / (1 << m_resolution_lvl))
                    );

                    // finally start the loop
                    StartCoroutine(OOCHybridLoop());

                    break;

                }  // END of switch case RenderingMode.OUT_OF_CORE

                case RenderingMode.OOC_PT:
                {
                    // check parameter constraints
                    if ((brick_cache_size.x <= 0) || (brick_cache_size.y <= 0) || (brick_cache_size.z <= 0)
                        || ((brick_cache_size.x % m_brick_size) != 0) || ((brick_cache_size.y % m_brick_size) != 0)
                        || ((brick_cache_size.z % m_brick_size) != 0))
                    {
                        throw new Exception($"invalid provided brick cache dimension size for out-of-core rendering: {brick_cache_size}");
                    }

                    if ((MAX_NBR_BRICK_REQUESTS_PER_FRAME % MAX_NBR_BRICK_REQUESTS_PER_RAY) != 0)
                    {
                        throw new Exception("MAX_NBR_BRICK_REQUESTS_PER_FRAME has to be a multiple of MAX_NBR_BRICK_REQUESTS_PER_RAY");
                    }

                    // assign material
                    m_material = GetComponent<Renderer>().material = m_DVR_out_of_core_page_table_mat;

                    m_brick_cache_size = brick_cache_size;

                    m_brick_cache_nbr_bricks = new(m_brick_cache_size.x / m_brick_size,
                        m_brick_cache_size.y / m_brick_size, m_brick_cache_size.z / m_brick_size);

                    m_brick_cache_brick_residency = new HashSet<UInt32>();

                    // clear any previously set UAVs
                    Graphics.ClearRandomWriteTargets();

                    // initialize the brick requests buffer and data
                    InitializeBrickRequestsBuffer();

                    // initialize the brick cache usage buffer
                    InitializeBrickCacheUsage();

                    // initialize brick requests random texture
                    InitializeBrickRequestsRandomTex();

                    // initialize the page table(s)
                    InitializePageDirectory();

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

                    Vector4[] volume_dims = new Vector4[m_metadata.NbrResolutionLvls];
                    for (int i = 0; i < volume_dims.Length; ++i)
                    {
                        volume_dims[i] = new Vector4(Mathf.Ceil(m_metadata.Dims.x / (float)(1 << i)),
                            Mathf.Ceil(m_metadata.Dims.y / (float)(1 << i)),
                            Mathf.Ceil(m_metadata.Dims.z / (float)(1 << i)));
                    }
                    m_material.SetVectorArray(SHADER_VOLUME_DIMS_ID, volume_dims);

                    Vector3 volume_texel_size = new(1.0f / m_metadata.Dims.x,
                        1.0f / m_metadata.Dims.y, 1.0f / m_metadata.Dims.z);
                    m_material.SetVector(SHADER_VOLUME_TEXEL_SIZE_ID, volume_texel_size);

                    // set remaining shader variables
                    m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_PER_RAY_ID, MAX_NBR_BRICK_REQUESTS_PER_RAY);
                    m_material.SetInteger(SHADER_MAX_NBR_BRICK_REQUESTS_ID, MAX_NBR_BRICK_REQUESTS_PER_FRAME);
                    m_material.SetInteger(SHADER_BRICK_SIZE_ID, m_brick_size);
                    m_material.SetInteger(SHADER_MAX_RES_LVL_ID, m_metadata.NbrResolutionLvls - 1);

                    Vector4 brick_cache_voxel_size = new(1.0f / m_brick_cache_size.x,
                        1.0f / m_brick_cache_size.y, 1.0f / m_brick_cache_size.z);
                    m_material.SetVector(SHADER_BRICK_CACHE_VOXEL_SIZE, brick_cache_voxel_size);

                    m_material.SetVector(SHADER_BRICK_CACHE_DIMS_ID, new Vector3(m_brick_cache_size.x,
                        m_brick_cache_size.y, m_brick_cache_size.z));

                    Vector3 brick_cache_nbr_bricks = new(m_brick_cache_nbr_bricks.x,
                        m_brick_cache_nbr_bricks.y, m_brick_cache_nbr_bricks.z);
                    m_material.SetVector(SHADER_BRICK_CACHE_NBR_BRICKS, brick_cache_nbr_bricks);

                    // scale mesh to match correct dimensions of the original volumetric data
                    m_transform.localScale = new Vector3(
                         MM_TO_METERS * m_metadata.VoxelDims.x * Mathf.Ceil(m_metadata.Dims.x / (1 << m_resolution_lvl)),
                         MM_TO_METERS * m_metadata.VoxelDims.y * Mathf.Ceil(m_metadata.Dims.y / (1 << m_resolution_lvl)),
                         MM_TO_METERS * m_metadata.VoxelDims.z * Mathf.Ceil(m_metadata.Dims.z / (1 << m_resolution_lvl))
                    );

                    // finally start the loop
                    StartCoroutine(OOCPageTableOnlyLoop());

                    break;
                }
                default:
                break;
            }

            // avoid overflow errors
            m_brick_cache_size_mb = (m_brick_cache_size.x / 1024.0f) * (m_brick_cache_size.y / 1024.0f)
                * m_brick_cache_size.z;

            // log useful info
            Debug.Log($"rendering mode set to: {m_rendering_mode}");
            Debug.Log($"number of frames in flight: {QualitySettings.maxQueuedFrames}");
            Debug.Log($"brick cache size dimensions: {m_brick_cache_size}");
            Debug.Log($"brick cache size: {m_brick_cache_size_mb}MB");

            // initialize object pools
            m_tex_params_pool = new(MAX_NBR_BRICK_UPLOADS_PER_FRAME);

            // rotate the volume according to provided Euler angles
            m_transform.localRotation = Quaternion.Euler(m_metadata.EulerRotation);

            // debugging stuff ...
            m_wireframe_cube_mesh = WireframeCubeMesh.GenerateMesh();

            StartCoroutine(InternalInit());
        }


        private void CreateBrickCacheTexture3D()
        {
            m_brick_cache = new Texture3D(m_brick_cache_size.x, m_brick_cache_size.y, m_brick_cache_size.z,
                m_brick_cache_format, mipChain: false, createUninitialized: false);  // TODO: set back to true

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
                width = (UInt32)m_brick_cache_size.x,
                height = (UInt32)m_brick_cache_size.y,
                depth = (UInt32)m_brick_cache_size.z,
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

            m_brick_cache = Texture3D.CreateExternalTexture(m_brick_cache_size.x, m_brick_cache_size.y,
                m_brick_cache_size.z, m_brick_cache_format, mipChain: false, nativeTex: m_brick_cache_ptr);

            // this has to be overwritten for Vulkan to work because Unity expects a VkImage* for the nativeTex
            // paramerter not a VkImage. GetNativeTexturePtr does not actually return a VkImage* as it claims
            // but rather a VkImage => This is probably a bug.
            // (see https://docs.unity3d.com/6000.0/Documentation/ScriptReference/Texture3D.CreateExternalTexture.html)
            m_brick_cache_ptr = m_brick_cache.GetNativeTexturePtr();

            m_is_brick_cache_nativaly_created = true;
        }

        private void InitializeBrickRequestsBuffer()
        {
            m_brick_requests_cb = new ComputeBuffer(MAX_NBR_BRICK_REQUESTS_PER_FRAME, sizeof(UInt32),
                ComputeBufferType.Default);
            m_brick_requests_default_data = new UInt32[MAX_NBR_BRICK_REQUESTS_PER_FRAME];
            for (int i = 0; i < MAX_NBR_BRICK_REQUESTS_PER_FRAME; ++i)
            {
                m_brick_requests_default_data[i] = INVALID_BRICK_ID;
            }
            m_material.SetBuffer(SHADER_BRICK_REQUESTS_BUFFER_ID, m_brick_requests_cb);
            Graphics.SetRandomWriteTarget(1, m_brick_requests_cb, true);
            GPUResetBrickRequestsBuffer();
            Debug.Log("brick requests buffer initialized successfully");
        }


        private void InitializeBrickCacheUsage()
        {
            int brick_cache_usage_size = m_brick_cache_nbr_bricks.x * m_brick_cache_nbr_bricks.y
                * m_brick_cache_nbr_bricks.z;
            m_brick_cache_usage_cb = new ComputeBuffer(brick_cache_usage_size, sizeof(float));
            m_brick_cache_usage_default_data = new float[brick_cache_usage_size];
            m_brick_cache_usage_tmp = new float[brick_cache_usage_size];
            m_brick_cache_usage_sorted = new BrickCacheUsage[brick_cache_usage_size];
            m_brick_cache_usage = new BrickCacheUsage[brick_cache_usage_size];
            for (int i = 0; i < brick_cache_usage_size; ++i)
            {
                // 0 means the brick cache slot with index i is unused for that frame.
                // Any other value means it was used for than frame.
                m_brick_cache_usage_default_data[i] = 0;
                m_brick_cache_usage[i] = new BrickCacheUsage()
                {
                    brick_id = INVALID_BRICK_ID,  // invalid brick ID => free slot
                    brick_cache_idx = i,
                    timestamp = 0                 // 0 so that when sorted, free slots are placed first
                };
            }
            m_material.SetBuffer(SHADER_BRICK_CACHE_USAGE_ID, m_brick_cache_usage_cb);
            Graphics.SetRandomWriteTarget(2, m_brick_cache_usage_cb, true);
            GPUResetBrickCacheUsageBuffer();

            Debug.Log($"brick cache usage buffer elements count: {brick_cache_usage_size}");
            Debug.Log($"brick cache usage size: {brick_cache_usage_size * sizeof(Int32) / 1024.0f} KB");
            Debug.Log("brick cache usage initialized successfully");
        }


        private void InitializeResidencyOctree()
        {

            // import the residency octree from filesystem
            m_residency_octree_data = Importer.ImportResidencyOctree(m_volume_dataset.Metadata).ToArray();

            m_octree_changed_node_indices = new HashSet<int>();

            m_max_octree_depth = (int)-(1 + Mathf.Log(m_residency_octree_data[m_residency_octree_data.Length - 1].side_halved, 2));

            int residency_octree_nodes_count = (int)((Mathf.Pow(8, m_max_octree_depth + 1) - 1) / 7);
            m_residency_octree_cb = new ComputeBuffer(residency_octree_nodes_count,
                Marshal.SizeOf<ResidencyNode>(), ComputeBufferType.Structured, ComputeBufferMode.Dynamic);

            m_material.SetBuffer(SHADER_RESIDENCY_OCTREE_BUFFER_ID, m_residency_octree_cb);
            GPUUpdateResidencyOctree(new List<BrickCacheUsage>(), new List<BrickCacheUsage>());

            Debug.Log($"residency octree node struct size: {Marshal.SizeOf<ResidencyNode>()} bytes");
            Debug.Log($"max residency octree depth: {m_max_octree_depth}");
            Debug.Log("residency octree loaded successfully");

        }


        private void InitializeBrickRequestsRandomTex()
        {
            m_brick_requests_random_tex = new Texture2D(m_brick_requests_random_tex_size, m_brick_requests_random_tex_size,
                TextureFormat.R8, mipChain: false, linear: true, createUninitialized: true);

            m_brick_requests_random_tex.wrapModeU = TextureWrapMode.Repeat;
            m_brick_requests_random_tex.wrapModeV = TextureWrapMode.Repeat;

            m_brick_requests_random_tex.filterMode = FilterMode.Point;

            m_brick_requests_random_tex_data = new byte[m_brick_requests_random_tex_size * m_brick_requests_random_tex_size];
            m_material.SetTexture(SHADER_BRICK_REQUESTS_RANDOM_TEX_ID, m_brick_requests_random_tex);

            for (int i = 0; i < m_brick_requests_random_tex_data.Length; ++i)
            {
                // we want random number from [0, 254] because 255 causes the normalized value to be 1.0 which
                // breaks array indexing in the out-of-core DVR shader
                m_brick_requests_random_tex_data[i] = (byte)UnityEngine.Random.Range(0, 255);
            }
            m_brick_requests_random_tex.SetPixelData(m_brick_requests_random_tex_data, 0);
            m_brick_requests_random_tex.Apply();
        }


        /// <summary>
        ///     Initializes the top level page directory with other optional intermediary page tables.
        /// </summary>
        /// 
        /// <exception cref="Exception">
        ///     thrown when the target platform does not support RGBA64 texture format
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

            Debug.Log($"page dir [x, y, z]: [{page_dir_dims.x}, {page_dir_dims.y}, {page_dir_dims.z}]");
            Debug.Log($"page dir total number of pages: {page_dir_dims.x * page_dir_dims.y * page_dir_dims.z}");

            m_page_dir_data = new float[page_dir_data_size];
            // set the alpha components to UNMAPPED
            for (int i = 0; i < page_dir_data_size; i += 4)
            {
                m_page_dir_data[i + 3] = UNMAPPED_PAGE_TABLE_ENTRY;
            }

            if (!SystemInfo.SupportsTextureFormat(TextureFormat.RGBAFloat))
            {
                throw new Exception("your system does not support RGBAFloat - float32 per channel texture format.");
            }

            m_page_dir = new Texture3D(page_dir_dims.x, page_dir_dims.y, page_dir_dims.z,
                TextureFormat.RGBAFloat, mipChain: false, createUninitialized: true);

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

        private IEnumerator InternalInit()
        {

            Debug.Log("VolumetricObject: waiting for volume dataset and transfer function to be non null ...");

            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null));

            Debug.Log("VolumetricObject: started internal init");

            // create the brick cache texture(s) natively in case of OpenGL/Vulkan to overcome
            // the 2GBs Unity/.NET Texture3D size limit. For Direct3D11/12 we don't have to create the textures
            // using the native plugin since these APIs already impose a 2GBs per-resource limit
            //
            // Important: if the texture is created using Unity's Texture3D with createUninitialized set to true
            // and you try to visualize some uninitailized blocks you might observe some artifacts (duh?!)
            if (ForceNativeTextureCreation)
            {
                Debug.Log("forcing native 3D texture creation");
                yield return CreateNativeBrickCacheTexture3D();
            }
            else if (m_brick_cache_size_mb < 2048)
            {
                Debug.Log($"requested brick cache size{m_brick_cache_size_mb}MB is less than 2GB."
                    + " Using Unity's API to create the 3D texture");
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
            m_material.SetFloat(SHADER_SAMPLING_QUALITY_FACTOR_ID, 1.0f);
            // m_material.SetFloat(SHADER_LOD_QUALITY_FACTOR_ID, 2.0f);

        }


        private void OnEnable()
        {
            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange += OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange += OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
        }

        private void OnDisable()
        {
            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange -= OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange -= OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;

            if (m_transfer_function != null)
            {
                m_transfer_function.TFColorsLookupTexChange -= OnTFColorsLookupTexChange;
            }

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
            Debug.Log("started loading all volume bricks into GPU ...");

            long nbr_bricks_uploaded = 0;
            long total_nbr_bricks = m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x *
                m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y *
                m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z *
                (int)Math.Pow(m_metadata.ChunkSize / m_brick_size, 3);
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[MAX_NBR_BRICK_UPLOADS_PER_FRAME];
            Vector3 brick_scale = new(
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * m_metadata.ChunkSize),
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * m_metadata.ChunkSize),
                m_brick_size / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * m_metadata.ChunkSize)
            );

            while (nbr_bricks_uploaded < total_nbr_bricks)
            {

                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < MAX_NBR_BRICK_UPLOADS_PER_FRAME &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned to a
                    // fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);
                    Assert.IsNotNull(brick);
                    handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);

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

#if DEBUG_VERBOSE_2
                    Debug.Log($"brick id: i={brick_id}; volume offset: x={x} y={y} z={z}");
#endif

                    if (InstantiateBrickWireframes)
                    {
                        GameObject brick_wireframe = Instantiate(m_brick_wireframe, gameObject.transform, false);
                        brick_wireframe.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
                        brick_wireframe.transform.localPosition = new Vector3(
                            (x / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * m_metadata.ChunkSize) - 0.5f) + brick_scale.x / 2.0f,
                            (y / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * m_metadata.ChunkSize) - 0.5f) + brick_scale.y / 2.0f,
                            (z / (float)(m_metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * m_metadata.ChunkSize) - 0.5f) + brick_scale.z / 2.0f
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

            Debug.Log($"uploading all {total_nbr_bricks} bricks to GPU took: {stopwatch.Elapsed}s");

            while (true)
            {
                yield return new WaitForEndOfFrame();
                GPUUpdateVisualizationParams();
            }
        }

        private void GPUGetBrickRequests(UInt32[] brick_requests)
        {
            m_brick_requests_cb.GetData(brick_requests);
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
        ///     Imports bricks into the CPU-memory brick cache if they are not already resident there.
        ///     Brick IDs set to INVALID_BRICK_ID are simply ignored.
        /// </summary>
        /// 
        /// <param name="brick_ids">
        ///     Collection of brick IDs to try to load into the CPU-memory cache.
        /// </param>
        private void ImportBricksIntoMemoryCache(UInt32[] brick_ids, int nbr_importer_threads = -1)
        {
            UInt32[] filtered_brick_ids = new UInt32[MAX_NBR_BRICK_REQUESTS_PER_FRAME];

            // first cleanup the brick requests list (remove duplicates, invalid IDs,
            // bricks already in cache, and bricks that are currently being loaded)
            int count = 0;
            for (int i = 0; i < brick_ids.Length; ++i)
            {
                if ((brick_ids[i] != INVALID_BRICK_ID) && !m_cpu_cache.Contains(brick_ids[i])
                    && !m_in_flight_brick_imports.ContainsKey(brick_ids[i]))
                {
                    /*
                    if (m_cpu_cache.Contains(brick_ids[i]))
                    {
                        m_brick_reply_queue.Enqueue(brick_ids[i]);
                        continue;
                    }
                    */
                    // save this brick IDs so that future imports know it is being imported
                    // this is semantically a HashSet, the value 0 is fully arbitrary
                    m_in_flight_brick_imports[brick_ids[i]] = 0;
                    filtered_brick_ids[count] = brick_ids[i];
                    ++count;
                }
            }

            if (count == 0)
            {
                // finally, no more brick requests - the GPU must be so happy
                if (m_in_flight_brick_imports.Count == 0)
                    OnNoMoreBrickRequests?.Invoke();
                return;
            }

            int nbr_threads = nbr_importer_threads > 0 ? nbr_importer_threads :
                Math.Max(Environment.ProcessorCount - 2, 1);

            Task t = Task.Run(() =>
            {
                Parallel.For(0, count, new ParallelOptions()
                {
                    TaskScheduler = new LimitedConcurrencyLevelTaskScheduler(nbr_threads)
                }, i =>
                {
                    UInt32 brick_id = filtered_brick_ids[i];
                    Importer.ImportBrick(m_metadata, brick_id, m_brick_size, m_cpu_cache);
                    m_in_flight_brick_imports.Remove(brick_id, out byte _);
                    m_brick_reply_queue.Enqueue(brick_id);
                });
            });
            t.ContinueWith(t => { Debug.LogException(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        private bool IsBrickCacheSlotEmpty(BrickCacheUsage slot) => slot.brick_id == INVALID_BRICK_ID;

        /// <summary>
        ///     Out-of-core virtual memory loop. This loop handles on-demand GPU brick requests,
        ///     manages the different caches, and synchronizes CPU-GPU resources.
        /// </summary>
        public IEnumerator OOCPageTableOnlyLoop()
        {

            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null)
                && (m_brick_cache != null));

            Debug.Log("started handling GPU brick requests");

            long nbr_bricks_uploaded = 0;
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[MAX_NBR_BRICK_UPLOADS_PER_FRAME];

            UInt32[] brick_requests = new UInt32[MAX_NBR_BRICK_REQUESTS_PER_FRAME];

            List<BrickCacheUsage> brick_cache_added_slots = new();
            List<BrickCacheUsage> brick_cache_evicted_slots = new();

            InitializePageDirectory();

            while (true)
            {

                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                bool page_directory_dity = false;

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // get bricks requested by GPU in this frame and import them into bricks memory cache
                GPUGetBrickRequests(brick_requests);
                GPUResetBrickRequestsBuffer();
                ImportBricksIntoMemoryCache(brick_requests, m_nbr_brick_importer_threads);

                // update CPU brick cache usage and reset it on the GPU
                CPUUpdateBrickCacheUsageBuffer();
                GPUResetBrickCacheUsageBuffer();

                GPURandomizeBrickRequestsTexOffset();
                GPUUpdateVisualizationParams();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < MAX_NBR_BRICK_UPLOADS_PER_FRAME &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned
                    // to a fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);
                    Assert.IsNotNull(brick);

                    // in case the brick is homogeneous, avoid adding it the cache and just update the
                    // corresponding page entry
                    if (brick.min == brick.max)
                    {
                        page_directory_dity = true;
                        UpdatePageTablesHomogeneousBrick(brick_id, brick.min);
                        Debug.Log($"brick {brick_id} is homogeneous with val: {brick.min}");
                        continue;
                    }

                    handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);

                    // LRU cache eviction scheme; pick least recently used brick cache slot
                    int brick_cache_idx = m_brick_cache_usage_sorted[nbr_bricks_uploaded_per_frame].brick_cache_idx;
                    BrickCacheUsage evicted_slot = m_brick_cache_usage[brick_cache_idx];
                    BrickCacheUsage added_slot = new()
                    {
                        brick_id = brick_id,
                        timestamp = m_timestamp,
                        brick_cache_idx = brick_cache_idx
                    };
                    // check if evicted brick slot is already empty
                    if (!IsBrickCacheSlotEmpty(evicted_slot))
                    {
                        brick_cache_evicted_slots.Add(evicted_slot);
                    }
                    brick_cache_added_slots.Add(added_slot);
                    m_brick_cache_usage[brick_cache_idx] = added_slot;
                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3Int brick_cache_slot_pos);

                    // allocate the plugin call's arguments struct
                    TextureSubImage3DParams args = new()
                    {
                        texture_handle = m_brick_cache_ptr,
                        xoffset = brick_cache_slot_pos.x,
                        yoffset = brick_cache_slot_pos.y,
                        zoffset = brick_cache_slot_pos.z,
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

#if DEBUG_VERBOSE_2
                    Debug.Log($"brick id: {brick_id}; brick cache offset: x={brick_cache_slot_pos.x} y={brick_cache_slot_pos.y} z={brick_cache_slot_pos.z}");
#endif
                    ++nbr_bricks_uploaded_per_frame;

                }  // END WHILE

                // we execute the command buffer instantly
                Graphics.ExecuteCommandBuffer(cmd_buffer);
                cmd_buffer.Clear();

                if (page_directory_dity || (brick_cache_added_slots.Count > 0)
                    || (brick_cache_evicted_slots.Count > 0))
                {

                    GPUUpdatePageTables(brick_cache_added_slots, brick_cache_evicted_slots);

                    if (InstantiateBrickWireframes)
                    {
                        foreach (var added_slot in brick_cache_added_slots)
                        {
                            OOCAddBrickWireframeObject(added_slot.brick_id);
                        }
                        foreach (var evicted_slot in brick_cache_evicted_slots)
                        {
                            Destroy(transform.Find($"brick_{evicted_slot.brick_id & 0x03FFFFFF}_res_lvl_{evicted_slot.brick_id >> 26}").gameObject);
                        }
                    }

                    brick_cache_added_slots.Clear();
                    brick_cache_evicted_slots.Clear();

                }

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
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null) && (m_brick_cache != null));

            Debug.Log("started handling GPU brick requests");

            CVDSMetadata metadata = m_volume_dataset.Metadata;
            long nbr_bricks_uploaded = 0;
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[MAX_NBR_BRICK_UPLOADS_PER_FRAME];

            UInt32[] brick_requests = new UInt32[MAX_NBR_BRICK_REQUESTS_PER_FRAME];

            List<BrickCacheUsage> brick_cache_added_slots = new();
            List<BrickCacheUsage> brick_cache_evicted_slots = new();

            while (true)
            {

                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // get bricks requested by GPU in this frame and import them into bricks memory cache
                GPUGetBrickRequests(brick_requests);
                GPUResetBrickRequestsBuffer();
                ImportBricksIntoMemoryCache(brick_requests);

                // update CPU brick cache usage and reset it on the GPU
                CPUUpdateBrickCacheUsageBuffer();
                GPUResetBrickCacheUsageBuffer();

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < MAX_NBR_BRICK_UPLOADS_PER_FRAME &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                )
                {

                    // LRU cache eviction scheme; pick least recently used brick cache slot
                    int brick_cache_idx = m_brick_cache_usage_sorted[nbr_bricks_uploaded_per_frame].brick_cache_idx;
                    BrickCacheUsage evicted_slot = m_brick_cache_usage[brick_cache_idx];
                    BrickCacheUsage added_slot = new() { brick_id = brick_id, timestamp = m_timestamp };
                    // check if evicted brick slot is already empty
                    if (!IsBrickCacheSlotEmpty(evicted_slot))
                    {
                        brick_cache_evicted_slots.Add(evicted_slot);
                    }
                    brick_cache_added_slots.Add(added_slot);
                    m_brick_cache_usage[brick_cache_idx] = added_slot;
                    GetBrickCacheSlotPosition(brick_cache_idx, out Vector3Int brick_cache_slot_pos);

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned to a
                    // fixed location in memory during the plugin call
                    var brick = m_cpu_cache.Get(brick_id);
                    Assert.IsNotNull(brick);
                    handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);

                    // allocate the plugin call's arguments struct
                    TextureSubImage3DParams args = new()
                    {
                        texture_handle = m_brick_cache_ptr,
                        xoffset = brick_cache_slot_pos.x,
                        yoffset = brick_cache_slot_pos.y,
                        zoffset = brick_cache_slot_pos.z,
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

#if DEBUG_VERBOSE_2
                    Debug.Log($"brick id: {brick_id}; brick cache offset: x={brick_cache_slot_pos.x} y={brick_cache_slot_pos.y} z={brick_cache_slot_pos.z}");
#endif
                    ++nbr_bricks_uploaded_per_frame;

                }  // END WHILE

                // we execute the command buffer instantly
                Graphics.ExecuteCommandBuffer(cmd_buffer);
                cmd_buffer.Clear();

                if ((brick_cache_added_slots.Count > 0) || (brick_cache_evicted_slots.Count > 0))
                {
                    // make sure to update the brick cache bricks residency HashSet before updating the residency octree!
                    UpdateBrickCacheResidencyHashSet(brick_cache_added_slots, brick_cache_evicted_slots);
                    GPUUpdateResidencyOctree(brick_cache_added_slots, brick_cache_evicted_slots);
                    GPUUpdatePageTables(brick_cache_added_slots, brick_cache_evicted_slots);

                    if (InstantiateBrickWireframes)
                    {
                        foreach (var added_slot in brick_cache_added_slots)
                        {
                            OOCAddBrickWireframeObject(added_slot.brick_id);
                        }
                        foreach (var evicted_slot in brick_cache_evicted_slots)
                        {
                            Destroy(transform.Find($"brick_{evicted_slot.brick_id & 0x03FFFFFF}_res_lvl_{evicted_slot.brick_id >> 26}").gameObject);
                        }
                    }
                }

                brick_cache_added_slots.Clear();
                brick_cache_evicted_slots.Clear();

                // increase timestamp
                ++m_timestamp;

            }  // END WHILE

        }  // END COROUTINE

        private void OOCAddBrickWireframeObject(UInt32 brick_id)
        {

            int id = (int)(brick_id & 0x03FFFFFF);
            int res_lvl = (int)(brick_id >> 26);

            Vector3 brick_scale = new(
                (m_brick_size << res_lvl) / (float)(m_metadata.Dims.x),
                (m_brick_size << res_lvl) / (float)(m_metadata.Dims.y),
                (m_brick_size << res_lvl) / (float)(m_metadata.Dims.z)
            );

            // transition to Unity's Texture3D coordinate system
            Vector4 nbr_bricks = m_nbr_bricks_per_res_lvl[res_lvl];
            int x = m_brick_size * (id % (int)nbr_bricks.x);
            int y = m_brick_size * ((id / (int)nbr_bricks.x) % (int)nbr_bricks.y);
            int z = m_brick_size * (id / ((int)nbr_bricks.x * (int)nbr_bricks.y));

            GameObject brick_wireframe = Instantiate(m_brick_wireframe, gameObject.transform, false);
            brick_wireframe.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
            brick_wireframe.transform.localPosition = new Vector3(
                (x / (float)(m_metadata.Dims.x) - 0.5f) + brick_scale.x / 2.0f,
                (y / (float)(m_metadata.Dims.y) - 0.5f) + brick_scale.y / 2.0f,
                (z / (float)(m_metadata.Dims.z) - 0.5f) + brick_scale.z / 2.0f
            );
            brick_wireframe.transform.localScale = brick_scale;
            brick_wireframe.name = $"brick_{brick_id & 0x03FFFFFF}_res_lvl_{brick_id >> 26}";

        }


        private void GPUResetBrickRequestsBuffer()
        {
            m_brick_requests_cb.SetData(m_brick_requests_default_data);
        }


        private void CPUUpdateBrickCacheUsageBuffer()
        {
            m_brick_cache_usage_cb.GetData(m_brick_cache_usage_tmp);
            for (int i = 0; i < m_brick_cache_usage_tmp.Length; ++i)
            {
                // filter unused brick slots (0 indicates unused brick slot)
                if (m_brick_cache_usage_tmp[i] != 0)
                {
                    m_brick_cache_usage[i].timestamp = m_timestamp;
                }
            }
            // sort the just-updated brick cache usage array by timestamp.
            // empty brick slots should be at the head of the array
            m_brick_cache_usage.CopyTo(m_brick_cache_usage_sorted, 0);
            Array.Sort(m_brick_cache_usage_sorted, (BrickCacheUsage a, BrickCacheUsage b)
                => a.timestamp.CompareTo(b.timestamp));
        }

        private void GPUResetBrickCacheUsageBuffer()
        {
            m_brick_cache_usage_cb.SetData(m_brick_cache_usage_default_data);
        }

        private void GetBrickCacheSlotPosition(int brick_cache_idx, out UInt32 x, out UInt32 y, out UInt32 z)
        {
            x = (UInt32)((brick_cache_idx % m_brick_cache_nbr_bricks.x) * m_brick_size);
            y = (UInt32)(((brick_cache_idx / m_brick_cache_nbr_bricks.x) % m_brick_cache_nbr_bricks.y) * m_brick_size);
            z = (UInt32)((brick_cache_idx / (m_brick_cache_nbr_bricks.x * m_brick_cache_nbr_bricks.y)) * m_brick_size);
        }

        private void GetBrickCacheSlotPosition(int brick_cache_idx, out Vector3 pos)
        {
            pos = new Vector3(
                (brick_cache_idx % m_brick_cache_nbr_bricks.x) * m_brick_size / (float)m_brick_cache.width,
                ((brick_cache_idx / m_brick_cache_nbr_bricks.x) % m_brick_cache_nbr_bricks.y) * m_brick_size / (float)m_brick_cache.height,
                (brick_cache_idx / (m_brick_cache_nbr_bricks.x * m_brick_cache_nbr_bricks.y)) * m_brick_size / (float)m_brick_cache.depth
            );
        }

        private void GetBrickCacheSlotPosition(int brick_cache_idx, out Vector3Int pos)
        {
            pos = new Vector3Int(
                (brick_cache_idx % m_brick_cache_nbr_bricks.x) * m_brick_size,
                ((brick_cache_idx / m_brick_cache_nbr_bricks.x) % m_brick_cache_nbr_bricks.y) * m_brick_size,
                (brick_cache_idx / (m_brick_cache_nbr_bricks.x * m_brick_cache_nbr_bricks.y)) * m_brick_size
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

        // TODO: remove per-frame heap allocations
        private void GPUUpdateResidencyOctree(List<BrickCacheUsage> added_slots, List<BrickCacheUsage> evicted_slots)
        {

            m_octree_changed_node_indices.Clear();

            List<UInt32> overlapping_bricks = new();
            List<int> leaf_node_indices = new();

            foreach (var added_slot in added_slots)
            {
                GetOverlappingLeafNodes(added_slot.brick_id, ref leaf_node_indices);
                // mark overlapping leaf nodes as partially mapped in the brick's res level
                int res_lvl = (int)(added_slot.brick_id >> 26);
                foreach (int idx in leaf_node_indices)
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
                GetOverlappingLeafNodes(evicted_slot.brick_id, ref leaf_node_indices);
                int res_lvl = (int)(evicted_slot.brick_id >> 26);
                // check if overlapping nodes are still partially mapped
                foreach (int idx in leaf_node_indices)
                {
                    GetOverlappingBricks(idx, res_lvl, ref overlapping_bricks);
                    bool at_least_one_brick_in_brick_cache = false;
                    foreach (UInt32 id in overlapping_bricks)
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

            return idx;
        }

        // TODO: adjust this so that it handles all allowed resolution levels not just res lvl 0
        private void GPUUpdatePageTables(List<BrickCacheUsage> added_slots, List<BrickCacheUsage> evicted_slots)
        {

            bool dirty = false;

            foreach (var added_slot in added_slots)
            {
                int idx = GetPageTableIndex(added_slot.brick_id) * 4;
                GetBrickCacheSlotPosition(added_slot.brick_cache_idx, out Vector3 pos);
                m_page_dir_data[idx] = pos.x;
                m_page_dir_data[idx + 1] = pos.y;
                m_page_dir_data[idx + 2] = pos.z;
                m_page_dir_data[idx + 3] = MAPPED_PAGE_TABLE_ENTRY;
                dirty = true;

            }

            foreach (var evicted_slot in evicted_slots)
            {
                int idx = GetPageTableIndex(evicted_slot.brick_id) * 4;
                // set the alpha component to UNMAPPED so the shader knows
                m_page_dir_data[idx + 3] = UNMAPPED_PAGE_TABLE_ENTRY;
                dirty = true;
            }


            if (dirty)
            {
                m_page_dir.SetPixelData(m_page_dir_data, mipLevel: 0);
                m_page_dir.Apply();
            }

        }

        private void UpdatePageTablesHomogeneousBrick(UInt32 brick_id, byte val)
        {
            int idx = GetPageTableIndex(brick_id) * 4;
            // x channel has to be set to the homogeneous value
            // on the shader side, do (page_entry.x / 255.0f) to convert to the correct value
            m_page_dir_data[idx] = val;
            m_page_dir_data[idx + 3] = HOMOGENEOUS_PAGE_TABLE_ENTRY;
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

        void OnTFColorsLookupTexChange(Texture2D new_colors_lookup_tex)
        {
            m_material.SetTexture(SHADER_TFTEX_ID, new_colors_lookup_tex);
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

        private void OnModelLODQualityFactorChange(float value)
        {
            m_vis_params_dirty = true;
            m_lod_quality_factor = value;
            m_lod_quality_factor = value;
        }

        private float m_alpha_cutoff;
        private float m_sampling_quality_factor;
        private float m_lod_quality_factor;
        private void GPUUpdateVisualizationParams()
        {
            if (!m_vis_params_dirty)
                return;

            m_material.SetFloat(SHADER_ALPHA_CUTOFF_ID, m_alpha_cutoff);
            m_material.SetFloat(SHADER_SAMPLING_QUALITY_FACTOR_ID, m_sampling_quality_factor);
            m_material.SetFloat(SHADER_LOD_QUALITY_FACTOR_ID, m_lod_quality_factor);
            m_vis_params_dirty = false;
        }

        private void OnModelTFChange(TF tf, ITransferFunction tf_so)
        {
            if (m_transfer_function != null)
                m_transfer_function.TFColorsLookupTexChange -= OnTransferFunctionTexChange;
            m_transfer_function = tf_so;
            m_transfer_function.TFColorsLookupTexChange += OnTFColorsLookupTexChange;
            m_transfer_function.ForceUpdateColorLookupTexture();
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
