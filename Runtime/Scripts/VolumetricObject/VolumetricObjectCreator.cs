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
        [SerializeField] Vector3 m_VolumetricObjectPosition;
        private VolumetricObject m_VolumetricObject;

        void OnEnable()
        {
            InitializationEvents.OnMetadataImport += OnMetadataImport;
        }

        void OnDisable()
        {
            InitializationEvents.OnMetadataImport -= OnMetadataImport;
        }

        private void OnMetadataImport(Tuple<CVDSMetadata, PipelineParams, DebugginParams> args)
        {
            var volumetric_dataset = ScriptableObject.CreateInstance<VolumetricDataset>();

            CVDSMetadata metadata = args.Item1;
            PipelineParams pipelineParams = args.Item2;
            DebugginParams debugginParams = args.Item3;

            volumetric_dataset.Init(metadata);

            Debug.Log($"dataset loaded successfully from: {metadata.RootFilepath}");

            InitializationEvents.OnVolumetricDatasetCreation?.Invoke(volumetric_dataset);

            // create the volumetric object that is backed by the previously created volumetric dataset
            m_VolumetricObject = Instantiate<GameObject>(
                    m_VolumetricObjectPrefab,
                    position: m_VolumetricObjectPosition,
                    rotation: Quaternion.identity
                )
                .GetComponent<VolumetricObject>();
            m_VolumetricObject.Init(volumetric_dataset, pipelineParams, debugginParams);
            m_VolumetricObject.enabled = true;

            // make the volumetric model dispatch its default visualization params state so that
            // listeners get the default visualization parameters
            volumetric_dataset.DispatchVisualizationParamsChangeEvents();

            InitializationEvents.OnHistogramImport?.Invoke(Importer.ImportHistogram(metadata));
        }
    }
}
