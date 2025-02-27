using System;

namespace UnityCTVisualizer {
    public struct VolumeInitializationParams {
      public int brickSize;
      public int highestResolutionLvl;
      public int nbrImporterThreads;
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

