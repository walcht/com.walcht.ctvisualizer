using TMPro;
using UnityEngine;

namespace UnityCTVisualizer
{
    public class DatasetMetadataUI : MonoBehaviour
    {
        //////////////////////////////
        /// TMP InputFields
        //////////////////////////////
        [SerializeField] TMP_InputField m_OriginalVolumeDimensions;
        [SerializeField] TMP_InputField m_ChunkSize;
        [SerializeField] TMP_InputField m_ColorDepth;
        [SerializeField] TMP_InputField m_LZ4Compressed;
        [SerializeField] TMP_InputField m_DecompressedSize;
        [SerializeField] TMP_InputField m_InterBrickInterpolation;
        [SerializeField] TMP_InputField m_NbrResolutionLvls;
        [SerializeField] TMP_InputField m_OctreeSmallestSubdivision;
        [SerializeField] TMP_InputField m_ConvertedTo8bpp;


        void Awake()
        {
            m_OriginalVolumeDimensions.readOnly = true;
            m_ChunkSize.readOnly = true;
            m_ColorDepth.readOnly = true;
            m_LZ4Compressed.readOnly = true;
            m_DecompressedSize.readOnly = true;
            m_InterBrickInterpolation.readOnly = true;
            m_NbrResolutionLvls.readOnly = true;
            m_OctreeSmallestSubdivision.readOnly = true;
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
            m_OriginalVolumeDimensions.text = $"{metadata.Dims.x}x{ metadata.Dims.y}x{metadata.Dims.z}";
            m_ChunkSize.text = metadata.ChunkSize.ToString();
            m_ColorDepth.text = $"{metadata.ColorDepth}";
            m_LZ4Compressed.text = metadata.Lz4Compressed ? "true" : "false";
            m_DecompressedSize.text = ((metadata.Dims.x / 1024.0) * (metadata.Dims.y / 1024.0) * metadata.Dims.z).ToString("0.00");
            m_InterBrickInterpolation.text = metadata.ChunkPadding ? "true" : "false";
            m_NbrResolutionLvls.text = metadata.NbrResolutionLvls.ToString();
            m_OctreeSmallestSubdivision.text = metadata.OctreeSmallestSubdivision.ToString();
            m_ConvertedTo8bpp.text = metadata.ConvertedToUInt8 ? "true" : "false";
        }
    }
}
