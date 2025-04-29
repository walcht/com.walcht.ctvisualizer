using System;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    public class ImporterUI : MonoBehaviour
    {
        [SerializeField] Button m_ImportBtn;
        [SerializeField] TMP_InputField m_FilepathInputField;


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
        [SerializeField] Toggle m_VisualizeHomogeneousRegions;
        [SerializeField] Toggle m_RandomSeedToggle;
        [SerializeField] TMP_InputField m_RandomSeedInputField;


        [SerializeField] ProgressHandler m_ProgressHandler;


        CVDSMetadata m_CurrentMetadata;
        Color m_DefaultTextColor;

        void OnEnable()
        {
            // make sure that initially the progress handler is disabled
            m_ProgressHandler.gameObject.SetActive(false);

            // disable import button
            m_ImportBtn.interactable = false;

            // event listeners
            m_FilepathInputField.onSubmit.AddListener(OnFilepathSubmit);
            m_ImportBtn.onClick.AddListener(OnImportClick);
            m_RenderingMode.onValueChanged.AddListener(OnRenderingModeChange);
            m_MaxNbrBrickRequestsPerFrame.onValueChanged.AddListener(OnMaxNbrBrickRequestsPerFrameChange);
            m_OctreeMaxDepth.onValueChanged.AddListener(OnOctreeMaxDepthChange);
            m_RandomSeedToggle.onValueChanged.AddListener(OnRandomSeedToggle);
        }

        private void Start()
        {
            m_RandomSeedToggle.onValueChanged.Invoke(m_RandomSeedToggle.isOn);
        }

        void OnDisable()
        {
            m_FilepathInputField.onSubmit.RemoveAllListeners();
            m_ImportBtn.onClick.RemoveAllListeners();
            m_BrickSize.onValueChanged.RemoveAllListeners();
            m_RenderingMode.onValueChanged.RemoveAllListeners();
            m_MaxNbrBrickRequestsPerFrame.onValueChanged.RemoveAllListeners();
            m_OctreeMaxDepth.onValueChanged.RemoveAllListeners();
            m_RandomSeedToggle.onValueChanged.RemoveAllListeners();
        }

        void Awake()
        {
            m_DefaultTextColor = m_FilepathInputField.textComponent.color;

            // initialize debugging params
            m_Benchmark.isOn = false;
            m_BrickWireframe.isOn = false;
            m_VisualizeHomogeneousRegions.isOn = false;
            m_RandomSeedToggle.isOn = false;
            m_RandomSeedInputField.text = "0";
            m_RandomSeedInputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            m_Benchmark.interactable = true;
            m_BrickWireframe.interactable = true;
            m_VisualizeHomogeneousRegions.interactable = true;
            m_RandomSeedToggle.interactable = true;
        }

        /////////////////////////////////
        /// UI CALLBACKS (VIEW INVOKES)
        /////////////////////////////////
        void OnFilepathSubmit(string fp)
        {
            try
            {
                m_CurrentMetadata = Importer.ImportMetadata(fp);

                {
                    // fill the allowed number of importer threads options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= Math.Max(Environment.ProcessorCount - 2, 1); ++i)
                        opts.Add(new(i.ToString()));
                    m_NbrImporterThreads.options = opts;
                    m_NbrImporterThreads.value = opts.Count - 1;  // default: nbr logical threads - 2
                }

                {
                    // fill the allowed brick size options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 32; i <= m_CurrentMetadata.ChunkSize; i <<= 1)
                        opts.Add(new(i.ToString()));
                    m_BrickSize.options = opts;
                    m_BrickSize.value = opts.Count - 1;  // default: same as chunk size
                }

                {
                    // fill brick cache size options
                    List<TMP_Dropdown.OptionData> opts = new();
                    int granularity = 256;
                    for (int i = 1; (i * granularity) <= SystemInfo.systemMemorySize; ++i)
                    {
                        opts.Add(new((i * granularity).ToString()));
                    }
                    m_GPUBrickCacheSize.options = opts;
                }

                {
                    // fill rendering mode options
                    List<TMP_Dropdown.OptionData> opts = new();
                    foreach (string opt in Enum.GetNames(typeof(RenderingMode)))
                        opts.Add(new(opt));
                    m_RenderingMode.options = opts;
                }

                {
                    // fill per-frame max nbr brick uploads to the GPU
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 64; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_MaxNbrGPUBrickUploadsPerFrame.options = opts;
                }

                {
                    // fill CPU brick cache size options
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i * 512 < SystemInfo.systemMemorySize; ++i)
                        opts.Add(new((i * 512).ToString()));
                    m_CPUBrickCacheSize.options = opts;
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
                    // fill max number of brick requests per frame
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 256; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_MaxNbrBrickRequestsPerFrame.options = opts;
                    m_MaxNbrBrickRequestsPerFrame.value = 4;  // default: 16
                }

                {
                    // fill octree max depth values
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 0; i <= m_CurrentMetadata.OctreeMaxDepth; ++i)
                        opts.Add(new(i.ToString()));
                    m_OctreeMaxDepth.options = opts;
                    m_OctreeMaxDepth.value = opts.Count - 1;  // default: max octree depth from metadata
                }

                {
                    // fill brick requests random texture size values
                    List<TMP_Dropdown.OptionData> opts = new();
                    for (int i = 1; i <= 1024; i *= 2)
                        opts.Add(new(i.ToString()));
                    m_BrickRequestsRandomTexSize.options = opts;
                    m_BrickRequestsRandomTexSize.value = 7;  // default: 2^7 = 128
                }

                m_ImportBtn.interactable = true;
                m_NbrImporterThreads.interactable = true;
                m_BrickSize.interactable = true;
                m_RenderingMode.interactable = true;
                m_CPUBrickCacheSize.interactable = true;
                m_MaxNbrGPUBrickUploadsPerFrame.interactable = true;

                m_RenderingMode.onValueChanged.Invoke(m_RenderingMode.value);

                m_FilepathInputField.textComponent.color = m_DefaultTextColor;

                Debug.Log($"successfully imported CVDS metadta from: {fp}");
            }
            catch (Exception e)
            {
                m_CurrentMetadata = null;

                m_ImportBtn.interactable = false;
                m_NbrImporterThreads.interactable = false;
                m_BrickSize.interactable = false;
                m_RenderingMode.interactable = false;
                m_CPUBrickCacheSize.interactable = false;
                m_MaxNbrGPUBrickUploadsPerFrame.interactable = false;
                m_GPUBrickCacheSize.interactable = false;
                m_ResLvl.interactable = false;
                m_OctreeMaxDepth.interactable = false;
                m_OctreeStartDepth.interactable = false;
                m_BrickRequestsRandomTexSize.interactable = false;

                m_FilepathInputField.textComponent.color = Color.red;
                Debug.LogError($"failed to import CVDS metadta from: {fp}, reason: {e.Message}");
            }

        }


        void OnRenderingModeChange(int idx)
        {
            m_ImportBtn.interactable = true;
            m_NbrImporterThreads.interactable = true;
            m_BrickSize.interactable = true;
            m_RenderingMode.interactable = true;
            m_CPUBrickCacheSize.interactable = true;
            m_MaxNbrGPUBrickUploadsPerFrame.interactable = true;

            m_ResLvl.interactable = false;
            m_GPUBrickCacheSize.interactable = false;
            m_MaxNbrBrickRequestsPerFrame.interactable = false;
            m_MaxNbrBrickRequestsPerRay.interactable = false;
            m_OctreeMaxDepth.interactable = false;
            m_OctreeStartDepth.interactable = false;
            m_BrickRequestsRandomTexSize.interactable = false;

            switch (Enum.Parse<RenderingMode>(m_RenderingMode.options[idx].text))
            {
                case RenderingMode.IC:
                m_ResLvl.interactable = true;
                break;

                case RenderingMode.OOC_PT:
                m_GPUBrickCacheSize.interactable = true;
                m_MaxNbrBrickRequestsPerFrame.interactable = true;
                m_MaxNbrBrickRequestsPerRay.interactable = true;
                m_BrickRequestsRandomTexSize.interactable = true;
                m_MaxNbrBrickRequestsPerFrame.onValueChanged.Invoke(m_MaxNbrBrickRequestsPerFrame.value);
                break;

                case RenderingMode.OOC_HYBRID:
                m_GPUBrickCacheSize.interactable = true;
                m_MaxNbrBrickRequestsPerFrame.interactable = true;
                m_MaxNbrBrickRequestsPerRay.interactable = true;
                m_OctreeMaxDepth.interactable = true;
                m_OctreeStartDepth.interactable = true;
                m_BrickRequestsRandomTexSize.interactable = true;
                m_MaxNbrBrickRequestsPerFrame.onValueChanged.Invoke(m_MaxNbrBrickRequestsPerFrame.value);
                m_OctreeMaxDepth.onValueChanged.Invoke(m_OctreeMaxDepth.value);
                break;
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


        void OnImportClick() => InitializationEvents.OnMetadataImport?.Invoke(
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
                  VisualizeHomogeneousRegions = m_VisualizeHomogeneousRegions.isOn,
                  RandomSeed = Int32.Parse(m_RandomSeedInputField.text),
                  RandomSeedValid = m_RandomSeedToggle.isOn,
              }
            )
        );
    }
}
