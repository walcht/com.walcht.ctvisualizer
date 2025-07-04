using System;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine.InputSystem;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RawImage))]
    public class HistogramUI : MonoBehaviour, IPointerClickHandler, IPointerEnterHandler, IPointerExitHandler
    {
        public event Action<Vector2> OnAddAlphaControlPoint;
        public event Action<float, Vector2> OnHistogramZoom;

        private RectTransform m_RectTransform;

        [SerializeField]
        InputActionReference m_UIScrollActionRef;

        private Camera m_ParentCanvasCam = null;


        void Awake()
        {
            m_RectTransform = GetComponent<RectTransform>();
            Canvas[] canvases = GetComponentsInParent<Canvas>();
            m_ParentCanvasCam = canvases[^1].worldCamera;
        }


        public void OnPointerClick(PointerEventData eventData)
        {
            if (eventData.button == PointerEventData.InputButton.Right)
            {
                RectTransformUtility.ScreenPointToLocalPointInRectangle(
                    m_RectTransform,
                    eventData.pressPosition,
                    eventData.pressEventCamera,
                    out Vector2 rectLocalPos
                );
                rectLocalPos.x =
                    (rectLocalPos.x + m_RectTransform.pivot.x * m_RectTransform.rect.width)
                    / m_RectTransform.rect.width;
                rectLocalPos.y =
                    (rectLocalPos.y + m_RectTransform.pivot.y * m_RectTransform.rect.height)
                    / m_RectTransform.rect.height;
                OnAddAlphaControlPoint?.Invoke(rectLocalPos);
            }
        }


        public void OnPointerEnter(PointerEventData eventData) => m_UIScrollActionRef.action.performed += OnScroll;


        public void OnPointerExit(PointerEventData eventData) => m_UIScrollActionRef.action.performed -= OnScroll;


        private void OnScroll(InputAction.CallbackContext context)
        {
            var scroll = context.ReadValue<Vector2>();

            // ignore "non-scroll" emitted events. y component takes the values: {+1, 0, -1}
            if (scroll.y == 0)
                return;

            Vector2 rectLocalPos = new(0, 0);
            OnHistogramZoom?.Invoke(scroll.y, rectLocalPos);
        }
    }
}
