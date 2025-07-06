using UnityEngine;
using TMPro;
using System.Collections;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    public class PipelineStatsUI : MonoBehaviour
    {
        //////////////////////////////
        /// TMP InputFields
        //////////////////////////////

        public float m_UpdateFrequency = 4;
        public Color m_EmptyBrickCacheColor = Color.green;
        public Color m_FullBrickCacheColor = Color.red;

        [SerializeField] TMP_Text m_NbrRequestedBricksText;
        [SerializeField] RectTransform m_ProgressBar;
        [SerializeField] Image m_ProgressBarImg;
        [SerializeField] TMP_Text m_ProgressBarText;

        private bool m_ProgressBarDirty = false;
        private float m_ProgressBarPercentage = 0;
        private int m_NbrGPUBricksUsed = 0;
        private int m_TotalNbrGPUBricks = 1;
        private float m_BrickSizeInMBs = 1;

        private int m_NbrRequestedBricks = 0;

        private void Awake()
        {
            m_NbrRequestedBricksText.text = "pipeline not initialized";
        }

        private void OnEnable()
        {
            m_ProgressBar.anchorMax = new Vector2(0.0f, 1.0f);

            PipelineStatEvents.ModelNbrBrickRequestsChange += OnModelNbrBrickRequestsChange;
            PipelineStatEvents.ModelGPUBrickCacheUsageChange += OnModelGPUBrickCacheUsageChange;
        }

        private void OnDisable()
        {

            PipelineStatEvents.ModelNbrBrickRequestsChange -= OnModelNbrBrickRequestsChange;
            PipelineStatEvents.ModelGPUBrickCacheUsageChange -= OnModelGPUBrickCacheUsageChange;
        }

        IEnumerator UpdateUIs()
        {
            while (true)
            {
                yield return new WaitForSecondsRealtime(1.0f / m_UpdateFrequency);
                if (m_ProgressBarDirty)
                {
                    m_ProgressBar.anchorMax = new Vector2(m_ProgressBarPercentage, 1.0f);
                    m_ProgressBarImg.color = Color.Lerp(m_EmptyBrickCacheColor, m_FullBrickCacheColor, Mathf.Clamp01((m_ProgressBarPercentage - 0.5f) * 2));
                    m_ProgressBarText.text = $"{Mathf.RoundToInt(m_NbrGPUBricksUsed * m_BrickSizeInMBs)}/{Mathf.RoundToInt(m_TotalNbrGPUBricks * m_BrickSizeInMBs)}MB {Mathf.FloorToInt(m_ProgressBarPercentage * 100.0f)} %";
                    m_ProgressBarDirty = false;
                }
                m_NbrRequestedBricksText.text = m_NbrRequestedBricks.ToString();
                m_NbrRequestedBricks = 0;
            }
        }

        private void Start()
        {
            StartCoroutine(UpdateUIs());
        }


        private void OnModelNbrBrickRequestsChange(int val)
        {
            m_NbrRequestedBricks = Mathf.Max(m_NbrRequestedBricks, val);
        }


        private void OnModelGPUBrickCacheUsageChange(int nbr_bricks_used, int total_nbr_bricks, int brick_size_cubed)
        {
            m_ProgressBarPercentage = nbr_bricks_used / (float)total_nbr_bricks;
            m_NbrGPUBricksUsed = nbr_bricks_used;
            m_TotalNbrGPUBricks = total_nbr_bricks;
            m_BrickSizeInMBs = brick_size_cubed / (1024.0f * 1024.0f);
            m_ProgressBarDirty = true;
        }
    }
}
