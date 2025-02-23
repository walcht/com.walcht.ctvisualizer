using System.Collections;
using System.Threading;
using TMPro;
using UnityEngine;

namespace UnityCTVisualizer {
    public class ProgressHandler : MonoBehaviour {
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
            m_TextMessage.text = "";

            // progress bar handler events
            ProgressHandlerEvents.OnRequestMaxProgressValueUpdate += OnRequestMaxProgressValueUpdate;
            ProgressHandlerEvents.OnRequestProgressValueIncrement += OnRequestProgressValueIncrement;
            ProgressHandlerEvents.OnRequestProgressMessageUpdate += OnRequestProgressMessageUpdate;
        }

        void OnDisable() {
            // progress bar handler events
            ProgressHandlerEvents.OnRequestMaxProgressValueUpdate -= OnRequestMaxProgressValueUpdate;
            ProgressHandlerEvents.OnRequestProgressValueIncrement -= OnRequestProgressValueIncrement;
            ProgressHandlerEvents.OnRequestProgressMessageUpdate -= OnRequestProgressMessageUpdate;
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

        void OnRequestMaxProgressValueUpdate(int val) => m_max_progress_value = val;
        void OnRequestProgressValueIncrement() {
            Interlocked.Increment(ref m_progress);
            m_progress_dirty = true;
        }
        void OnRequestProgressMessageUpdate(string msg) {
            m_progress_txt = msg;
            m_txt_dirty = true;
        }
    }
}
