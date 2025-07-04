using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Transform))]
public class VolumetricObjectController : MonoBehaviour
{

    private Transform m_Transform;

    [SerializeField] private InputActionReference m_LeftHandPinchValueActionRef;
    [SerializeField] private InputActionReference m_LeftHandPinchReadyActionRef;

    [SerializeField] private InputActionReference m_LeftHandGraspReadyActionRef;
    [SerializeField] private InputActionReference m_LeftHandRotationActionRef;

    [Tooltip("Minimum volumetric object scale multiplier value")]
    public float MinScale = 0.2f;

    [Tooltip("Maximum volumetric object scale multiplier value")]
    public float MaxScale = 5.0f;

    [Tooltip("Scaling multiplier speed in 1/s")]
    public float ScalingSpeed = 10.0f;

    [Tooltip("Rotation speed in degrees/s")]
    public float RotationSpeed = 5.0f;

    private float m_CurrScale = 1.0f;
    private float m_PrevPinchVal = 0.0f;
    private Vector3 m_PrevRotation = Vector3.zero;

    void Awake()
    {
        m_Transform = GetComponent<Transform>();
    }


    void Update()
    {
        if (m_LeftHandPinchReadyActionRef.action.IsPressed())
        {
            var val = m_LeftHandPinchValueActionRef.action.ReadValue<float>();
            m_CurrScale = Mathf.Clamp(m_CurrScale + Mathf.Sign(val - m_PrevPinchVal) * ScalingSpeed * Time.deltaTime,
                MinScale, MaxScale);
            m_Transform.localScale *= m_CurrScale;
            m_PrevPinchVal = val;
        }

        if (m_LeftHandRotationActionRef.action.IsPressed())
        {
            var val = m_LeftHandRotationActionRef.action.ReadValue<Quaternion>();
            Vector3 diff = RotationSpeed * Time.deltaTime * (val.eulerAngles - m_PrevRotation);
            m_Transform.Rotate(diff, Space.Self);
            m_PrevRotation = val.eulerAngles;
        }
    }
}
