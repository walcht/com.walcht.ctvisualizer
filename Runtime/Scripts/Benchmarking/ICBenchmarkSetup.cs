using Newtonsoft.Json;
using System;
using System.IO;
using UnityEngine;

namespace UnityCTVisualizer
{
    /// <summary>
    ///     Initializes a benchmarking setup for an in-core volumetric visualization
    ///     and stores various performance metrics in:
    ///         <code> Application.persistenPath/ic_benchmarks_{time}.json</code>
    /// </summary>
    public class ICBenchmarkSetup : BenchmarkingCommon
    {
        public int m_MaxNbrFrames = 2048;
        public int randomize_rot_axis_every_nth_frame = 200;
        public float rotation_speed = 30.0f;

        private long m_NbrFrames = 0;
        private bool m_Done = false;
        private Vector3 m_RotationAxis;

        private void Start()
        {
#if DEBUG
            Debug.Log("started in-core DVR benchmarking ...");
#endif
            InitializeVisualizationParameters();
        }

        void Update()
        {
            if (!m_Done)
            {
                transform.Rotate(m_RotationAxis, rotation_speed * Time.deltaTime, Space.Self);
                Benchmark();
            }
        }

        void Benchmark()
        {
            if (m_NbrFrames >= m_MaxNbrFrames)
            {
                m_Done = true;

                // set the pipeline parameters
                m_BenchmarkStats.PipelineParameters = GetComponent<VolumetricObject>().GetPipelineParams();

                // set screen resolution parameters
                m_BenchmarkStats.ScreenWidth = Screen.width;
                m_BenchmarkStats.ScreenHeight = Screen.height;

                m_BenchmarkStats.DatasetMetadata = GetComponent<VolumetricObject>().GetVolumetricDataset().Metadata.GetInternalMetadata();

                string fp = Path.Join(Application.persistentDataPath, $"ic_benchmarks_{DateTimeOffset.Now.ToUnixTimeMilliseconds()}.json");
                using (StreamWriter sw = File.CreateText(fp))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, m_BenchmarkStats);
                }
#if DEBUG
                Debug.Log($"benchmarking saved at {fp}");
#endif

                return;
            }

            if (m_NbrFrames % randomize_rot_axis_every_nth_frame == 0)
            {
                m_BenchmarkStats.Events.Add(new()
                {
                    Timestamp = DateTimeOffset.Now.ToUnixTimeMilliseconds(),
                    Type = BenchmarkingEventType.NEW_RANDOM_ROTATION_AXIS,
                });
                RandomizeRotationAxis();
            }

            m_BenchmarkStats.Timestamps.Add(DateTimeOffset.Now.ToUnixTimeMilliseconds());
            m_BenchmarkStats.FrameTimes.Add(Time.unscaledDeltaTime * 1000);
            ++m_NbrFrames;
        }

        void RandomizeRotationAxis()
        {
            // choose a random rotation axis
            float lat = Mathf.Acos(2 * UnityEngine.Random.value - 1) - Mathf.PI / 2.0f;
            float lon = 2 * Mathf.PI * UnityEngine.Random.value;
            m_RotationAxis = new Vector3(
                Mathf.Cos(lat) * Mathf.Cos(lon),
                Mathf.Cos(lat) * Mathf.Sin(lon),
                Mathf.Sin(lat)
            );
        }
    }
}
