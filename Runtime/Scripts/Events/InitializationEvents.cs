using System;

namespace UnityCTVisualizer {
    public static class InitializationEvents {
        /// <summary>
        ///     Invoked when a CVDS metadata is successfully imported.
        /// </summary>
        public static Action<CVDSMetadata> OnMetadataImport;

        /// <summary>
        ///     Invoked when the user requests the volumetric object to be visualized (after potentially setting the
        ///     offline pipeline parameters, debugging parameters, and runtime visualization parameters). The CVDSMetadata
        ///     instance along with additional runtime-constant pipeline parameters and debugging parametrs that are used
        ///     to create the volumetric object are passed to the handler(s).
        /// </summary>
        public static Action<Tuple<CVDSMetadata, PipelineParams, DebugginParams>> OnVisualize;

        /// <summary>
        ///     Invoked when a volumetric dataset is successfully created. The VolumetricDataset ScriptableObject
        ///     instance is passed to the handler(s).
        /// </summary>
        public static Action<VolumetricDataset> OnVolumetricDatasetCreation;

        public static Action<VolumetricObject> OnVolumetricObjectCreation;

        public static Action<UInt64[]> OnHistogramImport;
    }
}

