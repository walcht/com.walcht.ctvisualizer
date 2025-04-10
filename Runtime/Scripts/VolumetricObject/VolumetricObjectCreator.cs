using UnityEngine;
using System;

namespace UnityCTVisualizer {
    /// <summary>
    ///     A volumetric object creator. Keep a SINGLE INSTANCE of this per scene. Not having an instance will result in
    ///     nothing being visualized even after a volumetric dataset is loaded.
    /// </summary>
    public class VolumetricObjectCreator : MonoBehaviour {

        [SerializeField] GameObject m_VolumetricObjectPrefab;
        [SerializeField] Vector3 m_VolumetricObjectPosition;
        private VolumetricObject m_VolumetricObject;

        void OnEnable() {
            InitializationEvents.OnMetadataImport += OnMetadataImport;
        }

        void OnDisable() {
            InitializationEvents.OnMetadataImport -= OnMetadataImport;
        }

        private void OnMetadataImport(Tuple<CVDSMetadata, VolumeInitializationParams> args) {

            var volumetric_dataset = ScriptableObject.CreateInstance<VolumetricDataset>();

            volumetric_dataset.Init(args.Item1);

            Debug.Log($"dataset loaded successfully from: {args.Item1.RootFilepath}");
 
            InitializationEvents.OnVolumetricDatasetCreation?.Invoke(volumetric_dataset);

            // create the volumetric object that is backed by the previously created volumetric dataset
            m_VolumetricObject = Instantiate<GameObject>(
                    m_VolumetricObjectPrefab,
                    position: m_VolumetricObjectPosition,
                    rotation: Quaternion.identity
                )
                .GetComponent<VolumetricObject>();

            m_VolumetricObject.Init(volumetric_dataset, args.Item2.rendering_mode,
                args.Item2.brick_cache_size, resolution_lvl: args.Item2.highestResolutionLvl,
                nbr_brick_importer_threads: args.Item2.nbrImporterThreads,
                cpu_memory_cache_mb: args.Item2.CPUBrickCacheSizeMB);

            m_VolumetricObject.enabled = true;

            // make the volumetric model dispatch its default visualization params state so that
            // listeners get the default visualization parameters
            volumetric_dataset.DispatchVisualizationParamsChangeEvents();

        }
    }
}
