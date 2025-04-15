using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace UnityCTVisualizer
{
    public class ICBenchmarkSetup : MonoBehaviour
    {

        private class InCoreBenchmarking
        {
            public float[] FrameTimes;
            public double BricksLoadingTimeToCPUCache;
            public double BricksLoadingTimeToGPUCache;
            public long NbrBricks;
        }


        public long MAX_NBR_FRAMES = 5000;
        public int randomize_rot_axis_every_nth_frame = 500;
        public float rotation_speed = 30.0f;
        public string output_dir;
        public Vector3 initial_scale = Vector3.one;

        private long nbr_frames = 0;
        bool done = false;

        private InCoreBenchmarking m_InCoreBenchmarking;


        private Vector3 m_rotation_axis;

        private void OnEnable()
        {
            m_InCoreBenchmarking = new InCoreBenchmarking()
            {
                FrameTimes = new float[MAX_NBR_FRAMES]
            };

            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache += OnAllBricksUploadedToCPUCache;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache += OnAllBricksUploadedToGPUCache;
        }

        private void OnDisable()
        {
            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache -= OnAllBricksUploadedToCPUCache;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache -= OnAllBricksUploadedToGPUCache;
        }

        private void Start()
        {
            Debug.Log("started in-core DVR benchmarking ...");
            transform.localScale = initial_scale;
        }

        // Update is called once per frame
        void Update()
        {
            if (!done)
            {
                transform.Rotate(m_rotation_axis, rotation_speed * Time.deltaTime, Space.Self);
                Benchmark();
            }
        }

        void Benchmark()
        {
            if (nbr_frames >= MAX_NBR_FRAMES)
            {
                done = true;

                string fp = Path.Join(output_dir, $"incore_benchmarks_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, m_InCoreBenchmarking);
                }
                Debug.Log($"benchmarking saved at {fp}");

                return;
            }

            if (nbr_frames % randomize_rot_axis_every_nth_frame == 0)
            {
                RandomizeRotationAxis();
            }

            m_InCoreBenchmarking.FrameTimes[nbr_frames] = Time.unscaledDeltaTime * 1000;
            ++nbr_frames;
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

        void OnAllBricksUploadedToCPUCache(float time_ms, long nbr_bricks)
        {
            m_InCoreBenchmarking.BricksLoadingTimeToCPUCache = time_ms;
            VolumetricObject.OnInCoreAllBricksLoadedToCPUCache -= OnAllBricksUploadedToCPUCache;
            m_InCoreBenchmarking.NbrBricks = nbr_bricks;
        }

        void OnAllBricksUploadedToGPUCache(float time_ms, long nbr_bricks)
        {
            m_InCoreBenchmarking.BricksLoadingTimeToGPUCache = time_ms;
            VolumetricObject.OnInCoreAllBricksLoadedToGPUCache -= OnAllBricksUploadedToGPUCache;
            m_InCoreBenchmarking.NbrBricks = nbr_bricks;
        }

    }
}
