using System;
using System.Collections.Generic;
using System.Linq;
using UnityEngine;
using UnityEngine.UI;
using UnityEngine.EventSystems;
using UnityD3;
using System.Collections;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(RectTransform), typeof(Canvas))]
    public class TransferFunction1DUI : MonoBehaviour
    {
        /////////////////////////////////////////////////////////////////////////////////////////////////////
        ///////////////////////////////// IN-CURRENT or IN-CHILDREN REFERENCES //////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        [SerializeField] RawImage m_HistogramImage;
        [SerializeField] RectTransform m_HistogramTransform;
        [SerializeField] HistogramUI m_HistogramUI;
        [SerializeField] RectTransform m_ColorGradientControlRange;
        [SerializeField] RawImage m_GradientColorImage;
        [SerializeField] ColorGradientRangeUI m_GradientColorUI;

        [SerializeField] Button m_RemoveSelection;
        [SerializeField] Button m_ClearColors;
        [SerializeField] Button m_ClearAlphas;
        [SerializeField] Button m_ColorPicker;

        [SerializeField] Button m_Save;
        [SerializeField] Button m_Load;

        [SerializeField] ColorPickerWrapper m_ColorPickerWrapper;
        [SerializeField] RectTransform m_TF1DCanvasTransform;
        [SerializeField] RectTransform m_ColorPickerTransform;

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        /////////////////////////////////////////////// PREFABS /////////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        [Tooltip("Color control point UI prefab that will be instantiated by this TF UI in the color gradient "
            + "range UI once a new color control point is added.")]
        public GameObject m_ColorControlPointUIPrefab;

        [Tooltip("Alpha control point UI prefab that will be instantiated by this TF UI in the histogram child UI "
            + "once a new color control point is added.")]
        public GameObject m_AlphaControlPointUIPrefab;

        /////////////////////////////////////////////////////////////////////////////////////////////////////
        ////////////////////////////////////////// CACHED COMPONENTS ////////////////////////////////////////
        /////////////////////////////////////////////////////////////////////////////////////////////////////

        int ALPHA_COUNT_SHADER_ID = Shader.PropertyToID("_AlphaCount");
        int ALPHA_POSITIONS_SHADER_ID = Shader.PropertyToID("_AlphaPositions");
        int ALPHA_VALUES_SHADER_ID = Shader.PropertyToID("_AlphaValues");

        // -1 means None is selected which in turn means that we have no control points (empty list)
        int m_CurrColorControlPointID = -1;
        int m_CurrAlphaControlPointID = -1;

        TransferFunction1D m_TransferFunctionData = null;

        Dictionary<int, ColorControlPointUI> m_ColorControlPoints = new();
        Dictionary<int, AlphaControlPointUI> m_AlphaControlPoints = new();

        ///////////////////////////
        /// SCALES
        ///////////////////////////
        private IScaleContinuous<float, float> m_x_scale;
        private IScaleContinuous<float, float> m_y_scale;

        ///////////////////////////
        /// AXES
        ///////////////////////////
        private Axis<float> m_x_axis;
        private Axis<float> m_y_axis;

        ///////////////////////////
        /// MATERIALS
        ///////////////////////////
        public Material AxisMaterial;
        public Material TickMaterial;

        private TagHandle m_ColorAlphaControlsTag;


        private void Awake()
        {
            m_ColorAlphaControlsTag = TagHandle.GetExistingTag("ColorAlphaControls");
        }


        void OnEnable()
        {
            if (m_TransferFunctionData == null)
            {
                Debug.LogError("Init should be appropriately called before enabling this UI.");
                return;
            }

            m_RemoveSelection.onClick.AddListener(OnRemoveSelectedControlPoint);
            m_ClearColors.onClick.AddListener(OnClearColorsClick);
            m_ClearAlphas.onClick.AddListener(OnClearAlphasClick);
            m_ColorPicker.onClick.AddListener(OnColorPickerClick);

            m_HistogramUI.OnAddAlphaControlPoint += OnAddAlphaControlPoint;
            m_GradientColorUI.OnAddColorControlPoint += OnAddColorControlPoint;

            m_ColorPickerWrapper.gameObject.SetActive(false);
        }


        void OnDisable()
        {
            m_RemoveSelection.onClick.RemoveAllListeners();
            m_ClearColors.onClick.RemoveAllListeners();
            m_ClearAlphas.onClick.RemoveAllListeners();
            m_ColorPicker.onClick.RemoveAllListeners();

            m_HistogramUI.OnAddAlphaControlPoint -= OnAddAlphaControlPoint;
            m_GradientColorUI.OnAddColorControlPoint -= OnAddColorControlPoint;
            m_TransferFunctionData.TFColorsLookupTexChange -= OnTFTexChange;
        }


        public void Init(TransferFunction1D transferFunctionData)
        {
            m_TransferFunctionData = transferFunctionData;

            m_ColorControlPoints.Clear();

            // synchronize control points UI array with underlying transfer function data
            foreach (var colorCpID in m_TransferFunctionData.ColorControlPointIDs())
            {
                AddColorControlPointInternal(m_TransferFunctionData.GetColorControlPointAt(colorCpID), colorCpID);
            }
            foreach (var alphaCpID in m_TransferFunctionData.AlphaControlPointIDs())
            {
                AddAlphaControlPointInternal(m_TransferFunctionData.GetAlphaControlPointAt(alphaCpID), alphaCpID);
            }

            // select first element by default
            if (m_ColorControlPoints.Count > 0)
                UpdateCurrColorControlPointID(m_ColorControlPoints.Keys.First());
            else
                UpdateCurrColorControlPointID(-1);
            if (m_AlphaControlPoints.Count > 0)
                UpdateCurrAlphaControlPointID(m_AlphaControlPoints.Keys.First());
            else
                UpdateCurrAlphaControlPointID(-1);

            m_TransferFunctionData.TFColorsLookupTexChange += OnTFTexChange;
            m_TransferFunctionData.TryUpdateColorLookupTexture();

            // initialize histogram shader properties and request histogram texture
            OnAlphaControlPointDataChange();

            /*
            // create the x/y scales and axes
            m_x_scale = new ScaleLinearFloatFloat(0.0f, 1.0f, 0, 1);

            // create/update Y scale
            m_y_scale = new ScaleLinearFloatFloat(0.0f, 1.0f, 0, 1);

            m_x_axis = new AxisBottom<float>(m_x_scale, tick_count: 2)
                .SetAxisMaterial(AxisMaterial)
                .SetTickMaterial(TickMaterial)
                .SetTickTextColor(Color.black)
                .Attach(gameObject);

            m_y_axis = new AxisLeft<float>(m_y_scale, tick_count: 2)
                .SetAxisMaterial(AxisMaterial)
                .SetTickMaterial(TickMaterial)
                .SetTickTextColor(Color.black)
                .Attach(gameObject);
            */
        }

        public void SetHistogram(UInt64[] histogram)
        {
            int width = 256;
            Texture2D tex = new (width, 1, TextureFormat.R16, mipChain: false, linear: false, createUninitialized: true);

            tex.filterMode = FilterMode.Point;

            UInt16[] data = new UInt16[width];

            int r = histogram.Length / width;

            double total = 0;
            for (int i = 0; i < histogram.Length; ++i)
                total += (double)histogram[i];

            for (int i = 0; i < width; ++i)
            {
                double b = 0;
                for (int j = 0; j < r; ++j)
                {
                    b += histogram[i * r + j];
                }
                data[i] = (UInt16)Mathf.RoundToInt(65535 * (float)(b / total));
            }

            tex.SetPixelData(data, 0);
            tex.Apply();

            m_HistogramImage.texture = tex;
        }

        void AddColorControlPoint(ControlPoint<float, Color> cp)
        {
            int newCpID = m_TransferFunctionData.AddColorControlPoint(cp);
            AddColorControlPointInternal(cp, newCpID);
        }

        void AddColorControlPointInternal(ControlPoint<float, Color> cp, int cpID)
        {
            var newCp = Instantiate(
                    m_ColorControlPointUIPrefab,
                    parent: m_ColorGradientControlRange
                )
                .GetComponent<ColorControlPointUI>();
            newCp.Init(cpID, cp);
            newCp.ControlPointDeselected += OnColorControlPointDeselected;
            newCp.ControlPointSelected += OnColorControlPointSelected;
            newCp.ControlPointData.OnValueChange += OnColorControlPointDataChange;
            m_ColorControlPoints.Add(cpID, newCp);
        }

        void AddAlphaControlPoint(ControlPoint<float, float> cp)
        {
            int newCpID = m_TransferFunctionData.AddAlphaControlPoint(cp);
            AddAlphaControlPointInternal(cp, newCpID);
        }

        void AddAlphaControlPointInternal(ControlPoint<float, float> cp, int cpID)
        {
            var newCp = Instantiate(m_AlphaControlPointUIPrefab, parent: m_HistogramTransform)
                .GetComponent<AlphaControlPointUI>();
            newCp.Init(cpID, cp);
            newCp.ControlPointDeselected += OnAlphaControlPointDeselected;
            newCp.ControlPointSelected += OnAlphaControlPointSelect;
            newCp.ControlPointData.OnValueChange += OnAlphaControlPointDataChange;
            m_AlphaControlPoints.Add(cpID, newCp);
        }


        void UpdateCurrColorControlPointID(int newId)
        {
            if (newId < 0)
            {
                m_CurrColorControlPointID = -1;
                m_RemoveSelection.interactable = false;
                m_ColorPicker.interactable = false;
                return;
            }

            // make sure only one control point is selected at a given time
            UpdateCurrAlphaControlPointID(-1);

            m_CurrColorControlPointID = newId;
            m_RemoveSelection.interactable = true;
            m_ColorPicker.interactable = true;
        }


        void UpdateCurrAlphaControlPointID(int newId)
        {
            if (newId < 0)
            {
                m_CurrAlphaControlPointID = -1;
                m_RemoveSelection.interactable = false;
                return;
            }

            // make sure only one control point is selected at a given time
            UpdateCurrColorControlPointID(-1);

            m_CurrAlphaControlPointID = newId;
            m_RemoveSelection.interactable = true;
        }


        /////////////////////////////
        /// LISTENERS
        /////////////////////////////

        void OnRemoveSelectedControlPoint()
        {
            if (m_CurrAlphaControlPointID != -1 && m_CurrColorControlPointID != -1)
            {
                Debug.LogError("only one control point should be active at a given time!");
                return;
            }

            if (m_CurrAlphaControlPointID != -1)
            {
                var cpToRemove = m_AlphaControlPoints[m_CurrAlphaControlPointID];

                // remove it from alpha control points UI list
                m_AlphaControlPoints.Remove(m_CurrAlphaControlPointID);

                // destroy UI element (no need to unsubscribe)
                Destroy(cpToRemove.gameObject);

                if (m_AlphaControlPoints.Count == 0)
                {
                    m_TransferFunctionData.ClearAlphaControlPoints();

                    // ClearAlphaControlPoints adds new default alpha control point(s). We have to synchronize
                    foreach (var alphaCpID in m_TransferFunctionData.AlphaControlPointIDs())
                        AddAlphaControlPointInternal(m_TransferFunctionData.GetAlphaControlPointAt(alphaCpID), alphaCpID);
                }
                else
                {
                    // remove alpha from underlying Transfer function data
                    m_TransferFunctionData.RemoveAlphaControlPoint(m_CurrAlphaControlPointID);
                }

                // no alpha control point is currently selected
                UpdateCurrAlphaControlPointID(-1);

                // don't forget to request a transfer function texture update
                OnAlphaControlPointDataChange();

                return;
            }

            if (m_CurrColorControlPointID != -1)
            {
                var cpToRemove = m_ColorControlPoints[m_CurrColorControlPointID];

                // remove it from color control points UI list
                m_ColorControlPoints.Remove(m_CurrColorControlPointID);

                // destroy UI element (no need to unsubscribe)
                Destroy(cpToRemove.gameObject);

                if (m_ColorControlPoints.Count == 0)
                {
                    m_TransferFunctionData.ClearColorControlPoints();

                    // ClearColorControlPoints adds new default color control point(s). We have to synchronize
                    foreach (var colorCpID in m_TransferFunctionData.ColorControlPointIDs())
                        AddColorControlPointInternal(m_TransferFunctionData.GetColorControlPointAt(colorCpID), colorCpID);
                }
                else
                {
                    // remove from underlying Transfer function data
                    m_TransferFunctionData.RemoveColorControlPoint(m_CurrColorControlPointID);
                }

                // no color control point is currently selected
                UpdateCurrColorControlPointID(-1);

                // don't forget to request a transfer function texture update
                m_TransferFunctionData.TryUpdateColorLookupTexture();
                return;
            }

            Debug.LogError("No control point is currently selected."
                    + " This selected control point remove handler should not have been active!");
        }


        void OnClearColorsClick()
        {
            m_TransferFunctionData.ClearColorControlPoints();
            foreach (var item in m_ColorControlPoints.Values)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            m_ColorControlPoints.Clear();

            // ClearColorControlPoints adds new default color control point(s). We have to synchronize
            foreach (var colorCpID in m_TransferFunctionData.ColorControlPointIDs())
            {
                AddColorControlPointInternal(
                    m_TransferFunctionData.GetColorControlPointAt(colorCpID),
                    colorCpID
                );
            }
            // no color control point is currently selected
            UpdateCurrColorControlPointID(-1);
            // don't forget to request a transfer function texture update
            m_TransferFunctionData.TryUpdateColorLookupTexture();
        }


        void OnClearAlphasClick()
        {
            m_TransferFunctionData.ClearAlphaControlPoints();
            foreach (var item in m_AlphaControlPoints.Values)
            {
                item.gameObject.SetActive(false);
                Destroy(item.gameObject);
            }
            m_AlphaControlPoints.Clear();

            // ClearAlphaControlPoints adds new default alpha control point(s). We have to synchronize
            foreach (var alphaCpID in m_TransferFunctionData.AlphaControlPointIDs())
            {
                AddAlphaControlPointInternal(
                    m_TransferFunctionData.GetAlphaControlPointAt(alphaCpID),
                    alphaCpID
                );
            }
            // no alpha control point is currently selected
            UpdateCurrAlphaControlPointID(-1);
            // don't forget to request a transfer function texture update
            OnAlphaControlPointDataChange();
        }

        void OnColorPickerClick()
        {
            if (m_CurrColorControlPointID < 0)
            {
                Debug.LogWarning("Color picker button is active although no ColorControl is selected!");
                return;
            }

            m_ColorPickerWrapper.ColorPickerChange += OnColorPickerChange;
            m_ColorPickerWrapper.ColorPickerDone += OnColorPickerDoneClick;

            // don't forget to set the ColorPicker's initial color
            m_ColorPickerWrapper.Init(m_ColorControlPoints[m_CurrColorControlPointID].ControlPointData.Value);

            // activate whole color picker Canvas UI
            m_ColorPickerWrapper.gameObject.SetActive(true);

            DisableInteractiveness();
        }


        void EnableInteractiveness()
        {
            foreach (var cp in m_AlphaControlPoints)
                cp.Value.Interactable = true;

            foreach (var cp in m_ColorControlPoints)
                cp.Value.Interactable = true;

            m_ClearAlphas.interactable = true;
            m_ClearColors.interactable = true;
            m_Save.interactable = true;
            m_Load.interactable = true;
        }


        void DisableInteractiveness()
        {
            foreach (var cp in m_AlphaControlPoints)
                cp.Value.Interactable = false;

            foreach (var cp in m_ColorControlPoints)
                cp.Value.Interactable = false;

            m_RemoveSelection.interactable = false;
            m_ClearAlphas.interactable = false;
            m_ClearColors.interactable = false;
            m_ColorPicker.interactable = false;
            m_Save.interactable = false;
            m_Load.interactable = false;
        }


        void OnColorPickerChange(Color color)
        {
            // update currently selected color control point UI's color. This will automatically update
            // underlying transfer function data
            m_ColorControlPoints[m_CurrColorControlPointID].SetColor(color);

            m_TransferFunctionData.TryUpdateColorLookupTexture();
        }


        void OnColorPickerDoneClick(Color _)
        {
            m_ColorPickerWrapper.ColorPickerChange -= OnColorPickerChange;
            m_ColorPickerWrapper.ColorPickerDone -= OnColorPickerDoneClick;

            // disable whole color picker Canvas UI
            m_ColorPickerWrapper.gameObject.SetActive(false);

            // don't forget to request a transfer function texture update
            m_TransferFunctionData.TryUpdateColorLookupTexture();

            // make sure to re-enable TF1D interactiveness BEFORE re-selecting the control point
            EnableInteractiveness();

            // re-select the corresponding color control point
            EventSystem.current.SetSelectedGameObject(m_ColorControlPoints[m_CurrColorControlPointID].gameObject);
        }


        void OnColorControlPointSelected(int cpID)
        {
            Debug.Log("OnSelected");
            UpdateCurrColorControlPointID(cpID);
        }


        private Coroutine m_CurrControlPointDeselectedCoroutine = null;
        void OnColorControlPointDeselected(int cpID)
        {
            if (m_CurrControlPointDeselectedCoroutine != null)
                StopCoroutine(m_CurrControlPointDeselectedCoroutine);
            m_CurrControlPointDeselectedCoroutine =
                StartCoroutine(OnControlPointDeselectedCoroutine(m_ColorControlPoints[cpID].gameObject));
        }


        void OnAlphaControlPointSelect(int cpID)
        {
            UpdateCurrAlphaControlPointID(cpID);
        }


        void OnAlphaControlPointDeselected(int cpID)
        {
            if (m_CurrControlPointDeselectedCoroutine != null)
                StopCoroutine(m_CurrControlPointDeselectedCoroutine);
            m_CurrControlPointDeselectedCoroutine =
                StartCoroutine(OnControlPointDeselectedCoroutine(m_AlphaControlPoints[cpID].gameObject));
        }


        // honestly, Unity's UI system is probably the worst UI system that has ever existed.
        // calling EventSystem.current.currentSelectedGameObject within an OnDeselect callback
        // does NOT return the newly selected GameObject instead the one being deselected...
        // Even worse, one would think that in the next frame it would be correctly set, but
        // no, it will be not until some unknown X frames later. What a piece of garbage software.
        IEnumerator OnControlPointDeselectedCoroutine(GameObject prevSelectedGameObject)
        {
            yield return new WaitUntil(() => EventSystem.current.currentSelectedGameObject != prevSelectedGameObject);
            if (
                EventSystem.current.currentSelectedGameObject == null ||
                !EventSystem.current.currentSelectedGameObject.CompareTag(m_ColorAlphaControlsTag))
            {
                UpdateCurrAlphaControlPointID(-1);
                UpdateCurrColorControlPointID(-1);
            }
            m_CurrControlPointDeselectedCoroutine = null;
        }


        // Called whenever underlying data for any alpha control point change. Expensive method!
        void OnAlphaControlPointDataChange()
        {
            // update histogram shader
            List<ControlPoint<float, float>> tmp = new();

            foreach (var alphaCpID in m_TransferFunctionData.AlphaControlPointIDs())
            {
                tmp.Add(m_TransferFunctionData.GetAlphaControlPointAt(alphaCpID));
            }
            tmp.Sort((x, y) => x.Position.CompareTo(y.Position));
            if (tmp[0].Position > 0)
            {
                var firstVal = tmp[0].Value;
                tmp.Insert(0, new(0, firstVal));
            }
            if (tmp[tmp.Count - 1].Position < 1)
            {
                var lastVal = tmp[tmp.Count - 1].Value;
                tmp.Add(new(1, lastVal));
            }

            float[] alphaPositions = new float[TFConstants.MAX_ALPHA_CONTROL_POINTS];
            float[] alphaValues = new float[TFConstants.MAX_ALPHA_CONTROL_POINTS];
            for (int i = 0; i < tmp.Count; ++i)
            {
                alphaPositions[i] = tmp[i].Position;
                alphaValues[i] = tmp[i].Value;
            }

            m_HistogramImage.material.SetInteger(ALPHA_COUNT_SHADER_ID, tmp.Count);
            m_HistogramImage.material.SetFloatArray(ALPHA_POSITIONS_SHADER_ID, alphaPositions);
            m_HistogramImage.material.SetFloatArray(ALPHA_VALUES_SHADER_ID, alphaValues);
            m_TransferFunctionData.TryUpdateColorLookupTexture();
        }


        // Called whenever underlying data for any color control point change. Expensive method!
        void OnColorControlPointDataChange()
        {
            m_TransferFunctionData.TryUpdateColorLookupTexture();
        }


        void OnTFTexChange(Texture2D newTex)
        {
            m_GradientColorImage.texture = newTex;
        }


        void OnAddAlphaControlPoint(Vector2 histogramPos)
        {
            // reason for the -2 is for the extreme points at position 0 and 1 respectively.
            if (m_AlphaControlPoints.Count < TFConstants.MAX_ALPHA_CONTROL_POINTS - 2)
            {
                AddAlphaControlPoint(
                    new ControlPoint<float, float>(histogramPos.x, histogramPos.y)
                );
                OnAlphaControlPointDataChange();
            }
        }


        void OnAddColorControlPoint(float xPos)
        {
            if (m_ColorControlPoints.Count < TFConstants.MAX_COLOR_CONTROL_POINTS - 2)
            {
                AddColorControlPoint(new ControlPoint<float, Color>(xPos, Color.white));
                m_TransferFunctionData.TryUpdateColorLookupTexture();
            }
        }
    }
}
