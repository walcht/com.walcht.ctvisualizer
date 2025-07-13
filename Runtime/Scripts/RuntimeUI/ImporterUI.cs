using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    public class ImporterUI : MonoBehaviour
    {
        [SerializeField] TMP_InputField m_CVDSNameInputField;
        [SerializeField] Button m_ImportCVDSBtn;
        [SerializeField] Button m_VisualizeBtn;
        [SerializeField] TMP_Text m_VisualizeBtnText;


        /////////////////////////////////////
        /// PIPELINE PARAMS
        /////////////////////////////////////

        // common
        [SerializeField] TMP_Dropdown m_BrickSize;
        [SerializeField] TMP_Dropdown m_NbrImporterThreads;
        [SerializeField] TMP_Dropdown m_RenderingMode;
        [SerializeField] TMP_Dropdown m_CPUBrickCacheSize;
        [SerializeField] TMP_Dropdown m_MaxNbrGPUBrickUploadsPerFrame;

        // OOC only
        [SerializeField] TMP_Dropdown m_GPUBrickCacheSize;
        [SerializeField] TMP_Dropdown m_MaxNbrBrickRequestsPerFrame;
        [SerializeField] TMP_Dropdown m_MaxNbrBrickRequestsPerRay;
        [SerializeField] TMP_Dropdown m_BrickRequestsRandomTexSize;

        // OOC Hybrid only
        [SerializeField] TMP_Dropdown m_OctreeMaxDepth;
        [SerializeField] TMP_Dropdown m_OctreeStartDepth;

        // IC only
        [SerializeField] TMP_Dropdown m_ResLvl;

        /////////////////////////////////////
        /// DEBUGGING PARAMS
        /////////////////////////////////////

        [SerializeField] Toggle m_Benchmark;
        [SerializeField] Toggle m_BrickWireframe;
        [SerializeField] Toggle m_RandomSeedToggle;
        [SerializeField] TMP_InputField m_RandomSeedInputField;


        [SerializeField] ProgressHandler m_ProgressHandler;


        private CVDSMetadata m_CurrentMetadata;
        private RenderingMode m_CurrRenderingMode;
        Color m_DefaultTextColor;

        private ManagerUI m_ManagerUI;
        private bool m_Initialized = false;


        void OnEnable()
        {
            // make sure that initially the progress handler is disabled
            m_ProgressHandler.gameObject.SetActive(false);

            // disable import button
            m_VisualizeBtn.interactable = false;

            // event listeners
            m_ImportCVDSBtn.onClick.AddListener(OnImportCVDSClick);
            m_VisualizeBtn.onClick.AddListener(OnVisualizeClick);
            m_RenderingMode.onValueChanged.AddListener(OnRenderingModeChange);
            m_MaxNbrBrickRequestsPerFrame.onValueChanged.AddListener(OnMaxNbrBrickRequestsPerFrameChange);
            m_OctreeMaxDepth.onValueChanged.AddListener(OnOctreeMaxDepthChange);
            m_RandomSeedToggle.onValueChanged.AddListener(OnRandomSeedToggle);
            m_ResLvl.onValueChanged.AddListener(UpdateCPUBrickCacheOptions);
            m_CPUBrickCacheSize.onValueChanged.AddListener(UpdateCPUBrickCacheOptions);
        }


        private void Start()
        {
            if (m_ManagerUI == null)
            {
                throw new Exception("Init has to be called before enabling this Importer UI");
            }

            if (!m_Initialized)
            {
                m_CVDSNameInputField.readOnly = true;
                m_DefaultTextColor = m_CVDSNameInputField.textComponent.color;

                // initialize debugging params
                m_Benchmark.isOn = false;
                m_BrickWireframe.isOn = false;
                m_RandomSeedToggle.isOn = false;
                m_RandomSeedInputField.text = "0";
                m_RandomSeedInputField.contentType = TMP_InputField.ContentType.IntegerNumber;

                m_Benchmark.interactable = true;
                m_BrickWireframe.interactable = true;
                m_RandomSeedToggle.interactable = true;

                {
                    // fill rendering mode options
                    List<TMP_Dropdown.OptionData> opts = new();
                    foreach (string opt in Enum.GetNames(typeof(RenderingMode)))
                        opts.Add(new(opt));
                    m_RenderingMode.options = opts;
                    m_RenderingMode.value = 1;  // default: OOC PT
                }

                {
                    // fill the allowed number of importer threads options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= Math.Max(Environment.ProcessorCount - 2, 1); ++i)
                        opts.Add(new(i.ToString()));
                    m_NbrImporterThreads.options = opts;
                    m_NbrImporterThreads.value = opts.Count - 1;  // default: nbr logical threads - 2
                }

                {
                    // fill brick cache size options
                    List<TMP_Dropdown.OptionData> opts = new();
                    int granularity = 128;
                    for (int i = 1; (i * granularity) <= SystemInfo.graphicsMemorySize; ++i)
                    {
                        opts.Add(new((i * granularity).ToString()));
                    }
                    m_GPUBrickCacheSize.options = opts;
                    m_GPUBrickCacheSize.value = opts.Count / 2;  // default: 40% total GPU VRAM
                }

                {
                    // fill per-frame max nbr brick uploads to the GPU
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 64; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_MaxNbrGPUBrickUploadsPerFrame.options = opts;
                    m_MaxNbrGPUBrickUploadsPerFrame.value = opts.Count - 1;
                }

                {
                    // fill max number of brick requests per frame
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 256; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_MaxNbrBrickRequestsPerFrame.options = opts;
                    m_MaxNbrBrickRequestsPerFrame.value = 4;  // default: 16
                }

                {
                    // fill brick requests random texture size values
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 1024; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_BrickRequestsRandomTexSize.options = opts;
                    m_BrickRequestsRandomTexSize.value = 7;  // default: 2^7 = 128
                }

                {
                    // fill CPU brick cache size options
                    List < TMP_Dropdown.OptionData > opts = new();
                    int granularity = 512;
                    for (int i = 1; (i * granularity) < SystemInfo.systemMemorySize; ++i)
                        opts.Add(new((i * granularity).ToString()));
                    m_CPUBrickCacheSize.options = opts;
                    m_CPUBrickCacheSize.value = opts.Count / 4;  // default: 40% total RAM
                }

                m_Initialized = true;
            }
        }


        public void Init(ManagerUI managerUI)
        {
            m_ManagerUI = managerUI;
        }


        void OnDisable()
        {
            m_ImportCVDSBtn.onClick.RemoveAllListeners();
            m_VisualizeBtn.onClick.RemoveAllListeners();
            m_BrickSize.onValueChanged.RemoveAllListeners();
            m_RenderingMode.onValueChanged.RemoveAllListeners();
            m_MaxNbrBrickRequestsPerFrame.onValueChanged.RemoveAllListeners();
            m_OctreeMaxDepth.onValueChanged.RemoveAllListeners();
            m_RandomSeedToggle.onValueChanged.RemoveAllListeners();
            m_ResLvl.onValueChanged.RemoveAllListeners();
        }


        /////////////////////////////////
        /// UI CALLBACKS (VIEW INVOKES)
        /////////////////////////////////
        void OnImportCVDSClick()
        {
            m_ManagerUI.RequestFilesystemEntry(FilesystemExplorerMode.SEARCH_CVDS);
            m_ManagerUI.FilesystemExplorerEntry += OnFilesystemEntrySelection;
        }


        void OnFilesystemEntrySelection(string directoryPath)
        {
            m_ManagerUI.FilesystemExplorerEntry -= OnFilesystemEntrySelection;
            if (!Directory.Exists(directoryPath))
            {
                m_CVDSNameInputField.textComponent.color = Color.red;
                return;
            }
            try
            {
                CVDSMetadata _metadata = Importer.ImportMetadata(directoryPath);
                // TODO: provide better comparison operator for CVDS metadata objects
                if ((m_CurrentMetadata != null) && (_metadata.RootFilepath == m_CurrentMetadata.RootFilepath))
                {
                    return;
                }
                m_CurrentMetadata = _metadata;

                {
                    // fill the allowed brick size options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 32; i <= m_CurrentMetadata.ChunkSize; i <<= 1)
                        opts.Add(new(i.ToString()));
                    m_BrickSize.options = opts;
                    m_BrickSize.value = opts.Count - 1;  // default: same as chunk size
                }

                {
                    // fill the allowed highest resolution level options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 0; i < m_CurrentMetadata.NbrResolutionLvls; ++i)
                        opts.Add(new(i.ToString()));
                    m_ResLvl.options = opts;
                    m_ResLvl.value = 0;  // default: 0
                }

                {
                    // fill octree max depth values
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 0; i <= m_CurrentMetadata.OctreeMaxDepth; ++i)
                        opts.Add(new(i.ToString()));
                    m_OctreeMaxDepth.options = opts;
                    m_OctreeMaxDepth.value = opts.Count - 1;  // default: max octree depth from metadata
                }

                OnRenderingModeChange(m_RenderingMode.value);

                m_CVDSNameInputField.textComponent.color = m_DefaultTextColor;
                m_CVDSNameInputField.text = new DirectoryInfo(directoryPath).Name;

                m_VisualizeBtn.interactable = true;
                m_VisualizeBtnText.text = "VISUALIZE";

                InitializationEvents.OnMetadataImport?.Invoke(m_CurrentMetadata);

                Debug.Log($"successfully imported SEARCH_CVDS metadta from: {directoryPath}");
            }
            catch (Exception e)
            {
                m_CurrentMetadata = null;
                m_VisualizeBtn.interactable = false;
                m_CVDSNameInputField.textComponent.color = Color.red;

                Debug.LogError($"failed to import SEARCH_CVDS metadata from: {directoryPath}, reason: {e.Message}");
            }
        }


        void OnRenderingModeChange(int idx)
        {
            // update current rendering mode
            m_CurrRenderingMode = Enum.Parse<RenderingMode>(m_RenderingMode.options[idx].text);

            EnableParentGameObject(m_RenderingMode);
            EnableParentGameObject(m_NbrImporterThreads);
            EnableParentGameObject(m_CPUBrickCacheSize);
            EnableParentGameObject(m_MaxNbrGPUBrickUploadsPerFrame);

            DisableParentGameObject(m_ResLvl);
            DisableParentGameObject(m_GPUBrickCacheSize);
            DisableParentGameObject(m_MaxNbrBrickRequestsPerFrame);
            DisableParentGameObject(m_MaxNbrBrickRequestsPerRay);
            DisableParentGameObject(m_OctreeMaxDepth);
            DisableParentGameObject(m_OctreeStartDepth);
            DisableParentGameObject(m_BrickRequestsRandomTexSize);

            switch (m_CurrRenderingMode)
            {
                case RenderingMode.IC:
                {
                    if (m_CurrentMetadata != null)
                    {
                        EnableParentGameObject(m_BrickSize);
                        EnableParentGameObject(m_ResLvl);
                        UpdateCPUBrickCacheOptions();
                    }
                    break;
                }

                case RenderingMode.OOC_PT:
                {
                    if (m_CurrentMetadata != null)
                    {
                        EnableParentGameObject(m_BrickSize);
                    }
                    EnableParentGameObject(m_GPUBrickCacheSize);
                    EnableParentGameObject(m_MaxNbrBrickRequestsPerFrame);
                    EnableParentGameObject(m_MaxNbrBrickRequestsPerRay);
                    EnableParentGameObject(m_BrickRequestsRandomTexSize);
                    break;
                }

                case RenderingMode.OOC_HYBRID:
                {
                    EnableParentGameObject(m_GPUBrickCacheSize);
                    EnableParentGameObject(m_MaxNbrBrickRequestsPerFrame);
                    EnableParentGameObject(m_MaxNbrBrickRequestsPerRay);
                    EnableParentGameObject(m_BrickRequestsRandomTexSize);
                    if (m_CurrentMetadata != null)
                    {
                        EnableParentGameObject(m_OctreeStartDepth);
                        EnableParentGameObject(m_OctreeMaxDepth);
                    }
                    break;
                }
            }
        }


        void OnOctreeMaxDepthChange(int idx)
        {
            // fill octree start depth options
            List<TMP_Dropdown.OptionData> opts = new();
            for (int i = 1; i <= Int32.Parse(m_OctreeMaxDepth.options[idx].text); ++i)
                opts.Add(new(i.ToString()));
            m_OctreeStartDepth.options = opts;
        }


        void OnMaxNbrBrickRequestsPerFrameChange(int idx)
        {
            // fill max number of brick requests per ray
            List<TMP_Dropdown.OptionData> maxNbrBrickRequestsPerRayOpts = new();
            for (int i = 1; i <= Int32.Parse(m_MaxNbrBrickRequestsPerFrame.options[idx].text); i *= 2)
                maxNbrBrickRequestsPerRayOpts.Add(new(i.ToString()));
            m_MaxNbrBrickRequestsPerRay.options = maxNbrBrickRequestsPerRayOpts;
        }


        void OnRandomSeedToggle(bool toggled)
        {
            m_RandomSeedInputField.interactable = toggled;
        }


        void OnVisualizeClick()
        {
            InitializationEvents.OnVisualize?.Invoke(
            new Tuple<CVDSMetadata, PipelineParams, DebugginParams>(
              m_CurrentMetadata,
              new PipelineParams()
              {
                  BrickSize = Int32.Parse(m_BrickSize.options[m_BrickSize.value].text),
                  MaxNbrImporterThreads = Int32.Parse(m_NbrImporterThreads.options[m_NbrImporterThreads.value].text),
                  RenderingMode = Enum.Parse<RenderingMode>(m_RenderingMode.options[m_RenderingMode.value].text),
                  GPUBrickCacheSizeMBs = Int32.Parse(m_GPUBrickCacheSize.options[m_GPUBrickCacheSize.value].text),
                  CPUBrickCacheSizeMBs = Int32.Parse(m_CPUBrickCacheSize.options[m_CPUBrickCacheSize.value].text),
                  InCoreMaxResolutionLvl = Int32.Parse(m_ResLvl.options[m_ResLvl.value].text),
                  MaxNbrBrickRequestsPerFrame = Int32.Parse(m_MaxNbrBrickRequestsPerFrame.options[m_MaxNbrBrickRequestsPerFrame.value].text),
                  MaxNbrBrickRequestsPerRay = Int32.Parse(m_MaxNbrBrickRequestsPerRay.options[m_MaxNbrBrickRequestsPerRay.value].text),
                  OctreeStartDepth = Int32.Parse(m_OctreeStartDepth.options[m_OctreeStartDepth.value].text),
                  OctreeMaxDepth = Int32.Parse(m_OctreeMaxDepth.options[m_OctreeMaxDepth.value].text),
                  BrickRequestsRandomTexSize = Int32.Parse(m_BrickRequestsRandomTexSize.options[m_BrickRequestsRandomTexSize.value].text),
                  MaxNbrGPUBrickUploadsPerFrame = Int32.Parse(m_MaxNbrGPUBrickUploadsPerFrame.options[m_MaxNbrGPUBrickUploadsPerFrame.value].text),
              },
              new DebugginParams()
              {
                  Benchmark = m_Benchmark.isOn,
                  BrickWireframes = m_BrickWireframe.isOn,
                  RandomSeed = Int32.Parse(m_RandomSeedInputField.text),
                  RandomSeedValid = m_RandomSeedToggle.isOn,
              }
            )
            );

            m_VisualizeBtnText.text = "RE-VISUALIZE";
        }

        private void UpdateCPUBrickCacheOptions(int _ = -1)
        {
            if (m_CurrentMetadata == null || (m_CurrRenderingMode != RenderingMode.IC))
            {
                return;
            }

            // make sure CPU brick cache can hold the entire dataset
            int res_lvl = int.Parse(m_ResLvl.options[m_ResLvl.value].text);
            int minBrickCacheSizeMBs = Mathf.CeilToInt(m_CurrentMetadata.NbrChunksPerResolutionLvl[res_lvl].x
                    * m_CurrentMetadata.NbrChunksPerResolutionLvl[res_lvl].y
                    * m_CurrentMetadata.NbrChunksPerResolutionLvl[res_lvl].z
                    * (m_CurrentMetadata.ChunkSize * m_CurrentMetadata.ChunkSize * m_CurrentMetadata.ChunkSize / (1024.0f * 1024.0f)));

            Debug.Log($"min CPU brick cache size for IC rendering: {minBrickCacheSizeMBs}MBs");

            // do nothing in case selected brick cache size is already sufficient
            if (int.Parse(m_CPUBrickCacheSize.options[m_CPUBrickCacheSize.value].text) >= minBrickCacheSizeMBs)
            {
                return;
            }

            bool found = false;
            for (int i = 0; i < m_CPUBrickCacheSize.options.Count; ++i)
            {
                if (int.Parse(m_CPUBrickCacheSize.options[i].text) >= minBrickCacheSizeMBs)
                {
                    m_CPUBrickCacheSize.SetValueWithoutNotify(i);
                    found = true;
                    break;
                }
            }
            if (!found)
            {
                throw new Exception("Dataset is too large for in-core rendering mode (insufficient RAM to hold the dataset in its entirety).");
            }
        }

        private void DisableParentGameObject(TMP_Dropdown dropdown) => dropdown.transform.parent.gameObject.SetActive(false);


        private void EnableParentGameObject(TMP_Dropdown dropdown) => dropdown.transform.parent.gameObject.SetActive(true);
    }
}
