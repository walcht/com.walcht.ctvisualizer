using System;
using UnityEngine;

namespace UnityCTVisualizer {
    public struct VolumeInitializationParams {
        public int brickSize;
        public int highestResolutionLvl;
        public int nbrImporterThreads;
        public Vector3Int brick_cache_size;
        public RenderingMode rendering_mode;
        public int CPUBrickCacheSizeMB;
    }

    public static class VisualizationParametersEvents {
        ////////////////////////////////
        /// Invoked by Models (SOs)
        ////////////////////////////////
        public static Action<TF, ITransferFunction> ModelTFChange;
        public static Action<float> ModelAlphaCutoffChange;
        public static Action<INTERPOLATION> ModelInterpolationChange;
        public static Action<MaxIterations> ModelMaxIterationsChange;

        ////////////////////////////////
        /// Invoked by Views (UIs)
        ////////////////////////////////
        public static Action<TF> ViewTFChange;
        public static Action<float> ViewAlphaCutoffChange;
        public static Action<INTERPOLATION> ViewInterpolationChange;
        public static Action<MaxIterations> ViewMaxIterationsChange;
    }
}

