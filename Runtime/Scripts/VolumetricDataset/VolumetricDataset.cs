#define IN_CORE

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCTVisualizer
{

    /// <summary>
    ///     Serializable wrapper around a volumetric dataset and its visualization parameters.
    /// </summary>
    ///
    /// <remarks>
    ///     Includes metadata about the volume dataset, its visualization parameters, and other configurable parameters.
    ///     Once tweaked for optimal performance/visual quality tradeoff, this can be saved (i.e., serialized) for later
    ///     use. The brick cache is not serialized (i.e., the Texture3D object) since it is visualization-driven and
    ///     usually is of very large size.
    /// </remarks>
    [CreateAssetMenu(
        fileName = "volumetric_dataset",
        menuName = "UnityCTVisualizer/VolumetricDataset"
    )]
    public class VolumetricDataset : ScriptableObject
    {

        /////////////////////////////////
        // CONSTANTS
        /////////////////////////////////
        public readonly long MIN_BRICK_SIZE = (long)Math.Pow(32, 3);
        public readonly long MAX_BRICK_SIZE = (long)Math.Pow(128, 3);
        public readonly long MAX_BRICKS_CACHE_NBR_BRICKS = 32768; // == 2048^3 / 32^3

        /////////////////////////////////
        // VISUALIZATION PARAMETERS
        /////////////////////////////////
        private float m_AlphaCutoff = 254.0f / 255.0f;
        public float AlphaCutoff
        {
            get => m_AlphaCutoff; set
            {
                m_AlphaCutoff = value;
                VisualizationParametersEvents.ModelAlphaCutoffChange?.Invoke(value);
            }
        }
        private float m_SamplingQualityFactor = 1.0f;
        public float SamplingQualityFactor
        {
            get => m_SamplingQualityFactor; set
            {
                m_SamplingQualityFactor = value;
                VisualizationParametersEvents.ModelSamplingQualityFactorChange?.Invoke(value);
            }
        }
        private INTERPOLATION m_Interpolation = INTERPOLATION.TRILLINEAR;
        public INTERPOLATION InterpolationMethode
        {
            get => m_Interpolation; set
            {
                m_Interpolation = value;
                VisualizationParametersEvents.ModelInterpolationChange?.Invoke(value);
            }
        }

        private TF m_CurrentTF = TF.TF1D;
        private Dictionary<TF, ITransferFunction> m_TransferFunctions;
        public TF TransferFunction
        {
            set
            {
                m_CurrentTF = value;
                ITransferFunction tf_so;
                if (!m_TransferFunctions.TryGetValue(value, out tf_so))
                {
                    tf_so = TransferFunctionFactory.Create(value);
                    m_TransferFunctions.Add(m_CurrentTF, tf_so);
                }
                VisualizationParametersEvents.ModelTFChange?.Invoke(m_CurrentTF, tf_so);
            }
        }

        public void DispatchVisualizationParamsChangeEvents()
        {
            VisualizationParametersEvents.ModelTFChange?.Invoke(m_CurrentTF, m_TransferFunctions[m_CurrentTF]);
            VisualizationParametersEvents.ModelAlphaCutoffChange?.Invoke(m_AlphaCutoff);
            VisualizationParametersEvents.ModelInterpolationChange?.Invoke(m_Interpolation);
            VisualizationParametersEvents.ModelSamplingQualityFactorChange?.Invoke(m_SamplingQualityFactor);
        }

        /////////////////////////////////
        // PARAMETERS
        /////////////////////////////////
        private CVDSMetadata m_metadata;
        public CVDSMetadata Metadata { get => m_metadata; }

        public void Init(CVDSMetadata metadata)
        {
            m_metadata = metadata;
        }

        /*
        *   It is assumed that whatever graphics API is used (be it Vulkan, OpenGL,
        *   or DirectX) the following coordinate system is used:
        *    
        *    
        *                     ORIGIN
        *                       ↓ 
        *                 c111 .X- - - .*-----------------------+ c110 ⟶ X
        *                    .' |    .' |                    .' |
        *                  .*- - - -*   |                  .'   |
        *                .' | BRICK |   |                .'     |
        *              .'   | ID=0  | .'               .'       |
        *            .'     |_ _ _ _|'               .'         |
        *          .'           |                  .'           |
        *    c011 +-------------------------------+ c010        |
        *       ↙ |             |                 |             |
        *      Z  |             |                 |             |
        *         |             |                 |             |
        *         |             |                 |             |
        *         |        c100 +-----------------|-------------+ c101
        *         |          .' ↓                 |           .'
        *         |        .'   Y            .*- -|- -*     .'
        *         |      .'                .'     | .'|   .'
        *         |    .'                 *- - - -*   | .'
        *         |  .'                   | BRICK |   |'
        *         |.'                     | ID=N  | .'
        *    c000 |_______________________|_ _ _ _|'c001
        *    
        *     
        *   Brick IDs increase from the top left along the X axis. Then downwards
        *   along the Y axis direction. It then loops back to the top left of the
        *   next brick slice along the Z axis direction.
        *
        */
        public void ComputeVolumeOffset(UInt32 brick_id, int brick_size, out Int32 x, out Int32 y, out Int32 z)
        {
            int id = (int)(brick_id & 0x03FFFFFF);
            int res_lvl = (int)(brick_id >> 26);
            // transition to Unity's Texture3D coordinate system
            int nbr_bricks_x = m_metadata.NbrChunksPerResolutionLvl[res_lvl].x * m_metadata.ChunkSize / brick_size;
            int nbr_bricks_y = m_metadata.NbrChunksPerResolutionLvl[res_lvl].y * m_metadata.ChunkSize / brick_size;
            x = brick_size * (id % nbr_bricks_x);
            y = brick_size * ((id / nbr_bricks_x) % nbr_bricks_y);
            z = brick_size * (id / (nbr_bricks_x * nbr_bricks_y));
        }

        private void OnEnable()
        {
            if (m_TransferFunctions == null)
            {
                m_TransferFunctions = new Dictionary<TF, ITransferFunction> { { TF.TF1D, TransferFunctionFactory.Create(TF.TF1D) } };
            }

            VisualizationParametersEvents.ViewTFChange += OnViewTFChange;
            VisualizationParametersEvents.ViewAlphaCutoffChange += OnViewAlphaCutoffChange;
            VisualizationParametersEvents.ViewSamplingQualityFactorChange += OnViewSamplingQualityFactorChange;
            VisualizationParametersEvents.ViewInterpolationChange += OnViewInterpolationChange;
        }

        private void OnDisable()
        {
            VisualizationParametersEvents.ViewTFChange -= OnViewTFChange;
            VisualizationParametersEvents.ViewAlphaCutoffChange -= OnViewAlphaCutoffChange;
            VisualizationParametersEvents.ViewSamplingQualityFactorChange -= OnViewSamplingQualityFactorChange;
            VisualizationParametersEvents.ViewInterpolationChange -= OnViewInterpolationChange;
        }

        private void OnViewAlphaCutoffChange(float alphaCutoff)
        {
            AlphaCutoff = Mathf.Clamp01(alphaCutoff);
        }

        private void OnViewSamplingQualityFactorChange(float val)
        {
            SamplingQualityFactor = val;
        }

        private void OnViewInterpolationChange(INTERPOLATION interpolation)
        {
            InterpolationMethode = interpolation;
        }

        private void OnViewTFChange(TF new_tf)
        {
            TransferFunction = new_tf;
        }
    }
}
