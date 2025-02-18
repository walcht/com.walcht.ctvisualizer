using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;

namespace UnityCTVisualizer {
    public interface IProgressHandler {
        /// <summary>
        ///     Progress from 0 to MaxProgressValue. Read and update operataions are thread-safe.
        /// </summary>
        void IncrementProgress();
        int MaxProgressValue { get; set; }
        string Message { set; }

        /// <summary>
        ///     Enable the attached GameObject. Should only be called from main thread!
        /// </summary>
        void Enable();
    }

    public class ProgressHandler : MonoBehaviour, IProgressHandler {
        private int m_progress;
        private string m_progress_txt;
        private bool m_txt_dirty = false;
        private bool m_progress_dirty = false;
        private int m_max_progress_value;

        [SerializeField] TMP_Text m_TextMessage;
        [SerializeField] TMP_Text m_PercentageText;
        [SerializeField] RectTransform m_ProgressBar;

        void OnEnable() {
            m_ProgressBar.anchorMax = new Vector2(0.0f, 1.0f);
            m_PercentageText.text = "0 %";
        }

        public void IncrementProgress() {
            Interlocked.Increment(ref m_progress);
            m_progress_dirty = true;
        }

        public string Message { set { m_progress_txt = value; m_txt_dirty = true; } }

        public int MaxProgressValue {
            get => m_max_progress_value;
            set => m_max_progress_value = value;
        }

        public void Enable() {
            gameObject.SetActive(true);
        }

        void Update() {
            if (m_progress_dirty) {
                float p = Mathf.Clamp01(m_progress / (float)m_max_progress_value);
                m_ProgressBar.anchorMax = new Vector2(p, 1.0f);
                m_PercentageText.text = $"{Mathf.FloorToInt(p * 100.0f)} %";
                m_progress_dirty = false;
            }
            if (m_txt_dirty) {
                m_TextMessage.text = m_progress_txt;
                m_txt_dirty = false;
            }
        }
    }
}
