using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform), typeof(Selectable))]
    public class LODDistanceControlPointUI
        : MonoBehaviour,
            IDragHandler,
            IBeginDragHandler
    {
        public event Action<float, int> OnPositionChanged;

        [SerializeField] private Image m_Image;

        private Selectable m_ControlPointSelectable;
        private RectTransform m_ControlPointTransform;


        private int m_ResLvl;
        private float m_Position;

        private void Awake()
        {
            m_ControlPointTransform = GetComponent<RectTransform>();
            m_ControlPointSelectable = GetComponent<Selectable>();
        }


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
            m_ControlPointTransform.anchorMin = new Vector2(m_Position, m_ControlPointTransform.anchorMin.y);
            m_ControlPointTransform.anchorMax = new Vector2(m_Position, m_ControlPointTransform.anchorMax.y);
            // don't forget to reset rect position after updating anchors
            m_ControlPointTransform.anchoredPosition = Vector3.zero;
        }


        public void OnBeginDrag(PointerEventData _)
        {
            m_ControlPointSelectable.Select();
        }


        public void OnDrag(PointerEventData eventData)
        {
            m_Position += (eventData.delta.x / Screen.width);
            OnPositionChanged?.Invoke(m_Position, m_ResLvl);
        }
    }
}