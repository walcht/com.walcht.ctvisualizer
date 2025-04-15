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
        [SerializeField] ImporterUI m_ImporterUI;
        [SerializeField] DatasetMetadataUI m_MetadataUI;
        [SerializeField] VisualizationParametersUI m_VisualizationParamsUI;
        [SerializeField] TransferFunction1DUI m_TransferFunction1DUI;
        [SerializeField] ProgressHandler m_ProgressHandlerUI;

        private void Awake()
        {
            m_TransferFunction1DUI.gameObject.SetActive(false);
            m_VisualizationParamsUI.gameObject.SetActive(false);
            m_ProgressHandlerUI.gameObject.SetActive(false);
        }

        private void OnEnable()
        {
            m_ImporterUI.gameObject.SetActive(true);
            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;

            InitializationEvents.OnMetadataImport += OnMetadataImport;
            InitializationEvents.OnVolumetricDatasetCreation += OnVolumetricDatasetCreation;
            InitializationEvents.OnHistogramImport += OnHistogramImport;

            // progress handler events
            ProgressHandlerEvents.OnRequestActivate += ProgressHandlerEvents_OnRequestActivate;
        }

        private void OnDisable()
        {
            m_ImporterUI.gameObject.SetActive(false);
            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;

            InitializationEvents.OnMetadataImport -= OnMetadataImport;
            InitializationEvents.OnVolumetricDatasetCreation -= OnVolumetricDatasetCreation;
            InitializationEvents.OnHistogramImport -= OnHistogramImport;

            // progress handler events
            ProgressHandlerEvents.OnRequestActivate -= ProgressHandlerEvents_OnRequestActivate;
        }

        private void OnModelTFChange(TF new_tf, ITransferFunction tf_so)
        {
            switch (new_tf)
            {
                case TF.TF1D:
                // disable other TFUIs here
                m_TransferFunction1DUI.Init((TransferFunction1D)tf_so);
                m_TransferFunction1DUI.gameObject.SetActive(true);
                break;
            }
        }

        void OnMetadataImport(Tuple<CVDSMetadata, VolumeInitializationParams> args)
        {
            m_MetadataUI.Init(args.Item1);
            m_MetadataUI.gameObject.SetActive(true);
        }

        void OnVolumetricDatasetCreation(VolumetricDataset _)
        {
            // initial state for these objects is "undefined" but they are listening for change
            m_VisualizationParamsUI.gameObject.SetActive(true);
        }

        void OnHistogramImport(List<UInt64> histogram)
        {
            m_TransferFunction1DUI.SetHistogram(histogram);
        }

        void ProgressHandlerEvents_OnRequestActivate(bool val) => m_ProgressHandlerUI.gameObject.SetActive(val);
    }
}
