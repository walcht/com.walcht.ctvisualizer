using UnityEngine;
using UnityEngine.InputSystem;

namespace UnityCTVisualizer {
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
        MagicLeapInputs m_InputLayer;
        MagicLeapInputs.ControllerActions m_ControllerActions;

        float m_ScaleSpeedModifier = 1;
        Vector3 m_OriginalScale;
        Vector3 m_MaxScaleVect;

        void Awake() {
            m_Transform = GetComponent<Transform>();
            m_InputLayer = new MagicLeapInputs();

            m_ControllerActions = new MagicLeapInputs.ControllerActions(m_InputLayer);

            m_ControllerActions.TouchpadPosition.performed += OnScale;
            
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
            float scroll = context.ReadValue<float>();
            if (scroll > 0) {
                t = Mathf.Clamp01(t + m_ScaleSpeed * m_ScaleSpeedModifier);
                m_Transform.localScale = Vector3.Lerp(m_OriginalScale, m_MaxScaleVect, t);
            }
            // this has to be done because on linux we get 120, 0, -120
            else if (scroll < 0) {
                t = Mathf.Clamp01(t - m_ScaleSpeed * m_ScaleSpeedModifier);
                m_Transform.localScale = Vector3.Lerp(m_OriginalScale, m_MaxScaleVect, t);
            }
        }

        private void Update() {
            if (m_Rotate) {
                m_Transform.Rotate(0.0f, Time.deltaTime * m_RotationSpeed, 0.0f);
            }
        }

        void OnEnable() {
            m_InputLayer.Enable();
        }

        void OnDisable() {
            m_InputLayer.Enable();
        }
    }
}
