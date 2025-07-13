using System;
using System.Collections.Generic;

namespace UnityCTVisualizer
{
    [Serializable]
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
        public static Action<List<float>> ModelLODDistancesChange;
        public static Action<byte> ModelHomogeneityToleranceChange;
        public static Action<float> ModelVolumetricObjectScaleFactorChange;

        ////////////////////////////////
        /// Invoked by Views (UIs)
        ////////////////////////////////
        public static Action<TF> ViewTFChange;
        public static Action<float> ViewAlphaCutoffChange;
        public static Action<INTERPOLATION> ViewInterpolationChange;
        public static Action<float> ViewSamplingQualityFactorChange;
        public static Action<List<float>> ViewLODDistancesChange;
        public static Action<byte> ViewHomogeneityToleranceChange;
        public static Action<float> ViewVolumetricObjectScaleFactorChange;
    }
}

