using System;

namespace UnityCTVisualizer
{
    public struct PipelineParams
    {
        public RenderingMode RenderingMode;
        public int BrickSize;
        public int MaxNbrImporterThreads;
        public int MaxNbrGPUBrickUploadsPerFrame;
        public int GPUBrickCacheSizeMBs;
        public int CPUBrickCacheSizeMBs;
        public int InCoreMaxResolutionLvl;
        public int MaxNbrBrickRequestsPerFrame;
        public int MaxNbrBrickRequestsPerRay;
        public int OctreeMaxDepth;
        public int OctreeStartDepth;
        public int BrickRequestsRandomTexSize;
    }

    public struct DebugginParams
    {
        public bool Benchmark;
        public bool BrickWireframes;
        public bool VisualizeHomogeneousRegions;
        public int RandomSeed;
        public bool RandomSeedValid;
    }

    public static class VisualizationParametersEvents
    {
        ////////////////////////////////
        /// Invoked by Models (SOs)
        ////////////////////////////////
        public static Action<TF, ITransferFunction> ModelTFChange;
        public static Action<float> ModelOpacityCutoffChange;
        public static Action<INTERPOLATION> ModelInterpolationChange;
        public static Action<float> ModelSamplingQualityFactorChange;
        public static Action<float> ModelLODQualityFactorChange;

        ////////////////////////////////
        /// Invoked by Views (UIs)
        ////////////////////////////////
        public static Action<TF> ViewTFChange;
        public static Action<float> ViewAlphaCutoffChange;
        public static Action<INTERPOLATION> ViewInterpolationChange;
        public static Action<float> ViewSamplingQualityFactorChange;
        public static Action<float> ViewLODQualityFactorChange;
    }
}

