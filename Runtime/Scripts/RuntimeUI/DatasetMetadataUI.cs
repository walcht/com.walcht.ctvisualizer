using TMPro;
using UnityEngine;

namespace UnityCTVisualizer {
    public class DatasetMetadataUI : MonoBehaviour {
        //////////////////////////////
        /// TMP InputFields
        //////////////////////////////
        [SerializeField] TMP_InputField m_OriginalVolumeWidth;
        [SerializeField] TMP_InputField m_OriginalVolumeHeight;
        [SerializeField] TMP_InputField m_OriginalNumberSlices;
        [SerializeField] TMP_InputField m_ChunkSize;
        [SerializeField] TMP_InputField m_ColorDepth;
        [SerializeField] TMP_InputField m_VoxelDimX;
        [SerializeField] TMP_InputField m_VoxelDimY;
        [SerializeField] TMP_InputField m_VoxelDimZ;

        //////////////////////////////
        /// TMP Labels
        //////////////////////////////
        [SerializeField] TMP_Text m_OriginalVolumeWidthLabel;
        [SerializeField] TMP_Text m_OriginalVolumeHeightLabel;
        [SerializeField] TMP_Text m_OriginalNumberSlicesLabel;
        [SerializeField] TMP_Text m_ChunkSizeLabel;
        [SerializeField] TMP_Text m_ColorDepthLabel;
        [SerializeField] TMP_Text m_VoxelDimXLabel;
        [SerializeField] TMP_Text m_VoxelDimYLabel;
        [SerializeField] TMP_Text m_VoxelDimZLabel;

        void Awake() {
            m_OriginalVolumeWidth.readOnly = true;
            m_OriginalVolumeHeight.readOnly = true;
            m_OriginalNumberSlices.readOnly = true;
            m_ChunkSize.readOnly = true;
            m_ColorDepth.readOnly = true;
            m_VoxelDimX.readOnly = true;
            m_VoxelDimY.readOnly = true;
            m_VoxelDimZ.readOnly = true;

            TMP_Text[] texts = {m_OriginalVolumeWidthLabel, m_OriginalVolumeHeightLabel, m_OriginalNumberSlicesLabel,
            m_ChunkSizeLabel, m_ColorDepthLabel, m_VoxelDimXLabel, m_VoxelDimYLabel, m_VoxelDimZLabel };
            float fontsize = ResizingUtils.GetOptimalFontSize(texts);
            foreach (TMP_Text t in texts)
                t.fontSize = fontsize;
        }

        /// <summary>
        ///     Initializes read-only input fields from a given UVDS dataset's metadata.
        ///     Call this before enabling this UI or during its lifetime to update it.
        /// </summary>
        ///
        /// <param name="metadata">
        ///     An imported UVDS dataset's metadata
        /// </param>
        public void Init(CVDSMetadata metadata) {
            m_OriginalVolumeWidth.text = metadata.Dims.x.ToString();
            m_OriginalVolumeHeight.text = metadata.Dims.y.ToString();
            m_OriginalNumberSlices.text = metadata.Dims.z.ToString();

            m_ChunkSize.text = metadata.ChunkSize.ToString();

            m_ColorDepth.text = $"{metadata.ColorDepth} BPP";

            m_VoxelDimX.text = metadata.VoxelDims.x.ToString("0.00");
            m_VoxelDimY.text = metadata.VoxelDims.y.ToString("0.00");
            m_VoxelDimZ.text = metadata.VoxelDims.z.ToString("0.00");
        }
    }
}
