using System;
using System.Collections;
using System.Collections.Concurrent;
using System.Runtime.InteropServices;
using System.Threading.Tasks;
using TextureSubPlugin;
using UnityEngine;
using UnityEngine.Assertions;
using UnityEngine.Rendering;

namespace UnityCTVisualizer {

    public enum RenderingMode {
        IN_CORE = 0,
        OUT_OF_CORE = 1,
    }

    [RequireComponent(typeof(MeshRenderer)), RequireComponent(typeof(MeshFilter))]
    public class VolumetricObject : MonoBehaviour {

        private readonly int SHADER_BRICK_CACHE_TEX_ID = Shader.PropertyToID("_BrickCache");
        private readonly int SHADER_BRICK_CACHE_TEX_SIZE_ID = Shader.PropertyToID("_BrickCacheTexSize");
        private readonly int SHADER_TFTEX_ID = Shader.PropertyToID("_TFColors");
        private readonly int SHADER_ALPHA_CUTOFF_ID = Shader.PropertyToID("_AlphaCutoff");
        private readonly int SHADER_MAX_ITERATIONS_ID = Shader.PropertyToID("_MaxIterations");
        private readonly int SHADER_BRICK_CACHE_USAGE_BUFFER = Shader.PropertyToID("_BrickCacheUsageBuffer");
        private readonly int SHADER_BRICK_CACHE_MISSES_BUFFER = Shader.PropertyToID("_BrickCacheMissesBuffer");
        private readonly int MAX_NBR_BRICK_REQUESTS_PER_FRAME = 10;


        private VolumetricDataset m_volume_dataset = null;
        private ITransferFunction m_transfer_function = null;

        /////////////////////////////////
        // COROUTINES
        /////////////////////////////////
        private Coroutine m_interpolation_method_update;
        private Coroutine m_max_iterations_update;

        /////////////////////////////////
        // PARAMETERS
        /////////////////////////////////
        private RenderingMode m_rendering_mode;
        private Texture3D m_brick_cache = null;
        private int m_brick_cache_nbr_bricks;
        private IntPtr m_brick_cache_ptr = IntPtr.Zero;
        private TextureFormat m_brick_cache_format;
        private int m_tex_plugin_format;
        private int m_brick_size = 128;
        private Vector3Int m_brick_cache_size;
        private float m_brick_cache_size_mb;
        private int m_resolution_lvl;  // only for in-core rendering
        private float MM_TO_METERS = 0.001f;


        // TODO: I don't know how to make this less ugly...
        private MemoryCache<UInt16> m_cache_uint16;
        private MemoryCache<byte> m_cache_uint8;

        // private ComputeBuffer m_brick_cache_usage_buffer = null;
        // private ComputeBuffer m_brick_cache_misses_buffer = null;
        // private System.UInt32[] m_brick_cache_usage_buffer_reset;


        /////////////////////////////////
        // OBJECT POOLS
        /////////////////////////////////
        private UnmanagedObjectPool<TextureSubImage3DParams> m_tex_params_pool;


        /////////////////////////////////
        // DEBUGGING
        /////////////////////////////////
        [SerializeField] private GameObject m_brick_wireframe;
        private Mesh m_wireframe_cube_mesh;
        public bool InstantiateBrickWireframes = false;
        public bool ForceNativeTextureCreation = true;

        private Transform m_transform;
        private Material m_material;
        private IProgressHandler m_progress_handler;

        private ConcurrentQueue<UInt32> m_brick_reply_queue = new();

        private void Awake() {
            m_transform = GetComponent<Transform>();
            m_material = GetComponent<MeshRenderer>().sharedMaterial;
        }

        public void Init(VolumetricDataset volumetricDataset, RenderingMode rendering_mode,
            int brick_cache_dim_size = -1, int resolution_lvl = 0, IProgressHandler progressHandler = null) {

            if (volumetricDataset == null)
                throw new ArgumentNullException("the provided volumetric dataset should be a non-null reference");

            m_rendering_mode = rendering_mode;
            m_volume_dataset = volumetricDataset;
            m_progress_handler = progressHandler;
            m_resolution_lvl = resolution_lvl;

            CVDSMetadata metadata = volumetricDataset.Metadata;
            if (rendering_mode == RenderingMode.IN_CORE) {
                if (m_resolution_lvl < 0 || m_resolution_lvl >= metadata.NbrResolutionLvls) {
                    throw new Exception($"invalid provided resolution level for in-core rendering: {m_resolution_lvl}");
                }
                m_brick_cache_size = new Vector3Int(
                    metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * metadata.ChunkSize,
                    metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * metadata.ChunkSize,
                    metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * metadata.ChunkSize
                );
            } else {
                if (brick_cache_dim_size <= 0 || (brick_cache_dim_size % 2) != 0) {
                    throw new Exception($"invalid provided brick cache dimension size for out-of-core rendering: {brick_cache_dim_size}");
                }
                m_brick_cache_size = new Vector3Int(
                    brick_cache_dim_size,
                    brick_cache_dim_size,
                    brick_cache_dim_size
                );
            }
            m_brick_cache_size_mb = metadata.ColorDepth switch {
                ColorDepth.UINT8 => (m_brick_cache_size.x / 1024.0f) * (m_brick_cache_size.y / 1024.0f)
                                    * m_brick_cache_size.z * sizeof(byte),
                ColorDepth.UINT16 => (m_brick_cache_size.x / 1024.0f) * (m_brick_cache_size.y / 1024.0f)
                                    * m_brick_cache_size.z * sizeof(UInt16),
                _ => throw new NotImplementedException(metadata.ColorDepth.ToString()),
            };

            // log useful info
            Debug.Log($"rendering mode set to: {m_rendering_mode}");
            Debug.Log($"number of frames in flight: {QualitySettings.maxQueuedFrames}");
            Debug.Log($"brick cache size dimensions: {m_brick_cache_size}");
            Debug.Log($"brick cache size: {m_brick_cache_size_mb}MB");

            // initialize object pools
            m_tex_params_pool = new(volumetricDataset.MAX_BRICK_UPLOADS_PER_FRAME);

            // debugging stuff ...
            m_wireframe_cube_mesh = WireframeCubeMesh.GenerateMesh();

            StartCoroutine(InternalInit());
        }

        private void CreateTexture3D() {
            m_brick_cache = new Texture3D(m_brick_cache_size.x, m_brick_cache_size.y, m_brick_cache_size.z,
                m_brick_cache_format, mipChain: false, createUninitialized: true);
            // set texture wrapping to Clamp to remove edge/face artifacts
            m_brick_cache.wrapModeU = TextureWrapMode.Clamp;
            m_brick_cache.wrapModeV = TextureWrapMode.Clamp;
            m_brick_cache.wrapModeW = TextureWrapMode.Clamp;
            m_brick_cache_ptr = m_brick_cache.GetNativeTexturePtr();
            Assert.AreNotEqual(m_brick_cache_ptr, IntPtr.Zero);
        }

        private IEnumerator CreateNativeTexture3D() {
            // make sure that you do not create a resource during a render pass
            yield return new WaitForEndOfFrame();

            CommandBuffer cmd_buffer = new();
            UInt32 texture_id = 0;
            CreateTexture3DParams args = new() {
                texture_id = texture_id,
                width = (UInt32)m_brick_cache_size.x,
                height = (UInt32)m_brick_cache_size.y,
                depth = (UInt32)m_brick_cache_size.z,
                format = m_tex_plugin_format,
            };
            IntPtr args_ptr = Marshal.AllocHGlobal(Marshal.SizeOf<CreateTexture3DParams>());
            Marshal.StructureToPtr(args, args_ptr, false);
            cmd_buffer.IssuePluginEventAndData(API.GetRenderEventFunc(), (int)TextureSubPlugin.Event.CreateTexture3D,
                args_ptr);
            Graphics.ExecuteCommandBuffer(cmd_buffer);
            yield return new WaitForEndOfFrame();
            Marshal.FreeHGlobal(args_ptr);

            m_brick_cache_ptr = API.RetrieveCreatedTexture3D(texture_id);
            if (m_brick_cache_ptr == IntPtr.Zero) {
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
        }

        private IEnumerator InternalInit() {
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null));

            // initialize colordepth-dependent stuff
            switch (m_volume_dataset.Metadata.ColorDepth) {
                case ColorDepth.UINT8: {
                    m_brick_cache_format = TextureFormat.R8;
                    m_tex_plugin_format = (int)TextureSubPlugin.Format.UR8;
                    m_cache_uint8 = new MemoryCache<byte>(m_volume_dataset.MEMORY_CACHE_MB,
                        (int)Math.Pow(m_brick_size, 3));
                    break;
                }
                case ColorDepth.UINT16: {
                    m_brick_cache_format = TextureFormat.R16;
                    m_tex_plugin_format = (int)TextureSubPlugin.Format.UR16;
                    m_cache_uint16 = new MemoryCache<UInt16>(m_volume_dataset.MEMORY_CACHE_MB,
                        (int)Math.Pow(m_brick_size, 3) * sizeof(UInt16));
                    break;
                }
                default:
                throw new Exception($"unknown ColorDepth value: {m_volume_dataset.Metadata.ColorDepth}");
            }

            // create the brick cache texture(s) natively in case of OpenGL/Vulkan to overcome
            // the 2GBs Unity/.NET Texture3D size limit. For Direct3D11/12 we don't have to create the textures
            // using the native plugin since these APIs already impose a 2GBs per-resource limit
            //
            // Important: if the texture is created using Unity's Texture3D with createUninitialized set to true
            // and you try to visualize some uninitailized blocks you might observe some artifacts (duh?!)
            if (ForceNativeTextureCreation) {
                Debug.Log("forcing native 3D texture creation");
                yield return CreateNativeTexture3D();
            } else if (m_volume_dataset.BRICK_CACHE_SIZE_MB <= 2048) {
                Debug.Log($"requested brick cache size{m_volume_dataset.BRICK_CACHE_SIZE_MB}MB is less than 2GB."
                    + " Using Unity's API to create the 3D texture");
                CreateTexture3D();
            } else {
                if (SystemInfo.graphicsDeviceType != GraphicsDeviceType.Vulkan &&
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLCore &&
                    SystemInfo.graphicsDeviceType != GraphicsDeviceType.OpenGLES3) {
                    throw new NotImplementedException("multiple 3D texture brick caches are not yet implemented."
                        + " Choose a smaller than 2GB brick cache or use a different graphics API (e.g., Vulkan/OpenGLCore)");
                }
                yield return CreateNativeTexture3D();
            }

            // set shader attributes
            m_material.SetVector(
                SHADER_BRICK_CACHE_TEX_SIZE_ID,
                    new Vector4(
                        m_brick_cache_size.x,
                        m_brick_cache_size.y,
                        m_brick_cache_size.z,
                        0.0f
                    )
                );
            m_material.SetTexture(
                SHADER_BRICK_CACHE_TEX_ID,
                m_brick_cache
            );

            // TODO: set up buffers
            // m_brick_cache_usage_buffer = new ComputeBuffer(m_volume_dataset.BrickCacheNbrBricks / sizeof(System.UInt32), sizeof(System.UInt32));
            // m_brick_cache_usage_buffer_reset = new System.UInt32[m_volume_dataset.BrickCacheNbrBricks / sizeof(System.UInt32)];

            // Array.Fill<System.UInt32>(m_brick_cache_usage_buffer_reset, 0);
            // ResetBrickCacheUsageBuffer();
            // m_brick_cache_misses_buffer = new ComputeBuffer(m_volume_dataset.BRICK_CACHE_MISSES_WINDOW * sizeof(System.UInt32) * 4, sizeof(System.UInt32));

            // m_attached_mesh_renderer.sharedMaterial.SetBuffer(SHADER_BRICK_CACHE_USAGE_BUFFER, m_brick_cache_usage_buffer);
            // m_attached_mesh_renderer.sharedMaterial.SetBuffer(SHADER_BRICK_CACHE_MISSES_BUFFER, m_brick_cache_misses_buffer);

            // scale mesh to match correct dimensions of the original volumetric data
            m_transform.localScale = new Vector3(
                m_volume_dataset.Metadata.VoxelDims.x * m_volume_dataset.Metadata.NbrChunksPerResolutionLvl[0].x * MM_TO_METERS,
                m_volume_dataset.Metadata.VoxelDims.y * m_volume_dataset.Metadata.NbrChunksPerResolutionLvl[0].y * MM_TO_METERS,
                m_volume_dataset.Metadata.VoxelDims.z * m_volume_dataset.Metadata.NbrChunksPerResolutionLvl[0].z * MM_TO_METERS
            );

            // rotate the volume according to provided Euler angles
            m_transform.localRotation = Quaternion.Euler(m_volume_dataset.Metadata.EulerRotation);

            m_progress_handler.Enable();
            if (m_rendering_mode == RenderingMode.IN_CORE) {
                Task t = Task.Run(() => {
                    switch (m_volume_dataset.Metadata.ColorDepth) {
                        case ColorDepth.UINT8:
                        Importer.LoadAllBricksIntoCache(m_volume_dataset.Metadata, m_brick_size, m_resolution_lvl,
                            m_cache_uint8, m_brick_reply_queue, m_progress_handler);
                        break;
                        case ColorDepth.UINT16:
                        Importer.LoadAllBricksIntoCache(m_volume_dataset.Metadata, m_brick_size, m_resolution_lvl,
                            m_cache_uint16, m_brick_reply_queue, m_progress_handler);
                        break;
                        default:
                        throw new NotImplementedException();
                    }
                });
                t.ContinueWith(t => { Debug.LogException(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
            } else {
                // TODO: add out-of-core rendering
            }
        }


        private void OnEnable() {
            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange += OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelMaxIterationsChange += OnModelMaxIterationsChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;

            // start handling GPU requests (i.e., visualization-driven bricks loading) or to avoid corroutines cost
            // and assuming only one camera exists in the scene, register a callback to Camera.onPostRender
            if (m_rendering_mode == RenderingMode.IN_CORE) {
                StartCoroutine(LoadAllBricks());
            } else {
                StartCoroutine(HandleGPURequests());
            }
        }

        private void OnDisable() {
            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange -= OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelMaxIterationsChange -= OnModelMaxIterationsChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;

            if (m_transfer_function != null) {
                m_transfer_function.TFColorsLookupTexChange -= OnTFColorsLookupTexChange;
            }

            StopAllCoroutines();
        }

        /// <summary>
        ///     In-core approach that loads all bricks into the 3D texture. Use this when the whole
        ///     3D dataset can fit into a GPU's texture 3D
        /// </summary>
        /// 
        /// <remark>
        ///     Note that the limitations for this may depend on the graphics API used. For instance,
        ///     for D3D11/12 you can't create a 3D texture larger than 2GBs
        /// </remark>
        /// 
        /// <exception cref="NotImplementedException">
        ///     thrown in case ColorDepth is neither UR8 nor UR16
        /// </exception>
        public IEnumerator LoadAllBricks() {

            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null) && (m_brick_cache != null));

            System.Diagnostics.Stopwatch stopwatch = System.Diagnostics.Stopwatch.StartNew();
            Debug.Log("started handling GPU brick requests");

            CVDSMetadata metadata = m_volume_dataset.Metadata;
            long nbr_bricks_uploaded = 0;
            long total_nbr_bricks = metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x *
                metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y *
                metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z *
                (int)Math.Pow(metadata.ChunkSize / m_brick_size, 3);
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[m_volume_dataset.MAX_BRICK_UPLOADS_PER_FRAME];
            Vector3 brick_scale = new(
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * metadata.ChunkSize),
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * metadata.ChunkSize),
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * metadata.ChunkSize)
            );

            while (nbr_bricks_uploaded < total_nbr_bricks) {

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
                    nbr_bricks_uploaded_per_frame < m_volume_dataset.MAX_BRICK_UPLOADS_PER_FRAME &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                ) {

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned to a
                    // fixed location in memory during the plugin call
                    switch (metadata.ColorDepth) {
                        case ColorDepth.UINT8: {
                            var brick = m_cache_uint8.Get(brick_id);
                            Assert.IsNotNull(brick);
                            handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                            break;
                        }
                        case ColorDepth.UINT16: {
                            var brick = m_cache_uint16.Get(brick_id);
                            Assert.IsNotNull(brick);
                            handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                            break;
                        }
                        default:
                        throw new NotImplementedException();
                    }

                    // compute where the brick offset within the brick cache
                    m_volume_dataset.ComputeVolumeOffset(brick_id, m_brick_size, out Int32 x, out Int32 y, out Int32 z);

                    // allocate the plugin call's arguments struct
                    TextureSubImage3DParams args = new() {
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

                    if (InstantiateBrickWireframes) {
                        GameObject brick_wireframe = Instantiate(m_brick_wireframe, gameObject.transform, false);
                        brick_wireframe.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
                        brick_wireframe.transform.localPosition = new Vector3(
                            (x / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * metadata.ChunkSize) - 0.5f) + brick_scale.x / 2.0f,
                            (y / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * metadata.ChunkSize) - 0.5f) + brick_scale.y / 2.0f,
                            (z / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * metadata.ChunkSize) - 0.5f) + brick_scale.z / 2.0f
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
        }

        private int GetGPUBrickRequests(ref UInt32[] brick_requests) {
            return 0;
        }

        private void ImportBricks(UInt32[] brick_ids, int len) {
            Task t = Task.Run(() => {
                switch (m_volume_dataset.Metadata.ColorDepth) {
                    case ColorDepth.UINT8:
                    for (int i = 0; i < len; ++i) {
                        Importer.ImportBrick(m_volume_dataset.Metadata, brick_ids[i], m_brick_size, m_cache_uint8);
                    }
                    break;
                    case ColorDepth.UINT16:
                    for (int i = 0; i < len; ++i) {
                        Importer.ImportBrick(m_volume_dataset.Metadata, brick_ids[i], m_brick_size, m_cache_uint16);
                    }
                    break;
                    default:
                    throw new NotImplementedException();
                }
            });
            t.ContinueWith(t => { Debug.LogException(t.Exception); }, TaskContinuationOptions.OnlyOnFaulted);
        }

        public IEnumerator HandleGPURequests() {

            // make sure to only start when all dependencies are initialized
            yield return new WaitUntil(() => (m_volume_dataset != null) && (m_transfer_function != null) && (m_brick_cache != null));

            Debug.Log("started handling GPU brick requests");

            CVDSMetadata metadata = m_volume_dataset.Metadata;
            long nbr_bricks_uploaded = 0;
            int nbr_bricks_uploaded_per_frame = 0;
            CommandBuffer cmd_buffer = new();
            GCHandle[] handles = new GCHandle[m_volume_dataset.MAX_BRICK_UPLOADS_PER_FRAME];
            Vector3 brick_scale = new(
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * metadata.ChunkSize),
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * metadata.ChunkSize),
                m_brick_size / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * metadata.ChunkSize)
            );

            UInt32[] brick_requests = new UInt32[MAX_NBR_BRICK_REQUESTS_PER_FRAME];

            while (true) {

                // wait until current frame rendering is done ...
                yield return new WaitForEndOfFrame();

                // we can safely assume that bricks uploaded to GPU in previous frame are done
                nbr_bricks_uploaded += nbr_bricks_uploaded_per_frame;

                // notify GC that it is free to manage previous frame's bricks
                for (int i = 0; i < nbr_bricks_uploaded_per_frame; ++i)
                    handles[i].Free();
                nbr_bricks_uploaded_per_frame = 0;
                m_tex_params_pool.ReleaseAll();

                // get bricks requested by GPU
                int nbr_requested_bricks = GetGPUBrickRequests(ref brick_requests);

                // import relevant chunks (again a chunk is a persistent memory unit while a brick is a GPU unit)
                ImportBricks(brick_requests, nbr_requested_bricks);

                // upload requested bricks to the GPU from the bricks reply queue
                while (
                    nbr_bricks_uploaded_per_frame < m_volume_dataset.MAX_BRICK_UPLOADS_PER_FRAME &&
                    m_brick_reply_queue.TryDequeue(out UInt32 brick_id)
                ) {

                    // we are sending a managed object to unmanaged thread (i.e., C++) the object has to be pinned to a
                    // fixed location in memory during the plugin call
                    switch (metadata.ColorDepth) {
                        case ColorDepth.UINT8: {
                            var brick = m_cache_uint8.Get(brick_id);
                            Assert.IsNotNull(brick);
                            handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                            break;
                        }
                        case ColorDepth.UINT16: {
                            var brick = m_cache_uint16.Get(brick_id);
                            Assert.IsNotNull(brick);
                            handles[nbr_bricks_uploaded_per_frame] = GCHandle.Alloc(brick.data, GCHandleType.Pinned);
                            break;
                        }
                        default:
                        throw new NotImplementedException();
                    }

                    // compute where the brick offset within the brick cache
                    m_volume_dataset.ComputeVolumeOffset(brick_id, m_brick_size, out Int32 x, out Int32 y, out Int32 z);

                    // allocate the plugin call's arguments struct
                    TextureSubImage3DParams args = new() {
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

                    if (InstantiateBrickWireframes) {
                        GameObject brick_wireframe = Instantiate(m_brick_wireframe, gameObject.transform, false);
                        brick_wireframe.GetComponent<MeshFilter>().sharedMesh = m_wireframe_cube_mesh;
                        brick_wireframe.transform.localPosition = new Vector3(
                            (x / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].x * metadata.ChunkSize) - 0.5f) + brick_scale.x / 2.0f,
                            (y / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].y * metadata.ChunkSize) - 0.5f) + brick_scale.y / 2.0f,
                            (z / (float)(metadata.NbrChunksPerResolutionLvl[m_resolution_lvl].z * metadata.ChunkSize) - 0.5f) + brick_scale.z / 2.0f
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

        }  // END COROUTINE

        /*
        private void ResetBrickCacheUsageBuffer() {
            m_brick_cache_usage_buffer.SetData(m_brick_cache_usage_buffer_reset);
        }
        */


        void OnTFColorsLookupTexChange(Texture2D new_colors_lookup_tex) {
            m_material.SetTexture(SHADER_TFTEX_ID, new_colors_lookup_tex);
        }

        private void OnModelAlphaCutoffChange(float value) {
            m_material.SetFloat(SHADER_ALPHA_CUTOFF_ID, value);
        }

        private void OnModelMaxIterationsChange(MaxIterations value) {
            if (m_max_iterations_update != null) {
                StopCoroutine(m_max_iterations_update);
                m_max_iterations_update = null;
            }

            var maxIters = value switch {
                MaxIterations._128 => 128,
                MaxIterations._256 => 256,
                MaxIterations._512 => 512,
                MaxIterations._1024 => 1024,
                MaxIterations._2048 => 2048,
                _ => throw new Exception(value.ToString()),
            };
            // do NOT update immediately as the brick cache texture could not be available
            m_max_iterations_update = StartCoroutine(UpdateWhenBrickCacheReady(() => {
                // Material.SetInteger() is busted ... do NOT use it
                m_material.SetFloat(SHADER_MAX_ITERATIONS_ID, maxIters);
                m_max_iterations_update = null;
            }));
        }

        private void OnModelTFChange(TF tf, ITransferFunction tf_so) {
            if (m_transfer_function != null)
                m_transfer_function.TFColorsLookupTexChange -= OnTransferFunctionTexChange;
            m_transfer_function = tf_so;
            m_transfer_function.TFColorsLookupTexChange += OnTFColorsLookupTexChange;
            m_transfer_function.ForceUpdateColorLookupTexture();
        }

        private void OnModelInterpolationChange(INTERPOLATION value) {
            if (m_interpolation_method_update != null) {
                StopCoroutine(m_interpolation_method_update);
                m_interpolation_method_update = null;
            }
            switch (value) {
                case INTERPOLATION.NEAREST_NEIGHBOR:
                m_interpolation_method_update = StartCoroutine(UpdateWhenBrickCacheReady(
                    () => {
                        m_brick_cache.filterMode = FilterMode.Point;
                        m_interpolation_method_update = null;
                    }
                    ));
                break;

                case INTERPOLATION.TRILLINEAR:
                m_interpolation_method_update = StartCoroutine(UpdateWhenBrickCacheReady(
                    () => {
                        m_brick_cache.filterMode = FilterMode.Bilinear;
                        m_interpolation_method_update = null;
                    }));
                break;

                default:
                throw new Exception(value.ToString());
            }
        }

        private void OnTransferFunctionTexChange(Texture2D newTex) {
            m_material.SetTexture(SHADER_TFTEX_ID, newTex);
        }

        private IEnumerator UpdateWhenBrickCacheReady(Action clbk) {
            yield return new WaitUntil(() => m_brick_cache != null);
            clbk();
        }
    }
}
