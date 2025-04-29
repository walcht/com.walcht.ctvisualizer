using TMPro;
using UnityEngine;

namespace UnityCTVisualizer
{
    public class DatasetMetadataUI : MonoBehaviour
    {
        //////////////////////////////
        /// TMP InputFields
        //////////////////////////////
        [SerializeField] TMP_InputField m_OriginalVolumeWidth;
        [SerializeField] TMP_InputField m_OriginalVolumeHeight;
        [SerializeField] TMP_InputField m_OriginalVolumeDepth;
        [SerializeField] TMP_InputField m_VDHMeasure;
        [SerializeField] TMP_InputField m_ChunkSize;
        [SerializeField] TMP_InputField m_ColorDepth;
        [SerializeField] TMP_InputField m_VolumetricObjectWidth;
        [SerializeField] TMP_InputField m_VolumetricObjectHeight;
        [SerializeField] TMP_InputField m_VolumetricObjectDepth;
        [SerializeField] TMP_InputField m_LZ4Compressed;
        [SerializeField] TMP_InputField m_DecompressedSize;


        void Awake()
        {
            m_OriginalVolumeWidth.readOnly = true;
            m_OriginalVolumeHeight.readOnly = true;
            m_OriginalVolumeDepth.readOnly = true;
            m_VDHMeasure.readOnly = true;
            m_ChunkSize.readOnly = true;
            m_ColorDepth.readOnly = true;
            m_VolumetricObjectWidth.readOnly = true;
            m_VolumetricObjectHeight.readOnly = true;
            m_VolumetricObjectDepth.readOnly = true;
            m_LZ4Compressed.readOnly = true;
            m_DecompressedSize.readOnly = true;
        }

        /// <summary>
        ///     Initializes read-only input fields from a given UVDS dataset's metadata.
        ///     Call this before enabling this UI or during its lifetime to update it.
        /// </summary>
        ///
        /// <param name="metadata">
        ///     An imported UVDS dataset's metadata
        /// </param>
        public void Init(CVDSMetadata metadata)
        {
            m_OriginalVolumeWidth.text = metadata.Dims.x.ToString();
            m_OriginalVolumeHeight.text = metadata.Dims.y.ToString();
            m_OriginalVolumeDepth.text = metadata.Dims.z.ToString();
            m_VDHMeasure.text = "-";
            m_ChunkSize.text = metadata.ChunkSize.ToString();
            m_ColorDepth.text = $"{metadata.ColorDepth}";
            m_VolumetricObjectWidth.text = (metadata.VoxelDims.x * metadata.Dims.x).ToString("0.00");
            m_VolumetricObjectHeight.text = (metadata.VoxelDims.y * metadata.Dims.x).ToString("0.00");
            m_VolumetricObjectDepth.text = (metadata.VoxelDims.z * metadata.Dims.x).ToString("0.00");
            m_LZ4Compressed.text = metadata.Lz4Compressed ? "true" : "false";
            m_DecompressedSize.text = ((metadata.Dims.x * metadata.Dims.y * metadata.Dims.z) / (1024.0f * 1024.0f)).ToString("0.00");
        }
    }
}
