using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform))]
    public class LODDistanceControlPointUI
        : MonoBehaviour,
            IDragHandler,
            IBeginDragHandler
    {
        public event Action<float, int> OnPositionChanged;

        [SerializeField]
        private Selectable m_ControlPointSelectable;

        [SerializeField]
        private RectTransform m_ControlPointTransform;

        [SerializeField]
        private Image m_Image;

        private Vector2 m_AnchorMin = new(0, 0.0f);
        private Vector2 m_AnchorMax = new(0, 1.0f);

        private int m_ResLvl;
        private float m_Position;


        /// <summary>
        ///     Initializes control point's attributes. Call this before enabling this behaviour.
        /// </summary>
        /// <param name="id">the unique ID assigned to this control point. Useful for identifying it</param>
        /// <param name="cpData">initial control point data</param>
        public void Init(float normalized_position, int res_lvl)
        {
            m_ResLvl = res_lvl;
            SetPosition(normalized_position);
        }


        /// <summary>
        ///     Sets the position for this LOD distance control point.
        /// </summary>
        /// 
        /// <param name="pos">
        ///     Normalized position. Values not in the range [0.0, 1.0] are clamped.
        /// </param>
        public void SetPosition(float pos)
        {
            m_Position = Mathf.Clamp01(pos);
            m_AnchorMin.x = m_Position;
            m_AnchorMax.x = m_Position;
            m_ControlPointTransform.anchorMin = m_AnchorMin;
            m_ControlPointTransform.anchorMax = m_AnchorMax;
            // don't forget to reset rect position after updating anchors
            m_ControlPointTransform.anchoredPosition = Vector3.zero;
        }


        public void OnBeginDrag(PointerEventData eventData)
        {
            m_ControlPointSelectable.Select();
        }


        public void OnDrag(PointerEventData eventData)
        {
            SetPosition(m_Position + (eventData.delta.x / Screen.width));
            OnPositionChanged?.Invoke(m_Position, m_ResLvl);
        }
    }
}