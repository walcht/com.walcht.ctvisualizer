using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.XR.Interaction.Toolkit;
using UnityEngine.XR.Interaction.Toolkit.Interactables;

namespace UnityCTVisualizer {
    [RequireComponent(typeof(XRSimpleInteractable))]
    public class VolumetricObjectController : MonoBehaviour {
        [Range(0.05f, 0.5f)]
        public float m_ScaleSpeed = 0.25f;

        [Range(0.01f, 1.0f)]
        public float m_ScaleSlowDown = 1.0f;

        [Range(5.0f, 10.0f)]
        public float m_MaxScale = 10.0f;

        public bool m_Rotate = true;

        [Range(0.0f, 360.0f)]
        public float m_RotationSpeed = 40.0f;

        Transform m_Transform;
        InputAction m_TrackpadAction;
        XRSimpleInteractable m_SimpleInteractable;

        float m_ScaleSpeedModifier = 1;
        Vector3 m_OriginalScale;
        Vector3 m_MaxScaleVect;

        void Awake() {
            m_Transform = GetComponent<Transform>();
            m_SimpleInteractable = GetComponent<XRSimpleInteractable>();
            m_TrackpadAction = InputSystem.actions.FindAction("Trackpad");
            // m_SimpleInteractable.hoverEntered.AddListener(OnHoverEntered);
            // m_SimpleInteractable.hoverExited.AddListener(OnHoverExited);

            /*
            m_InputLayer.VolumetricObjectControls.SlowScaleActivator.performed += (
                InputAction.CallbackContext _
            ) => m_ScaleSpeedModifier = m_ScaleSlowDown;
            m_InputLayer.VolumetricObjectControls.SlowScaleActivator.canceled += (
                InputAction.CallbackContext _
            ) => m_ScaleSpeedModifier = 1.0f;
            */

            m_OriginalScale = m_Transform.localScale;
            m_MaxScaleVect = m_OriginalScale * m_MaxScale;
        }

        float t = 0.0f;

        void OnScale(InputAction.CallbackContext context) {
            Vector2 scroll = context.ReadValue<Vector2>();
            if (scroll.x > 0) {
                t = Mathf.Clamp01(t + m_ScaleSpeed * m_ScaleSpeedModifier);
                m_Transform.localScale = Vector3.Lerp(m_OriginalScale, m_MaxScaleVect, t);
            }
            // this has to be done because on linux we get 120, 0, -120
            else if (scroll.x < 0) {
                t = Mathf.Clamp01(t - m_ScaleSpeed * m_ScaleSpeedModifier);
                m_Transform.localScale = Vector3.Lerp(m_OriginalScale, m_MaxScaleVect, t);
            }
        }

        private void Update() {
            if (m_Rotate) {
                m_Transform.Rotate(0.0f, Time.deltaTime * m_RotationSpeed, 0.0f);
            }
        }

        private void OnHoverEntered(HoverEnterEventArgs args) {
            m_TrackpadAction.performed += OnScale;
        }

        private void OnHoverExited(HoverExitEventArgs args) {
            m_TrackpadAction.performed -= OnScale;
        }
    }
}
