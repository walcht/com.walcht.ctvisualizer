using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform), typeof(Button))]
    public class AlphaControlPointUI
        : MonoBehaviour,
            IDragHandler,
            IBeginDragHandler,
            IPointerClickHandler,
            ISelectHandler,
            IDeselectHandler
    {
        /// <summary>
        ///     Invoked when this alpha control point is selected. The ID assigned to this control point is passed.
        /// </summary>
        public event Action<int> ControlPointSelected;

        /// <summary>
        ///     Invoked when this alpha control point is deselected. The ID assigned to this control point is passed.
        /// </summary>
        public event Action<int> ControlPointDeselected;

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////// IN-CURRENT or IN-CHILDREN REFERENCES //////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        [SerializeField]
        private Selectable m_ControlPointSelectable;

        [SerializeField]
        private RectTransform m_ControlPointTransform;

        private RectTransform m_HistogramTransform;

        [SerializeField]
        private Image m_Image;

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        //////////////////////////////////////////////// MISC ///////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        private int m_ID;

        private Vector2 m_Scale = new(1.0f, 1.0f);
        private Vector2 m_ScaleInverse = new(1.0f, 1.0f);
        private Vector2 m_Translation = new(0.0f, 0.0f);

        // underlying alpha control point data
        ControlPoint<float, float> m_ControlPoint;
        public ControlPoint<float, float> ControlPointData
        {
            get => m_ControlPoint;
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////// INITIALIZERS ////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///     Initializes control point's attributes. Call this before enabling this behaviour.
        /// </summary>
        /// <param name="id">the unique ID assigned to this control point. Useful for identifying it</param>
        /// <param name="cpData">initial control point data</param>
        public void Init(int id, ControlPoint<float, float> cpData, RectTransform histogramTransform,
            Vector2 scale, Vector2 translation)
        {
            m_HistogramTransform = histogramTransform;
            m_ID = id;
            m_ControlPoint = cpData;
            SetScale(scale);
            SetTranslation(translation);
            SetPosition(cpData.Position, cpData.Value);
        }

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////////// SETTERS //////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        /// <summary>
        ///     Sets the position for this alpha control point; both in UI and in underlying control point data
        /// </summary>
        public void SetPosition(float x, float y)
        {
            m_ControlPoint.Position = Mathf.Clamp01(x);
            m_ControlPoint.Value = Mathf.Clamp01(y);
            UpdateUIPosition();
        }


        /// <summary>
        ///     Sets the scale that this alpha control point should use. Should be called whenever the histogram's
        ///     scale changes (e.g., when zooming in).
        /// </summary>
        /// <param name="scale"></param>
        public void SetScale(Vector2 scale)
        {
            m_Scale = scale;
            m_ScaleInverse = new(1.0f / m_Scale.x, 1.0f / m_Scale.y);
            UpdateUIPosition();
        }


        public void SetTranslation(Vector2 translation)
        {
            m_Translation = translation;
            UpdateUIPosition();
        }


        /////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////// POINTER EVENTS ///////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        public void OnBeginDrag(PointerEventData eventData)
        {
            if (m_ControlPointSelectable.interactable)
                m_ControlPointSelectable.Select();
        }

        public void OnDrag(PointerEventData eventData)
        {
            if (m_ControlPointSelectable.interactable)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_HistogramTransform,
                    eventData.position,
                    eventData.pressEventCamera,
                    out Vector2 rectLocalPos
                );
                Vector2 normalizedPos = new(m_ScaleInverse.x * Mathf.Clamp01(rectLocalPos.x / m_HistogramTransform.rect.width) + m_Translation.x,
                    m_ScaleInverse.y * Mathf.Clamp01(rectLocalPos.y / m_HistogramTransform.rect.height) + m_Translation.y);
                SetPosition(normalizedPos.x, normalizedPos.y);
            }
        }

        public void OnPointerClick(PointerEventData eventData)
        {
            if (m_ControlPointSelectable.interactable)
                m_ControlPointSelectable.Select();
        }

        public void OnSelect(BaseEventData eventData)
        {
            if (m_ControlPointSelectable.interactable)
                ControlPointSelected?.Invoke(m_ID);
        }

        public void OnDeselect(BaseEventData eventData)
        {
            if (m_ControlPointSelectable.interactable)
                ControlPointDeselected?.Invoke(m_ID);
        }


        public bool Interactable
        {
            get => m_ControlPointSelectable.interactable;
            set => m_ControlPointSelectable.interactable = value;
        }


        /// <summary>
        ///     Updates this control point's UI position using the underlying control point's X position
        ///     and alpha value (i.e., Y position).
        /// </summary>
        private void UpdateUIPosition()
        {
            float x = Mathf.Clamp01((m_ControlPoint.Position - m_Translation.x) * m_Scale.x);
            float y = Mathf.Clamp01((m_ControlPoint.Value - m_Translation.y) * m_Scale.y);
            Vector2 newAnchor = new(x, y);
            m_ControlPointTransform.anchorMin = newAnchor;
            m_ControlPointTransform.anchorMax = newAnchor;

            // don't forget to reset rect position after updating anchors
            m_ControlPointTransform.anchoredPosition = Vector3.zero;
        }
    }
}
