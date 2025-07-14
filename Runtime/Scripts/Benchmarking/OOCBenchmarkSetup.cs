using UnityEngine;
using Newtonsoft.Json;
using System.IO;
using System;

namespace UnityCTVisualizer
{
    /// <summary>
    ///     Out-of-core benchmarking setup. Attach to a volumetric object to enable a benchmarking
    ///     setup. Results are reported in the debug console.
    /// </summary>
    public class OOCBenchmarkSetup : BenchmarkingCommon
    {

        private interface IState
        {
            public void OnEnter();
            public void UpdateState();
            public void OnExit();
        }


        private class BrickCacheWarmupState : IState
        {
            private readonly OOCBenchmarkSetup m_Controller;
            private readonly float m_RotationSpeed = 30.0f;
            private readonly float m_Duration = 3.0f;
            private double m_StartTime;


            public BrickCacheWarmupState(OOCBenchmarkSetup controller)
            {
                m_Controller = controller;
            }

            public void OnEnter()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.BRICK_CACHE_WARMUP_START,
                });
#if DEBUG
                Debug.Log("benchmarking started ...");
#endif
                m_StartTime = Time.realtimeSinceStartupAsDouble;
            }

            public void UpdateState()
            {
                // report current frame time and current timestamp
                m_Controller.m_BenchmarkStats.Timestamps.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds());
                m_Controller.m_BenchmarkStats.FrameTimes.Add(Time.unscaledDeltaTime * 1000);

                if (Time.realtimeSinceStartupAsDouble - m_StartTime > m_Duration)
                {
                    m_Controller.TransitionToState(new FrameTimeMeasurementsState(m_Controller));
                    return;
                }
                m_Controller.transform.Rotate(Vector3.forward, m_RotationSpeed * Time.deltaTime, Space.Self);
            }

            public void OnExit()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.BRICK_CACHE_WARMUP_END,
                });
#if DEBUG
                Debug.Log("BrickCacheWarmupState completed");
#endif
            }
        }


        private class FrameTimeMeasurementsState : IState
        {
            private readonly OOCBenchmarkSetup m_Controller;

            private float rotation_speed = 30.0f;
            private Vector3 m_rotation_axis;

            private readonly int MAX_NBR_FRAMES = 2000;

            private int nbr_frames = 0;
            public int randomize_rot_axis_every_nth_frame = 250;

            public FrameTimeMeasurementsState(OOCBenchmarkSetup controller)
            {
                m_Controller = controller;
            }

            public void OnEnter()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.FRAMETIMES_MEASUREMENTS_START,
                });

                RandomizeRotationAxis();
            }

            public void UpdateState()
            {
                // report current frame time and current timestamp
                m_Controller.m_BenchmarkStats.Timestamps.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds());
                m_Controller.m_BenchmarkStats.FrameTimes.Add(Time.unscaledDeltaTime * 1000);

                if (nbr_frames >= MAX_NBR_FRAMES)
                {
                    m_Controller.TransitionToState(new FCTMeasurementState(m_Controller));
                    return;
                }
                m_Controller.transform.Rotate(m_rotation_axis, rotation_speed * Time.deltaTime, Space.Self);
                ++nbr_frames;
                if (nbr_frames % randomize_rot_axis_every_nth_frame == 0)
                {
                    RandomizeRotationAxis();
                }
            }

            public void OnExit()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.FRAMETIMES_MEASUREMENTS_END,
                });

#if DEBUG
                Debug.Log("FrameTimeMeasurementsState done.");
#endif
            }

            void RandomizeRotationAxis()
            {
                // report a new random rotation axis selection
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.NEW_RANDOM_ROTATION_AXIS,
                });

                // choose a random rotation axis
                float lat = Mathf.Acos(2 * UnityEngine.Random.value - 1) - Mathf.PI / 2.0f;
                float lon = 2 * Mathf.PI * UnityEngine.Random.value;
                m_rotation_axis = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Cos(lat) * Mathf.Sin(lon),
                    Mathf.Sin(lat)
                );
            }
        }


        private class FCTMeasurementState : IState
        {
            private readonly OOCBenchmarkSetup m_Controller;
            private double m_StartTime;
            private Vector3 look_at_point;

            public int MAX_NBR_MEASUREMENTS = 10;
            private int nbr_measurements = 0;

            // in seconds
            private readonly float m_Threshold = 60;

            public FCTMeasurementState(OOCBenchmarkSetup controller)
            {
                m_Controller = controller;
                m_Controller.m_BenchmarkStats.FCTTimes = new(MAX_NBR_MEASUREMENTS);
            }

            public void OnEnter()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.FCT_MEASUREMENTS_START,
                });

                // choose a random rotation axis
                RandomizeLookAtPoint();

                VolumetricObject.OnNoMoreBrickRequests += OnNoMoreBrickRequests;

                m_StartTime = Time.realtimeSinceStartupAsDouble;
            }

            public void UpdateState()
            {
                // report current frame time and current timestamp
                m_Controller.m_BenchmarkStats.Timestamps.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds());
                m_Controller.m_BenchmarkStats.FrameTimes.Add(Time.unscaledDeltaTime * 1000);

                if (Time.realtimeSinceStartupAsDouble - m_StartTime > m_Threshold)
                {
                    m_Controller.TransitionToState(null);
                    return;
                }
            }

            public void OnExit()
            {
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.FCT_MEASUREMENTS_END,
                });

                VolumetricObject.OnNoMoreBrickRequests -= OnNoMoreBrickRequests;

                // set the pipeline parameters
                var dataset = m_Controller.gameObject.GetComponent<VolumetricObject>();
                m_Controller.m_BenchmarkStats.PipelineParameters = dataset.GetPipelineParams();

                // set screen resolution parameters
                m_Controller.m_BenchmarkStats.ScreenWidth = Screen.width;
                m_Controller.m_BenchmarkStats.ScreenHeight = Screen.height;

                m_Controller.m_BenchmarkStats.DatasetMetadata = dataset.GetVolumetricDataset().Metadata.GetInternalMetadata();


                string fp = Path.Join(Application.persistentDataPath, $"ooc_benchmarks_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, m_Controller.m_BenchmarkStats);
                }
#if DEBUG
                Debug.Log($"benchmarking saved at {fp}");
#endif
            }

            private void OnNoMoreBrickRequests()
            {
                m_Controller.m_BenchmarkStats.FCTTimes.Add((float)(Time.realtimeSinceStartupAsDouble - 2 - m_StartTime));
                ++nbr_measurements;
                if (nbr_measurements >= MAX_NBR_MEASUREMENTS)
                {
                    m_Controller.TransitionToState(null);
                }
                RandomizeLookAtPoint();
                m_StartTime = Time.realtimeSinceStartupAsDouble;
            }

            private void RandomizeLookAtPoint()
            {
                // report a new random look-at point 
                m_Controller.m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.NEW_RANDOM_LOOK_AT_POINT,
                });

                float lat = Mathf.Acos(2 * UnityEngine.Random.value - 1) - Mathf.PI / 2.0f;
                float lon = 2 * Mathf.PI * UnityEngine.Random.value;
                look_at_point = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Cos(lat) * Mathf.Sin(lon),
                    Mathf.Sin(lat)
                );
                m_Controller.transform.LookAt(look_at_point, worldUp: Vector3.up);
            }
        }

        private IState m_CurrState = null;


        void Start()
        {
            InitializeVisualizationParameters();
            TransitionToState(new BrickCacheWarmupState(this));
        }


        void Update()
        {
            m_CurrState?.UpdateState();
        }


        private void TransitionToState(IState new_state)
        {
            m_CurrState?.OnExit();
            m_CurrState = new_state;
            m_CurrState?.OnEnter();
        }
    }
}
