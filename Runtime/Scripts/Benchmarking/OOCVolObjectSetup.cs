using System;
using UnityEngine;
using System.Threading.Tasks;
using System.Collections;
using UnityEngine.InputSystem.LowLevel;
using System.Collections.Generic;
using Newtonsoft.Json;
using System.IO;

namespace UnityCTVisualizer
{
    /// <summary>
    ///     Out-of-core benchmarking setup. Attach to a volumetric object to enable a benchmarking
    ///     setup. Results are reported in the debug console.
    /// </summary>
    public class OOCVolObjectSetup : MonoBehaviour
    {
        private interface IState
        {
            public void OnEnter();
            public void UpdateState();
            public void OnExit();
        }

        private class BrickCacheWarmupState : IState
        {
            private OOCVolObjectSetup m_controller;
            private double start_time;
            private float rotation_speed = 30.0f;
            private float duration = 3.0f;

            public BrickCacheWarmupState(OOCVolObjectSetup controller)
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
            private OOCVolObjectSetup m_controller;

            private float rotation_speed = 30.0f;
            private Vector3 m_rotation_axis;

            private readonly int MAX_NBR_FRAMES = 2000;

            private float[] frame_times;
            private int nbr_frames = 0;
            public int randomize_rot_axis_every_nth_frame = 250;

            public FrameTimeMeasurementsState(OOCVolObjectSetup controller)
            {
                m_controller = controller;
                frame_times = new float[MAX_NBR_FRAMES];
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
                frame_times[nbr_frames] = Time.unscaledDeltaTime * 1000;
                ++nbr_frames;
                if (nbr_frames % randomize_rot_axis_every_nth_frame == 0)
                {
                    RandomizeRotationAxis();
                }
            }

            public void OnExit()
            {
                string fp = @"C:\Users\walid\Desktop\benchmarks\FrameTimeMeasurementsState.json";
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, frame_times);
                }
                Debug.Log($"FrameTimeMeasurementsState done. Json at: {fp}");
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
            private OOCVolObjectSetup m_controller;
            private double start_time;
            private Vector3 look_at_point;

            public int nbr_measurements = 10;

            // in seconds
            private float threshold = 5;

            // hold measurements
            private List<float> m_fct_measurements;

            public FCTMeasurementState(OOCVolObjectSetup controller)
            {
                m_controller = controller;
                m_fct_measurements = new List<float>(nbr_measurements);
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
                    m_fct_measurements.Add(float.PositiveInfinity);
                    m_controller.TransitionToState(null);
                    return;
                }
            }

            public void OnExit()
            {
                VolumetricObject.OnNoMoreBrickRequests -= OnNoMoreBrickRequests;

                string fp = @"C:\Users\walid\Desktop\benchmarks\FCTMeasurementState.json";
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, m_fct_measurements);
                }
                Debug.Log($"FCTMeasurementState done. Json at: {fp}");
                Debug.Log("benchmarking done.");
            }

            private void OnNoMoreBrickRequests()
            {
                m_fct_measurements.Add((float)((Time.realtimeSinceStartupAsDouble - start_time) * 1000));
                RandomizeLookAtPoint();
                start_time = Time.realtimeSinceStartupAsDouble;
                if (m_fct_measurements.Count >= nbr_measurements)
                {
                    m_controller.TransitionToState(null);
                }
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

        private IState m_curr_state = null;

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
