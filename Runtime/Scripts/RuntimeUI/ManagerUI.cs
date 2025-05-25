using UnityEngine;
using System;
using System.Collections.Generic;

namespace UnityCTVisualizer
{
    public enum TF
    {
        TF1D,
        // TF2D - currently not supported
    }

    public class ManagerUI : MonoBehaviour
    {
        public event Action<string> FilesystemExplorerEntry;

        [SerializeField] ImporterUI m_ImporterUI;
        [SerializeField] DatasetMetadataUI m_MetadataUI;
        [SerializeField] VisualizationParametersUI m_VisualizationParamsUI;
        [SerializeField] TransferFunction1DUI m_TransferFunction1DUI;
        [SerializeField] FilesystemExplorerUI m_FilesystemExplorerUI;
        [SerializeField] ProgressHandler m_ProgressHandlerUI;

        private void Awake()
        {
            m_ImporterUI.Init(this);
            m_TransferFunction1DUI.gameObject.SetActive(true);
            m_VisualizationParamsUI.gameObject.SetActive(true);
            m_ProgressHandlerUI.gameObject.SetActive(false);
            m_FilesystemExplorerUI.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_ImporterUI.gameObject.SetActive(true);

            InitializationEvents.OnMetadataImport += OnMetadataImport;
            InitializationEvents.OnHistogramImport += OnHistogramImport;

            // progress handler events
            ProgressHandlerEvents.OnRequestActivate += ProgressHandlerEvents_OnRequestActivate;
        }

        private void OnDisable()
        {
            m_ImporterUI.gameObject.SetActive(false);

            InitializationEvents.OnMetadataImport -= OnMetadataImport;
            InitializationEvents.OnHistogramImport -= OnHistogramImport;

            // progress handler events
            ProgressHandlerEvents.OnRequestActivate -= ProgressHandlerEvents_OnRequestActivate;
        }


        public void RequestFilesystemEntry(FilesystemExplorerMode entryType)
        {
            // TODO: disable interactivity of all other UIs
            m_FilesystemExplorerUI.gameObject.SetActive(true);
            m_FilesystemExplorerUI.UpdateMode(entryType);
            m_FilesystemExplorerUI.FilesystemEntrySelection += OnFilesystemExplorerEntrySelection;
            m_FilesystemExplorerUI.FilesystemExplorerExit += OnFilesystemExplorerExit;
        }


        private void OnFilesystemExplorerEntrySelection(string path = null)
        {
            // TODO: restore interactivity of all other UIs
            m_FilesystemExplorerUI.FilesystemEntrySelection -= OnFilesystemExplorerEntrySelection;
            m_FilesystemExplorerUI.FilesystemExplorerExit -= OnFilesystemExplorerExit;
            FilesystemExplorerEntry?.Invoke(path);
            m_FilesystemExplorerUI.gameObject.SetActive(false);
        }


        private void OnFilesystemExplorerExit()
        {
            // TODO: restore interactivity of all other UIs
            m_FilesystemExplorerUI.FilesystemEntrySelection -= OnFilesystemExplorerEntrySelection;
            m_FilesystemExplorerUI.FilesystemExplorerExit -= OnFilesystemExplorerExit;
            FilesystemExplorerEntry?.Invoke(null);
            m_FilesystemExplorerUI.gameObject.SetActive(false);
        }


        void OnMetadataImport(Tuple<CVDSMetadata, PipelineParams, DebugginParams> args)
        {
            m_MetadataUI.Init(args.Item1);
            m_MetadataUI.gameObject.SetActive(true);
        }

        void OnHistogramImport(UInt64[] histogram)
        {
            m_TransferFunction1DUI.SetHistogramData(histogram);
        }

        void ProgressHandlerEvents_OnRequestActivate(bool val) => m_ProgressHandlerUI.gameObject.SetActive(val);
    }
}
