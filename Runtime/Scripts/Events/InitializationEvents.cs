using System;

namespace UnityCTVisualizer {
    public static class InitializationEvents {
        /// <summary>
        ///     Invoked when a SEARCH_CVDS metadata is successfully imported. The CVDSMetadata instance along with additional
        ///     runtime-constant pipeline parameters and debugging parametrs that are used to create the volumetric object
        ///     are passed to the handler(s).
        /// </summary>
        public static Action<Tuple<CVDSMetadata, PipelineParams, DebugginParams>> OnMetadataImport;

        /// <summary>
        ///     Invoked when a volumetric dataset is successfully created. The VolumetricDataset ScriptableObject
        ///     instance is passed to the handler(s).
        /// </summary>
        public static Action<VolumetricDataset> OnVolumetricDatasetCreation;

        public static Action<UInt64[]> OnHistogramImport;
    }
}

