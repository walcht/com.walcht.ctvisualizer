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
            m_TransferFunction1DUI.gameObject.SetActive(true);
            m_VisualizationParamsUI.gameObject.SetActive(true);
            m_ProgressHandlerUI.gameObject.SetActive(false);
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
