using UnityEngine;

namespace UnityCTVisualizer
{
    public class  SystemInfoStats
    {
        public readonly int GPUMemorySize;
        public readonly int CPUMemorySize;
        public readonly string DeviceModel;
        public readonly string GPUModel;
        public readonly string CPUModel;
        public readonly int MaxUAVs;
        public readonly bool Compressed3DTextureSupport;
        public readonly BatteryStatus BatteryStatus;
        public readonly float BatteryLevel;
        // only on Unity >= 6000.1
        public readonly bool VRSSupport = false;

        public SystemInfoStats()
        {
            CPUMemorySize = SystemInfo.systemMemorySize;
            GPUMemorySize = SystemInfo.graphicsMemorySize;
            DeviceModel = SystemInfo.deviceModel;
            GPUModel = SystemInfo.graphicsDeviceName;
            CPUModel = SystemInfo.processorType;
            MaxUAVs = SystemInfo.supportedRandomWriteTargetCount;
            Compressed3DTextureSupport = SystemInfo.supportsCompressed3DTextures;
            BatteryStatus = SystemInfo.batteryStatus;
            BatteryLevel = SystemInfo.batteryLevel;
        }
    }
}
