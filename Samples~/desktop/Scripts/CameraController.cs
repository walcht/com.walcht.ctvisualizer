using UnityEngine;
using UnityEngine.InputSystem;

[RequireComponent(typeof(Transform))]
public class CameraController : MonoBehaviour
{
    [SerializeField] private float m_MovementSpeed = 1.0f;
    [SerializeField] private float m_RotationSpeed = 5.0f;
    [SerializeField] private InputActionReference m_MoveActionRef;
    [SerializeField] private InputActionReference m_LookActionRef;


    private Transform m_Transform;
    private InputAction m_MoveAction;
    private InputAction m_LookAction;

    private void Awake()
    {
        m_Transform = GetComponent<Transform>();
        m_MoveAction = m_MoveActionRef.action;
        m_LookAction = m_LookActionRef.action;
    }

    private void LateUpdate()
    {
        var moveVal = m_MoveAction.ReadValue<Vector2>();
        float d = m_MovementSpeed;
        var posDelta = moveVal.x * d * m_Transform.right + moveVal.y * d * m_Transform.forward;
        if (Mouse.current.rightButton.isPressed)
        {
            var lookVal = m_LookAction.ReadValue<Vector2>();
            float m = m_RotationSpeed;
            m_Transform.SetPositionAndRotation(m_Transform.position + posDelta, Quaternion.Euler(
                m_Transform.eulerAngles.x - lookVal.y * m / Screen.height, m_Transform.eulerAngles.y + lookVal.x * m / Screen.width, 0));
        }
        else
        {
            m_Transform.position += posDelta;
        }
    }
}
