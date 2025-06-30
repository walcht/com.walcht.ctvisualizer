using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCTVisualizer
{
    public enum BenchmarkingEventType
    {
        NEW_OPCAITY_CUTOFF_VALUE = 0,
        NEW_SAMPLING_QUALITY_FACTOR_VALUE,
        NEW_INTERPOLATION_METHOD,
        NEW_RANDOM_ROTATION_AXIS,
        NEW_RANDOM_LOOK_AT_POINT,
        NEW_HOMOGENEITY_TOLERANCE_VALUE,
        NEW_LOD_DISTANCES,
        BRICK_CACHE_WARMUP_START,
        BRICK_CACHE_WARMUP_END,
        FRAMETIMES_MEASUREMENTS_START,
        FRAMETIMES_MEASUREMENTS_END,
        FCT_MEASUREMENTS_START,
        FCT_MEASUREMENTS_END,
    }


    public struct BenchmarkingEvent
    {
        public long Timestamp;
        public BenchmarkingEventType Type;
        public float Value;
    }


    public class BenchmarkingStats
    {
        public CVDSMetadataInternal DatasetMetadata;
        public PipelineParams PipelineParameters;
        public double BricksLoadingTimeToCPUCache;
        public double BricksLoadingTimeToGPUCache;
        public long NbrBricks;
        public int ScreenWidth;
        public int ScreenHeight;
        public SystemInfoStats SystemInfoStats;
        public List<long> Timestamps;
        public List<float> FrameTimes;
        public List<float> FCTTimes;
        public List<BenchmarkingEvent> Events;
    }


    [RequireComponent(typeof(VolumetricObject))]
    public abstract class BenchmarkingCommon : MonoBehaviour
    {
        protected BenchmarkingStats m_BenchmarkStats;


        private void Awake()
        {
            int approxMaxNbrFrames = 4096;
            m_BenchmarkStats = new BenchmarkingStats()
            {
                Timestamps = new(approxMaxNbrFrames),
                FrameTimes = new(approxMaxNbrFrames),
                Events = new(),
                SystemInfoStats = new SystemInfoStats(),
            };
        }


        private void OnEnable()
        {
            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache += OnAllBricksUploadedToCPUCache;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache += OnAllBricksUploadedToGPUCache;

            VisualizationParametersEvents.ModelOpacityCutoffChange += OnOpacityCutoffChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnInterpolationChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange += OnHomogeneityToleranceChange;
            VisualizationParametersEvents.ModelLODDistancesChange += OnLODDistancesChange;
        }


        private void OnDisable()
        {
            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache -= OnAllBricksUploadedToCPUCache;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache -= OnAllBricksUploadedToGPUCache;

            VisualizationParametersEvents.ModelOpacityCutoffChange -= OnOpacityCutoffChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnInterpolationChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange -= OnHomogeneityToleranceChange;
            VisualizationParametersEvents.ModelLODDistancesChange -= OnLODDistancesChange;
        }


        protected void InitializeVisualizationParameters()
        {
            VolumetricDataset vd = gameObject.GetComponent<VolumetricObject>().GetVolumetricDataset();
            OnOpacityCutoffChange(vd.OpacityCutoff);
            OnSamplingQualityFactorChange(vd.SamplingQualityFactor);
            OnInterpolationChange(vd.InterpolationMethod);
            OnHomogeneityToleranceChange(vd.HomogeneityTolerance);
            OnLODDistancesChange(vd.LODDistances);
        }


        void OnAllBricksUploadedToCPUCache(float time_ms, long nbr_bricks)
        {
            m_BenchmarkStats.BricksLoadingTimeToCPUCache = time_ms;
            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache -= OnAllBricksUploadedToCPUCache;
            m_BenchmarkStats.NbrBricks = nbr_bricks;
        }


        void OnAllBricksUploadedToGPUCache(float time_ms, long nbr_bricks)
        {
            m_BenchmarkStats.BricksLoadingTimeToGPUCache = time_ms;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache -= OnAllBricksUploadedToGPUCache;
            m_BenchmarkStats.NbrBricks = nbr_bricks;
        }


        protected void OnOpacityCutoffChange(float val)
        {
            m_BenchmarkStats.Events.Add(new()
            {
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Type = BenchmarkingEventType.NEW_OPCAITY_CUTOFF_VALUE,
                Value = val,
            });
        }


        protected void OnSamplingQualityFactorChange(float val)
        {
            m_BenchmarkStats.Events.Add(new()
            {
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Type = BenchmarkingEventType.NEW_SAMPLING_QUALITY_FACTOR_VALUE,
                Value = val,
            });
        }


        protected void OnInterpolationChange(INTERPOLATION val)
        {
            m_BenchmarkStats.Events.Add(new()
            {
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Type = BenchmarkingEventType.NEW_INTERPOLATION_METHOD,
                Value = (float)val,
            });
        }


        protected void OnLODDistancesChange(List<float> distances)
        {
            m_BenchmarkStats.Events.Add(new()
            {
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Type = BenchmarkingEventType.NEW_LOD_DISTANCES,
                // TODO: ughh, fix this
                // Value = ,
            });
        }


        protected void OnHomogeneityToleranceChange(byte val)
        {
            m_BenchmarkStats.Events.Add(new()
            {
                Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                Type = BenchmarkingEventType.NEW_HOMOGENEITY_TOLERANCE_VALUE,
                Value = val,
            });
        }

    }
}
