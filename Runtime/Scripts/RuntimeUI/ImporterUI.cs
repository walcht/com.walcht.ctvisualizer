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
        [SerializeField] TMP_Dropdown m_NbrImporterThreadsDropDown;
        [SerializeField] TMP_Dropdown m_BrickSizeDropDown;
        [SerializeField] TMP_Dropdown m_HighestResLvlDropDown;
        [SerializeField] TMP_Dropdown m_RenderingModeDropDown;
        [SerializeField] TMP_Dropdown m_BrickCacheSizeXDropDown;
        [SerializeField] TMP_Dropdown m_BrickCacheSizeYDropDown;
        [SerializeField] TMP_Dropdown m_BrickCacheSizeZDropDown;
        [SerializeField] TMP_Dropdown m_CPUBrickCacheSizeDropDown;
        [SerializeField] Toggle m_BenchmarkToggle;

        [SerializeField] ProgressHandler m_ProgressHandler;

        CVDSMetadata m_CurrentMetadata;
        Color m_DefaultTextColor;

        void OnEnable()
        {
            // make sure that initially the progress handler is disabled
            m_ProgressHandler.gameObject.SetActive(false);
            // disable import button
            m_ImportBtn.interactable = false;
            m_FilepathInputField.onSubmit.AddListener(OnFilepathSubmit);
            m_ImportBtn.onClick.AddListener(OnImportClick);
            m_BrickSizeDropDown.onValueChanged.AddListener(OnBrickSizeChange);
        }

        void OnDisable()
        {
            m_FilepathInputField.onSubmit.RemoveListener(OnFilepathSubmit);
            m_ImportBtn.onClick.RemoveListener(OnImportClick);
            m_BrickSizeDropDown.onValueChanged.RemoveAllListeners();
        }

        void Awake()
        {
            m_DefaultTextColor = m_FilepathInputField.textComponent.color;
        }

        /////////////////////////////////
        /// UI CALLBACKS (VIEW INVOKES)
        /////////////////////////////////
        void OnFilepathSubmit(string fp)
        {
            try
            {
                m_CurrentMetadata = Importer.ImportMetadata(fp);

                // fill the allowed number of importer threads options
                List<TMP_Dropdown.OptionData> nbrImporterThreadsOptions = new();
                for (int i = 1; i <= Math.Max(Environment.ProcessorCount - 2, 1); ++i)
                    nbrImporterThreadsOptions.Add(new(i.ToString()));
                m_NbrImporterThreadsDropDown.options = nbrImporterThreadsOptions;

                // fill the allowed brick size options
                List<TMP_Dropdown.OptionData> brickSizeOptions = new();
                for (int i = 32; i <= m_CurrentMetadata.ChunkSize; i <<= 1)
                    brickSizeOptions.Add(new(i.ToString()));
                m_BrickSizeDropDown.options = brickSizeOptions;
                m_BrickSizeDropDown.value = 0;

                // fill the allowed highest resolution level options
                List<TMP_Dropdown.OptionData> highestResLvlOptions = new();
                for (int i = 0; i < m_CurrentMetadata.NbrResolutionLvls; ++i)
                    highestResLvlOptions.Add(new(i.ToString()));
                m_HighestResLvlDropDown.options = highestResLvlOptions;

                // fill brick cache size options
                OnBrickSizeChange(0);

                // fill rendering mode options
                List<TMP_Dropdown.OptionData> renderingModeOptions = new();
                foreach (string opt in Enum.GetNames(typeof(RenderingMode)))
                    renderingModeOptions.Add(new(opt));
                m_RenderingModeDropDown.options = renderingModeOptions;

                // fill CPU brick cache size options
                List<TMP_Dropdown.OptionData> cpuBrickCacheSizeOptions = new();
                for (int i = 1; i * 512 < SystemInfo.systemMemorySize; ++i)
                    cpuBrickCacheSizeOptions.Add(new((i * 512).ToString()));
                m_CPUBrickCacheSizeDropDown.options = cpuBrickCacheSizeOptions;

                // by default benchmarking is on
                m_BenchmarkToggle.isOn = true;

                m_ImportBtn.interactable = true;
                m_NbrImporterThreadsDropDown.interactable = true;
                m_BrickSizeDropDown.interactable = true;
                m_HighestResLvlDropDown.interactable = true;
                m_RenderingModeDropDown.interactable = true;
                m_BrickCacheSizeXDropDown.interactable = true;
                m_BrickCacheSizeYDropDown.interactable = true;
                m_BrickCacheSizeZDropDown.interactable = true;
                m_CPUBrickCacheSizeDropDown.interactable = true;
                m_BenchmarkToggle.interactable = true;

                m_FilepathInputField.textComponent.color = m_DefaultTextColor;

                Debug.Log($"successfully imported CVDS metadta from: {fp}");
            }
            catch (Exception)
            {
                m_CurrentMetadata = null;

                m_ImportBtn.interactable = false;
                m_NbrImporterThreadsDropDown.interactable = false;
                m_BrickSizeDropDown.interactable = false;
                m_HighestResLvlDropDown.interactable = false;
                m_RenderingModeDropDown.interactable = false;
                m_BrickCacheSizeXDropDown.interactable = false;
                m_BrickCacheSizeYDropDown.interactable = false;
                m_BrickCacheSizeZDropDown.interactable = false;
                m_CPUBrickCacheSizeDropDown.interactable = false;
                m_BenchmarkToggle.interactable = false;

                m_FilepathInputField.textComponent.color = Color.red;
                Debug.LogError($"failed to import CVDS metadta from: {fp}");
            }

        }

        void OnBrickSizeChange(int _)
        {
            List<TMP_Dropdown.OptionData> opts = new();
            int brick_size = Int32.Parse(m_BrickSizeDropDown.options[m_BrickSizeDropDown.value].text);
            for (int i = 0; (i * brick_size) <= SystemInfo.maxTexture3DSize; ++i)
            {
                opts.Add(new((i * brick_size).ToString()));
            }
            m_BrickCacheSizeXDropDown.options = opts;
            m_BrickCacheSizeYDropDown.options = opts;
            m_BrickCacheSizeZDropDown.options = opts;
        }

        void OnImportClick() => InitializationEvents.OnMetadataImport?.Invoke(
            new Tuple<CVDSMetadata, VolumeInitializationParams>(
              m_CurrentMetadata,
              new VolumeInitializationParams()
              {
                  brickSize = Int32.Parse(m_BrickSizeDropDown.options[m_BrickSizeDropDown.value].text),
                  highestResolutionLvl = Int32.Parse(m_HighestResLvlDropDown.options[m_HighestResLvlDropDown.value].text),
                  nbrImporterThreads = Int32.Parse(m_NbrImporterThreadsDropDown.options[m_NbrImporterThreadsDropDown.value].text),
                  rendering_mode = Enum.Parse<RenderingMode>(m_RenderingModeDropDown.options[m_RenderingModeDropDown.value].text),
                  brick_cache_size = new Vector3Int(
                      Int32.Parse(m_BrickCacheSizeXDropDown.options[m_BrickCacheSizeXDropDown.value].text),
                      Int32.Parse(m_BrickCacheSizeYDropDown.options[m_BrickCacheSizeYDropDown.value].text),
                      Int32.Parse(m_BrickCacheSizeZDropDown.options[m_BrickCacheSizeZDropDown.value].text)
                  ),
                  CPUBrickCacheSizeMB = Int32.Parse(m_CPUBrickCacheSizeDropDown.options[m_CPUBrickCacheSizeDropDown.value].text),
                  benchmark = m_BenchmarkToggle.isOn,
              })
            );
    }
}
