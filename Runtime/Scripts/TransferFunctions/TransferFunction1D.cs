using System.Collections.Generic;
using System.IO;
using UnityEngine;
using Newtonsoft.Json;
using System;
using System.Linq;

namespace UnityCTVisualizer
{
    public class TransferFunction1D : ITransferFunction
    {
        public static readonly int MAX_COLOR_CONTROL_POINTS = 16;
        public static readonly int MAX_ALPHA_CONTROL_POINTS = 16;

        private readonly Dictionary<int, ControlPoint<float, Color>> m_ColorControls = new(MAX_COLOR_CONTROL_POINTS);
        private readonly Dictionary<int, ControlPoint<float, float>> m_AlphaControls = new(MAX_ALPHA_CONTROL_POINTS);
        private readonly List<ControlPoint<float, Color>> m_SortedColors = new(MAX_COLOR_CONTROL_POINTS);
        private readonly List<ControlPoint<float, float>> m_SortedAlphas = new(MAX_ALPHA_CONTROL_POINTS);

        private int m_color_control_points_id_accum = 0;
        private int m_alpha_control_points_id_accum = 0;

        internal class TransferFunctionData
        {
            // public int TextureWidth;
            // public int TextureHeight;
            public float[] ColorControlPositions;
            public float[] AlphaControlPositions;
            public float[][] ColorControlValues;
            public float[] AlphaControlValues;
        }


        public TransferFunction1D() : base(256, 1)
        {
            TransferFunctionEvents.ViewTF1DColorControlAddition += OnViewTF1DColorControlAddition;
            TransferFunctionEvents.ViewTF1DColorControlRemoval += OnViewTF1DColorControlRemoval;
            TransferFunctionEvents.ViewTF1DAlphaControlAddition += OnViewTF1DAlphaControlAddition;
            TransferFunctionEvents.ViewTF1DAlphaControlRemoval += OnViewTF1DAlphaControlRemoval;

            // default color control points - default ramp function
            AddColorControlPoint(new(0.0f, Color.black));
            AddColorControlPoint(new(0.20f, Color.red));
            AddColorControlPoint(new(1.00f, Color.white));

            // default alpha control points - default ramp function
            AddAlphaControlPoint(new(0.0f, 0.0f));
            AddAlphaControlPoint(new(0.20f, 0.0f));
            AddAlphaControlPoint(new(0.80f, 0.9f));
            AddAlphaControlPoint(new(1.00f, 0.9f));
        }


        ~TransferFunction1D()
        {
            TransferFunctionEvents.ViewTF1DColorControlAddition -= OnViewTF1DColorControlAddition;
            TransferFunctionEvents.ViewTF1DColorControlRemoval -= OnViewTF1DColorControlRemoval;
            TransferFunctionEvents.ViewTF1DAlphaControlAddition -= OnViewTF1DAlphaControlAddition;
            TransferFunctionEvents.ViewTF1DAlphaControlRemoval -= OnViewTF1DAlphaControlRemoval;
        }


        ///////////////////////////////////////////////////////////////////////
        /// GETTERS
        ///////////////////////////////////////////////////////////////////////

        public IEnumerable<int> GetColorControlPointIDs() => m_ColorControls.Keys;

        public IEnumerable<int> GetAlphaControlPointIDs() => m_AlphaControls.Keys;


        /// <summary>
        ///     Gets the color control point by its unique ID.
        /// </summary>
        /// 
        /// <param name="cpID">
        ///     unique ID of the color control point.
        /// </param>
        /// 
        /// <returns>
        ///     color control point
        /// </returns>
        public bool TryGetColorControlPoint(int cpID, out ControlPoint<float, Color> cp) => m_ColorControls.TryGetValue(cpID, out cp);

        /// <summary>
        ///     Gets the alpha control point by its unique ID.
        /// </summary>
        /// 
        /// <param name="cpID">
        ///     unique ID of the alpha control point.
        /// </param>
        /// 
        /// <returns>
        ///     alpha control point
        /// </returns>
        public bool TryGetAlphaControlPoint(int cpID, out ControlPoint<float, float> cp) => m_AlphaControls.TryGetValue(cpID, out cp);


        ///////////////////////////////////////////////////////////////////////
        /// MODIFIERS
        ///////////////////////////////////////////////////////////////////////

        /// <summary>
        ///     Add a new color control point from which intermediate colors (no alpha) are interpolated.
        ///     For adding alpha colors use <seealso cref="AddAlphaControlPoint">AddAlphaControlPoint</seealso>
        /// </summary>
        /// 
        /// <remarks>
        ///     Even internally, it is preferable to use this function to add new color control points.
        /// </remarks>
        ///
        /// <param name="color_cp">color control point data (position and color)</param>
        /// 
        /// <returns>
        ///     unique ID of the newly added color control point. Useful for identification purposes.
        /// </returns>
        private int AddColorControlPoint(ControlPoint<float, Color> color_cp)
        {
            color_cp.OnValueChange += () => m_Dirty = true;
            m_ColorControls.Add(m_color_control_points_id_accum, color_cp);
            TransferFunctionEvents.ModelTF1DColorControlAddition?.Invoke(m_color_control_points_id_accum, color_cp);
            m_Dirty = true;
            return m_color_control_points_id_accum++;
        }

        /// <summary>
        ///     Adds a new alpha control point from which intermediate alphas are interpolated.
        ///     For adding colors (no alpha) use <seealso cref="AddColorControlPoint(ControlPoint{float, Color})">AddColorControlPoint</seealso>
        /// </summary>
        /// 
        /// <remarks>
        ///     Even internally, it is preferable to use this function to add new color control points.
        /// </remarks>
        /// 
        /// <param name="alpha_cp">
        ///     alpha control point data (position and alpha)
        /// </param>
        /// 
        /// <returns>
        ///     unique ID of the newly added alpha control point. Useful for identification purposes.
        /// </returns>
        private int AddAlphaControlPoint(ControlPoint<float, float> alpha_cp)
        {
            alpha_cp.OnValueChange += () => m_Dirty = true;
            m_AlphaControls.Add(m_alpha_control_points_id_accum, alpha_cp);
            TransferFunctionEvents.ModelTF1DAlphaControlAddition?.Invoke(m_alpha_control_points_id_accum, alpha_cp);
            m_Dirty = true;
            return m_alpha_control_points_id_accum++;
        }

        /// <summary>
        ///     Removes the identified color control point
        /// </summary>
        /// 
        /// <param name="cp_id">
        ///     unique ID of the color control point to be removed
        /// </param>
        private void RemoveColorControlPoint(int cp_id, bool createDefaultIfEmpty = true)
        {
            TransferFunctionEvents.ModelTF1DColorControlRemoval?.Invoke(cp_id);
            m_ColorControls.Remove(cp_id);

            // we want to leave at lease one default color control point
            if (createDefaultIfEmpty && m_ColorControls.Count == 0)
            {
                AddColorControlPoint(new(0.5f, Color.white));
            }

            m_Dirty = true;
        }

        /// <summary>
        ///     Removes the alpha color control point by unique ID
        /// </summary>
        ///
        /// <param name="cpID">
        ///     unique ID of the alpha control point to be remove
        /// </param>
        private void RemoveAlphaControlPoint(int cpID, bool createDefaultIfEmpty = true)
        {
            TransferFunctionEvents.ModelTF1DAlphaControlRemoval?.Invoke(cpID);
            m_AlphaControls.Remove(cpID);

            // we want to leave at least one default alpha control point
            if (createDefaultIfEmpty && m_AlphaControls.Count == 0)
            {
                AddAlphaControlPoint(new(0.5f, 0.2f));
            }

            m_Dirty = true;
        }


        /// <summary>
        ///     Removes all color control points. Since at least one color control point is needed for TF texture
        ///     generation, a white color control point is added at position 0.5.
        /// </summary>
        private void ClearColorControlPoints(bool createDefaultCp = true)
        {
            var keys = m_ColorControls.Keys.ToList();
            foreach (int k in keys)
            {
                RemoveColorControlPoint(k, createDefaultIfEmpty: createDefaultCp);
            }

            // just to be sure
            m_Dirty = true;
        }


        /// <summary>
        ///     Removes all alpha control points. Since at least one alpha control point is needed for TF texture
        ///     generation, an alpha control point of 0.2 is added at position 0.5.
        /// </summary>
        private void ClearAlphaControlPoints(bool createDefaultCp = true)
        {
            var keys = m_AlphaControls.Keys.ToList();
            foreach (int k in keys)
            {
                RemoveAlphaControlPoint(k, createDefaultIfEmpty: createDefaultCp);
            }

            // just to be sure
            m_Dirty = true;
        }


        protected override void GenerateColorLookupTexData()
        {
            if (m_ColorControls.Count == 0)
            {
                Debug.LogError("Color control points array is empty. Aborted TF generation.");
                return;
            }
            if (m_AlphaControls.Count == 0)
            {
                Debug.LogError("Alpha control points array is empty. Aborted TF generation.");
                return;
            }

            m_SortedColors.Clear();
            m_SortedAlphas.Clear();

            m_SortedColors.AddRange(m_ColorControls.Values);
            m_SortedAlphas.AddRange(m_AlphaControls.Values);

            m_SortedColors.Sort((x, y) => x.Position.CompareTo(y.Position));
            m_SortedAlphas.Sort((x, y) => x.Position.CompareTo(y.Position));

            // add same color as first color at position 0 if no color is set at that position
            if (m_SortedColors[0].Position > 0)
            {
                var tmp = m_SortedColors[0].Value;
                m_SortedColors.Insert(0, new(0, tmp));
            }

            // add same color as last color at position 1 if no color is set at that position
            if (m_SortedColors[^1].Position < 1)
            {
                var tmp = m_SortedColors[^1].Value;
                m_SortedColors.Add(new(1, tmp));
            }

            // add same alpha as first alpha at position 0 if no alpha is set at that position
            if (m_SortedAlphas[0].Position > 0)
            {
                var tmp = m_SortedAlphas[0].Value;
                m_SortedAlphas.Insert(0, new(0, tmp));
            }

            // add same alpha as last alpha at position 1 if no alpha is set at that position
            if (m_SortedAlphas[^1].Position < 1)
            {
                var tmp = m_SortedAlphas[^1].Value;
                m_SortedAlphas.Add(new(1, tmp));
            }

            int leftColorControlIndex = 0;
            int leftAlphaControlIndex = 0;

            int numOfColors = m_SortedColors.Count;
            int numOfAlphas = m_SortedAlphas.Count;

            for (int i = 0; i < m_RawTexData.Length; i += 4)
            {
                // map [0, tex.width] to [0.0, 1.0]
                float density = (i / 4) / (float)(m_ColorLookupTex.width - 1);

                // find nearest left color control point to density
                while (
                    leftColorControlIndex < numOfColors - 2
                    && m_SortedColors[leftColorControlIndex + 1].Position < density
                )
                {
                    leftColorControlIndex++;
                }

                // find nearest left alpha control point to density
                while (
                    leftAlphaControlIndex < numOfAlphas - 2
                    && m_SortedAlphas[leftAlphaControlIndex + 1].Position < density
                )
                {
                    leftAlphaControlIndex++;
                }

                var leftColor = m_SortedColors[leftColorControlIndex];
                var rightColor = m_SortedColors[leftColorControlIndex + 1];
                var leftAlpha = m_SortedAlphas[leftAlphaControlIndex];
                var rightAlpha = m_SortedAlphas[leftAlphaControlIndex + 1];

                float tColor = (density - leftColor.Position) / (rightColor.Position - leftColor.Position);
                float tAlpha = (density - leftAlpha.Position) / (rightAlpha.Position - leftAlpha.Position);

                // color (without alpha) linear interpolation
                Color pixelColor = Color.Lerp(leftColor.Value, rightColor.Value, tColor);

                // alpha linear interpolation
                pixelColor.a = Mathf.Lerp(leftAlpha.Value, rightAlpha.Value, tAlpha);

                if (QualitySettings.activeColorSpace == ColorSpace.Linear)
                {
                    Color c = pixelColor.linear;
                    m_RawTexData[i] = (byte)(c.r * 255);
                    m_RawTexData[i + 1] = (byte)(c.g * 255);
                    m_RawTexData[i + 2] = (byte)(c.b * 255);
                    m_RawTexData[i + 3] = (byte)(c.a * 255);
                }
                else
                {
                    m_RawTexData[i] = (byte)(pixelColor.r * 255);
                    m_RawTexData[i + 1] = (byte)(pixelColor.g * 255);
                    m_RawTexData[i + 2] = (byte)(pixelColor.b * 255);
                    m_RawTexData[i + 3] = (byte)(pixelColor.a * 255);
                }
            }
        }


        public override void Serialize(string fp)
        {
            if (File.Exists(fp))
            {
                throw new Exception($"{Path.GetFileName(fp)} already exists in {Path.GetDirectoryName(fp)}");
            }

            int[] colorControlIDs = new int[m_ColorControls.Count];
            float[] colorControlPositions = new float[m_ColorControls.Count];
            float[][] colorControlValues = new float[m_ColorControls.Count][];
            int i = 0;
            foreach (var cp in m_ColorControls)
            {
                colorControlIDs[i] = cp.Key;
                colorControlPositions[i] = cp.Value.Position;
                colorControlValues[i] = new float[] {cp.Value.Value.r, cp.Value.Value.g, cp.Value.Value.b,
                    cp.Value.Value.a };
                ++i;
            }

            int[] alphaControlIDs = new int[m_AlphaControls.Count];
            float[] alphaControlPositions = new float[m_AlphaControls.Count];
            float[] alphaControlValues = new float[m_AlphaControls.Count];
            i = 0;
            foreach (var cp in m_AlphaControls)
            {
                alphaControlIDs[i] = cp.Key;
                alphaControlPositions[i] = cp.Value.Position;
                alphaControlValues[i] = cp.Value.Value;
                ++i;
            }

            var data = new TransferFunctionData()
            {
                ColorControlPositions = colorControlPositions,
                AlphaControlPositions = alphaControlPositions,
                ColorControlValues = colorControlValues,
                AlphaControlValues = alphaControlValues,
            };

            using (StreamWriter sw = File.CreateText(fp))
            {
                sw.Write(JsonConvert.SerializeObject(data));
            }
        }


        public override void Deserialize(string fp)
        {
            if (!File.Exists(fp))
            {
                throw new Exception($"{Path.GetFileName(fp)} does NOT exist in {Path.GetDirectoryName(fp)}");
            }

            // clear previous control points
            ClearColorControlPoints(createDefaultCp: false);
            ClearAlphaControlPoints(createDefaultCp: false);

            m_color_control_points_id_accum = 0;
            m_alpha_control_points_id_accum = 0;

            var data = JsonConvert.DeserializeObject<TransferFunctionData>(File.ReadAllText(fp));
            for (int i = 0; i < data.ColorControlPositions.Length; ++i)
            {
                var col = new Color(data.ColorControlValues[i][0], data.ColorControlValues[i][1],
                    data.ColorControlValues[i][2], data.ColorControlValues[i][3]);
                AddColorControlPoint(new ControlPoint<float, Color>(data.ColorControlPositions[i], col));
            }

            for (int i = 0; i < data.AlphaControlPositions.Length; ++i)
            {
                AddAlphaControlPoint(new ControlPoint<float, float>(data.AlphaControlPositions[i],
                    data.AlphaControlValues[i]));
            }

            m_Dirty = true;
        }


        private void OnViewTF1DColorControlAddition(ControlPoint<float, Color> cp) => AddColorControlPoint(cp);


        private void OnViewTF1DColorControlRemoval(int cpID) => RemoveColorControlPoint(cpID);


        private void OnViewTF1DAlphaControlAddition(ControlPoint<float, float> cp) => AddAlphaControlPoint(cp);


        private void OnViewTF1DAlphaControlRemoval(int cpID) => RemoveAlphaControlPoint(cpID);

    }

}
