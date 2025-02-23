using UnityEngine;


namespace UnityCTVisualizer {
  public class LogSystemInfo : MonoBehaviour {
    void Start() {
      Debug.Log($"GPU device: {SystemInfo.graphicsDeviceVendor} - {SystemInfo.graphicsDeviceName}");
      Debug.Log($"available GPU memory (VRAM): {SystemInfo.graphicsMemorySize}MB");
      Debug.Log($"CPU device: {SystemInfo.processorType}");
      Debug.Log($"CPU number of hardware threads: {SystemInfo.processorCount}");
      Debug.Log($"available system memory (RAM): {SystemInfo.systemMemorySize}MB");
    }
  }
}
