﻿#define IN_CORE

using System;
using System.Collections.Generic;
using UnityEngine;

namespace UnityCTVisualizer
{

    // TODO: remove or clean this up. This is not needed.

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
        // VISUALIZATION PARAMETERS
        /////////////////////////////////
        private float m_AlphaCutoff = 254.0f / 255.0f;
        public float OpacityCutoff
        {
            get => m_AlphaCutoff; set
            {
                m_AlphaCutoff = Mathf.Clamp01(value);
                VisualizationParametersEvents.ModelOpacityCutoffChange?.Invoke(m_AlphaCutoff);
            }
        }

        private float m_SamplingQualityFactor = 1.0f;
        public float SamplingQualityFactor
        {
            get => m_SamplingQualityFactor; set
            {
                m_SamplingQualityFactor = Mathf.Clamp(value, 0.5f, 3.0f);
                VisualizationParametersEvents.ModelSamplingQualityFactorChange?.Invoke(m_SamplingQualityFactor);
            }
        }

        private List<float> m_LODDistances = new();
        public List<float> LODDistances
        {
            get => m_LODDistances; set
            {
                m_LODDistances = value;
                VisualizationParametersEvents.ModelLODDistancesChange?.Invoke(m_LODDistances);
            }
        }


        private byte m_HomogeneityTolerance = 0;
        public byte HomogeneityTolerance
        {
            get => m_HomogeneityTolerance;
            set
            {
                m_HomogeneityTolerance = value;
                VisualizationParametersEvents.ModelHomogeneityToleranceChange?.Invoke(m_HomogeneityTolerance);
            }
        }


        private INTERPOLATION m_Interpolation = INTERPOLATION.TRILLINEAR;
        public INTERPOLATION InterpolationMethod
        {
            get => m_Interpolation; set
            {
                m_Interpolation = value;
                VisualizationParametersEvents.ModelInterpolationChange?.Invoke(m_Interpolation);
            }
        }

        private TF m_CurrentTF = TF.TF1D;
        private Dictionary<TF, ITransferFunction> m_TransferFunctions;
        public TF TransferFunction
        {
            set
            {
                m_CurrentTF = value;
                if (!m_TransferFunctions.TryGetValue(m_CurrentTF, out ITransferFunction tf))
                {
                    tf = TransferFunctionFactory.Create(m_CurrentTF);
                    m_TransferFunctions.Add(m_CurrentTF, tf);
                }
                VisualizationParametersEvents.ModelTFChange?.Invoke(m_CurrentTF, tf);
            }
        }

        public void DispatchVisualizationParamsChangeEvents()
        {
            if (m_TransferFunctions.ContainsKey(m_CurrentTF))
                VisualizationParametersEvents.ModelTFChange?.Invoke(m_CurrentTF, m_TransferFunctions[m_CurrentTF]);
            VisualizationParametersEvents.ModelOpacityCutoffChange?.Invoke(m_AlphaCutoff);
            VisualizationParametersEvents.ModelInterpolationChange?.Invoke(m_Interpolation);
            VisualizationParametersEvents.ModelSamplingQualityFactorChange?.Invoke(m_SamplingQualityFactor);
            VisualizationParametersEvents.ModelLODDistancesChange?.Invoke(m_LODDistances);
            VisualizationParametersEvents.ModelHomogeneityToleranceChange?.Invoke(m_HomogeneityTolerance);
        }

        /////////////////////////////////
        // PARAMETERS
        /////////////////////////////////
        private CVDSMetadata m_metadata;
        public CVDSMetadata Metadata { get => m_metadata; }

        public void Init(CVDSMetadata metadata)
        {
            m_metadata = metadata;
            List<float> lod_distances = new();
            float acc = 0.5f;
            for (int i = 0; i < m_metadata.NbrResolutionLvls; ++i)
            {
                lod_distances.Add(acc);
                acc += 0.5f;
            }
            LODDistances = lod_distances;
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
            VisualizationParametersEvents.ViewLODDistancesChange += OnViewLODDistancesChange;
            VisualizationParametersEvents.ViewInterpolationChange += OnViewInterpolationChange;
            VisualizationParametersEvents.ViewHomogeneityToleranceChange += OnViewHomogeneityToleranceChange;
        }

        private void OnDisable()
        {
            VisualizationParametersEvents.ViewTFChange -= OnViewTFChange;
            VisualizationParametersEvents.ViewAlphaCutoffChange -= OnViewAlphaCutoffChange;
            VisualizationParametersEvents.ViewSamplingQualityFactorChange -= OnViewSamplingQualityFactorChange;
            VisualizationParametersEvents.ViewLODDistancesChange -= OnViewLODDistancesChange;
            VisualizationParametersEvents.ViewInterpolationChange -= OnViewInterpolationChange;
            VisualizationParametersEvents.ViewHomogeneityToleranceChange -= OnViewHomogeneityToleranceChange;
        }

        private void OnViewAlphaCutoffChange(float val) => OpacityCutoff = val;


        private void OnViewSamplingQualityFactorChange(float val) => SamplingQualityFactor = val;


        private void OnViewLODDistancesChange(List<float> distances) => LODDistances = distances;


        private void OnViewInterpolationChange(INTERPOLATION interpolation) => InterpolationMethod = interpolation;


        private void OnViewTFChange(TF new_tf) => TransferFunction = new_tf;


        private void OnViewHomogeneityToleranceChange(byte val) => HomogeneityTolerance = val;
    }
}
