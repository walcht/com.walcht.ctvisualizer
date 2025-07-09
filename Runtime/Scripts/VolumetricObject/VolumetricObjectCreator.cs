using UnityEngine;
using System;

namespace UnityCTVisualizer
{
    /// <summary>
    ///     A volumetric object creator. Keep a SINGLE INSTANCE of this per scene. Not having an instance will result in
    ///     nothing being visualized even after a volumetric dataset is loaded.
    /// </summary>
    public class VolumetricObjectCreator : MonoBehaviour
    {

        [SerializeField] GameObject m_VolumetricObjectPrefab;
        public Vector3 m_InitialVolumetricObjectPosition;
        private VolumetricObject m_VolumetricObject = null;
        private VolumetricDataset m_VolumetricDataset;

        private void Awake()
        {
            m_VolumetricDataset = ScriptableObject.CreateInstance<VolumetricDataset>();
        }

        private void Start()
        {
            m_VolumetricDataset.DispatchVisualizationParamsChangeEvents();
        }

        void OnEnable()
        {
            InitializationEvents.OnMetadataImport += OnMetadataImport;
            InitializationEvents.OnVisualize += OnVisualize;
        }

        void OnDisable()
        {
            InitializationEvents.OnMetadataImport -= OnMetadataImport;
            InitializationEvents.OnVisualize -= OnVisualize;
        }


        private void OnMetadataImport(CVDSMetadata metadata)
        {
            m_VolumetricDataset.Init(metadata);
            Debug.Log($"dataset loaded successfully from: {metadata.RootFilepath}");
            InitializationEvents.OnVolumetricDatasetCreation?.Invoke(m_VolumetricDataset);
            InitializationEvents.OnHistogramImport?.Invoke(Importer.ImportHistogram(metadata));

            // make the volumetric model dispatch its default visualization params state so that
            // listeners get the default visualization parameters
            m_VolumetricDataset.DispatchVisualizationParamsChangeEvents();
        }


        private void OnVisualize(Tuple<CVDSMetadata, PipelineParams, DebugginParams> args)
        {
            if (m_VolumetricObject != null)
            {
                // will cause a 1s freeze
                DestroyImmediate(m_VolumetricObject);
            }

            PipelineParams pipelineParams = args.Item2;
            DebugginParams debugginParams = args.Item3;

            // create the volumetric object that is backed by the previously created volumetric dataset
            m_VolumetricObject = Instantiate<GameObject>(
                    m_VolumetricObjectPrefab,
                    position: m_InitialVolumetricObjectPosition,
                    rotation: Quaternion.identity
                )
                .GetComponent<VolumetricObject>();
            m_VolumetricObject.Init(m_VolumetricDataset, pipelineParams, debugginParams);
            m_VolumetricObject.enabled = true;

            // make the volumetric model dispatch its default visualization params state so that
            // listeners get the default visualization parameters
            m_VolumetricDataset.DispatchVisualizationParamsChangeEvents();

            InitializationEvents.OnVolumetricObjectCreation?.Invoke(m_VolumetricObject);
        }
    }
}
