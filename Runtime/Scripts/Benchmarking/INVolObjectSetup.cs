using Newtonsoft.Json;
using System.IO;
using UnityEngine;

namespace UnityCTVisualizer
{
    public class INVolObjectSetup : MonoBehaviour
    {
        public long MAX_NBR_FRAMES = 5000;
        public int randomize_rot_axis_every_nth_frame = 500;
        public float rotation_speed = 30.0f;
        public string output;
        public Vector3 initial_scale = Vector3.one;

        private long nbr_frames = 0;
        private float[] frame_times;
        bool done = false;

        private Vector3 m_rotation_axis;

        private void OnEnable()
        {
            frame_times = new float[MAX_NBR_FRAMES];
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

                using (StreamWriter sw = File.CreateText(output))
                {
                    JsonSerializer serializer = new()
                    {
                        Formatting = Formatting.Indented
                    };
                    serializer.Serialize(sw, frame_times);
                }
                Debug.Log("benchmarking done.");

                return;
            }

            if (nbr_frames % randomize_rot_axis_every_nth_frame == 0)
            {
                RandomizeRotationAxis();
            }

            frame_times[nbr_frames] = Time.unscaledDeltaTime * 1000;
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

    }
}
