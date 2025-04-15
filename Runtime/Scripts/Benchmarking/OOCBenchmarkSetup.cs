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
    public class OOCBenchmarkSetup : MonoBehaviour
    {

        private interface IState
        {
            public void OnEnter();
            public void UpdateState();
            public void OnExit();
        }

        private class OutOfCoreBenchmarks
        {
            public float[] FrameTimes;
            public float[] FCTTimes;
        }

        private class BrickCacheWarmupState : IState
        {
            private OOCBenchmarkSetup m_controller;
            private double start_time;
            private float rotation_speed = 30.0f;
            private float duration = 3.0f;


            public BrickCacheWarmupState(OOCBenchmarkSetup controller)
            {
                m_controller = controller;
            }

            public void OnEnter()
            {
                Debug.Log("benchmarking started ...");
                start_time = Time.realtimeSinceStartupAsDouble;
            }

            public void UpdateState()
            {
                if (Time.realtimeSinceStartupAsDouble - start_time > duration)
                {
                    m_controller.TransitionToState(new FrameTimeMeasurementsState(m_controller));
                    return;
                }
                m_controller.transform.Rotate(Vector3.forward, rotation_speed * Time.deltaTime, Space.Self);
            }

            public void OnExit()
            {
                Debug.Log("BrickCacheWarmupState completed");
            }
        }


        private class FrameTimeMeasurementsState : IState
        {
            private OOCBenchmarkSetup m_controller;

            private float rotation_speed = 30.0f;
            private Vector3 m_rotation_axis;

            private readonly int MAX_NBR_FRAMES = 2000;

            private int nbr_frames = 0;
            public int randomize_rot_axis_every_nth_frame = 250;

            public FrameTimeMeasurementsState(OOCBenchmarkSetup controller)
            {
                m_controller = controller;
                m_controller.m_ooc_benchmarks.FrameTimes = new float[MAX_NBR_FRAMES];
            }

            public void OnEnter()
            {
                RandomizeRotationAxis();
            }

            public void UpdateState()
            {
                if (nbr_frames >= MAX_NBR_FRAMES)
                {
                    m_controller.TransitionToState(new FCTMeasurementState(m_controller));
                    return;
                }
                m_controller.transform.Rotate(m_rotation_axis, rotation_speed * Time.deltaTime, Space.Self);
                m_controller.m_ooc_benchmarks.FrameTimes[nbr_frames] = Time.unscaledDeltaTime * 1000;
                ++nbr_frames;
                if (nbr_frames % randomize_rot_axis_every_nth_frame == 0)
                {
                    RandomizeRotationAxis();
                }
            }

            public void OnExit()
            {
                Debug.Log("FrameTimeMeasurementsState done.");
            }

            void RandomizeRotationAxis()
            {
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
            private OOCBenchmarkSetup m_controller;
            private double start_time;
            private Vector3 look_at_point;

            public int MAX_NBR_MEASUREMENTS = 10;
            private int nbr_measurements = 0;

            // in seconds
            private float threshold = 5;

            public FCTMeasurementState(OOCBenchmarkSetup controller)
            {
                m_controller = controller;
                m_controller.m_ooc_benchmarks.FCTTimes = new float[MAX_NBR_MEASUREMENTS];
            }

            public void OnEnter()
            {
                // choose a random rotation axis
                RandomizeLookAtPoint();

                VolumetricObject.OnNoMoreBrickRequests += OnNoMoreBrickRequests;

                start_time = Time.realtimeSinceStartupAsDouble;
            }

            public void UpdateState()
            {
                if (Time.realtimeSinceStartupAsDouble - start_time > threshold)
                {
                    m_controller.TransitionToState(null);
                    return;
                }
            }

            public void OnExit()
            {
                VolumetricObject.OnNoMoreBrickRequests -= OnNoMoreBrickRequests;

                string fp = Path.Join(m_controller.output_dir, $"ooc_benchmarks_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, m_controller.m_ooc_benchmarks);
                }
                Debug.Log($"benchmarking saved at {fp}");
            }

            private void OnNoMoreBrickRequests()
            {
                m_controller.m_ooc_benchmarks.FCTTimes[nbr_measurements] =
                    (float)((Time.realtimeSinceStartupAsDouble - start_time) * 1000);
                ++nbr_measurements;
                if (nbr_measurements >= MAX_NBR_MEASUREMENTS)
                {
                    m_controller.TransitionToState(null);
                }
                RandomizeLookAtPoint();
                start_time = Time.realtimeSinceStartupAsDouble;
            }

            private void RandomizeLookAtPoint()
            {
                float lat = Mathf.Acos(2 * UnityEngine.Random.value - 1) - Mathf.PI / 2.0f;
                float lon = 2 * Mathf.PI * UnityEngine.Random.value;
                look_at_point = new Vector3(
                    Mathf.Cos(lat) * Mathf.Cos(lon),
                    Mathf.Cos(lat) * Mathf.Sin(lon),
                    Mathf.Sin(lat)
                );
                m_controller.transform.LookAt(look_at_point, worldUp: Vector3.up);
            }
        }

        public string output_dir;
        private IState m_curr_state = null;
        private OutOfCoreBenchmarks m_ooc_benchmarks;


        void Start()
        {
            TransitionToState(new BrickCacheWarmupState(this));
        }


        void Update()
        {
            m_curr_state?.UpdateState();
        }


        private void TransitionToState(IState new_state)
        {
            m_curr_state?.OnExit();
            m_curr_state = new_state;
            m_curr_state?.OnEnter();
        }
    }
}
