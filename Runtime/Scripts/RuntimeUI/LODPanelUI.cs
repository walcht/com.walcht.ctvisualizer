using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform), typeof(Image))]
    public class LODPanelComponent : MonoBehaviour
    {
        private RectTransform m_ControlPointTransform;
        private Image m_Image;
        private TMP_Text m_LODText;

        private RectTransform m_ParentRect;
        private Vector2 m_Position;

        private void Awake()
        {
            m_ControlPointTransform = GetComponent<RectTransform>();
            m_Image = GetComponent<Image>();
            m_LODText = GetComponentInChildren<TMP_Text>();
        }


        public void Init(Vector2 normalized_position, RectTransform parentRect)
        {
            m_ParentRect = parentRect;
            SetPosition(normalized_position);
        }


        public void SetPosition(Vector2 startEndPos)
        {
            m_Position = new Vector2(Mathf.Clamp01(startEndPos.x), Mathf.Clamp01(startEndPos.y));
            m_ControlPointTransform.anchorMin = new(m_Position.x, m_ControlPointTransform.anchorMin.y);
            m_ControlPointTransform.anchorMax = new(m_Position.y, m_ControlPointTransform.anchorMax.y);
            m_ControlPointTransform.SetSizeWithCurrentAnchors(RectTransform.Axis.Horizontal, Mathf.Abs(m_Position.y - m_Position.x)
                * m_ParentRect.rect.width);
        }


        public void SetStartPosition(float x)
        {
            SetPosition(new Vector2(Mathf.Clamp01(x), m_Position.y));
        }


        public void SetEndPosition(float y)
        {
            SetPosition(new Vector2(m_Position.x, Mathf.Clamp01(y)));
        }


        public void SetLODLevel(int lod)
        {
            m_LODText.text = lod.ToString();
            gameObject.name = $"lod_panel_lvl_{lod}";
        }


        public void SetLODColor(Color lodColor)
        {
            m_Image.color = lodColor;
        }
    }
}
