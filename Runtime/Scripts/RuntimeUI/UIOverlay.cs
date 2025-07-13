using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform), typeof(Graphic))]
    public class UIOverlay : MonoBehaviour
    {
        [SerializeField] private int overlayRenderQueueOffset = 0;

        private void Awake()
        {
            var graphic = GetComponent<Graphic>();
            var copyMat = new Material(graphic.material);
            copyMat.SetInt("unity_GUIZTestMode", (int)UnityEngine.Rendering.CompareFunction.Always);
            copyMat.renderQueue = (int)UnityEngine.Rendering.RenderQueue.Overlay + overlayRenderQueueOffset;
            graphic.material = copyMat;
        }
    }
}
