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
        ///////////////////////////////////////////////////////////////////////
        /// IN-CURRENT or IN-CHILDREN REFERENCES
        ///////////////////////////////////////////////////////////////////////
        [SerializeField] RawImage m_HistogramImage;
        [SerializeField] RectTransform m_HistogramTransform;
        [SerializeField] HistogramUI m_HistogramUI;
        [SerializeField] RectTransform m_ColorGradientControlRange;
        [SerializeField] RawImage m_GradientColorImage;
        [SerializeField] ColorGradientRangeUI m_GradientColorUI;

        [SerializeField] Button m_RemoveSelection;
        [SerializeField] Button m_AddColor;
        [SerializeField] Button m_AddAlpha;
        [SerializeField] Button m_ClearColors;
        [SerializeField] Button m_ClearAlphas;
        [SerializeField] Button m_ColorPicker;

        [SerializeField] Button m_Save;
        [SerializeField] Button m_Load;

        [SerializeField] ColorPickerWrapper m_ColorPickerWrapper;
        [SerializeField] RectTransform m_TF1DCanvasTransform;
        [SerializeField] RectTransform m_ColorPickerTransform;

        [SerializeField] RectTransform m_XAxisContainer;
        [SerializeField] RectTransform m_YAxisContainer;


        ///////////////////////////////////////////////////////////////////////
        /// PREFABS
        ///////////////////////////////////////////////////////////////////////
        [Tooltip("Color control point UI prefab that will be instantiated by this TF UI in the color gradient "
            + "range UI once a new color control point is added.")]
        public GameObject m_ColorControlPointUIPrefab;

        [Tooltip("Alpha control point UI prefab that will be instantiated by this TF UI in the histogram child UI "
            + "once a new color control point is added.")]
        public GameObject m_AlphaControlPointUIPrefab;


        ///////////////////////////////////////////////////////////////////////
        /// CACHED COMPONENTS
        ///////////////////////////////////////////////////////////////////////
        private readonly int ALPHA_COUNT_SHADER_ID = Shader.PropertyToID("_AlphaCount");
        private readonly int ALPHA_POSITIONS_SHADER_ID = Shader.PropertyToID("_AlphaPositions");
        private readonly int ALPHA_VALUES_SHADER_ID = Shader.PropertyToID("_AlphaValues");
        private readonly int SCALE_ID = Shader.PropertyToID("_Scale");


        ///////////////////////////////////////////////////////////////////////
        /// SCALES
        ///////////////////////////////////////////////////////////////////////
        private IScaleContinuous<int, float> m_x_scale;
        private IScaleContinuous<float, float> m_y_scale;

        ///////////////////////////////////////////////////////////////////////
        /// AXES
        ///////////////////////////////////////////////////////////////////////
        private Axis<int> m_x_axis;
        private Axis<float> m_y_axis;

        ///////////////////////////////////////////////////////////////////////
        /// MATERIALS
        ///////////////////////////////////////////////////////////////////////
        public Material AxisMaterial;
        public Material TickMaterial;


        ///////////////////////////////////////////////////////////////////////
        /// DIRTY FLAGS
        ///////////////////////////////////////////////////////////////////////
        private bool m_DirtyHistogramTex = false;
        private bool m_DirtyAlphaCps = false;


        ///////////////////////////////////////////////////////////////////////
        /// HISTOGRAM-RELATED
        ///////////////////////////////////////////////////////////////////////
        private Texture2D m_HistogramTex;
        private UInt64[] m_OriginalHistogramData;
        private const int m_HistogramTexWidth = 256;
        private UInt16[] m_HistogramTexData;


        ///////////////////////////////////////////////////////////////////////
        /// MISC
        ///////////////////////////////////////////////////////////////////////
        private TagHandle m_ColorAlphaControlsTag;
        // -1 means None is selected which in turn means that we have no control points (empty list)
        private int m_CurrColorControlPointID = -1;
        private int m_CurrAlphaControlPointID = -1;
        private TransferFunction1D m_TransferFunction = null;
        private readonly Dictionary<int, ColorControlPointUI> m_ColorControlPoints = new();
        private readonly Dictionary<int, AlphaControlPointUI> m_AlphaControlPoints = new();

        private float m_PrevHistogramWidth = 0;
        private float m_PrevHistogramHeight = 0;


        private void Awake()
        {
            m_ColorAlphaControlsTag = TagHandle.GetExistingTag("ColorAlphaControls");
        }


        private void Update()
        {
            m_x_axis?.Update();
            m_y_axis?.Update();

            // update histogram texture if necessary
            if (m_DirtyHistogramTex)
            {
                GPUUpdateHistogramTex();
                m_DirtyHistogramTex = false;
            }

            // update alpha control points (for line rendering) if necessary
            if (m_DirtyAlphaCps)
            {
                GPUUpdateAlphaControlPoints();
                m_DirtyAlphaCps = false;
            }

            if (m_HistogramTransform.rect.width != m_PrevHistogramWidth
                || m_HistogramTransform.rect.height != m_PrevHistogramHeight)
            {
                OnHistogramDimsChange();
                m_PrevHistogramWidth = m_HistogramTransform.rect.width;
                m_PrevHistogramHeight = m_HistogramTransform.rect.height;
            }

            // TODO: probably someone else should take the responsibility of updating the color lookup texture.
            m_TransferFunction?.TryUpdateColorLookupTex();
        }


        void OnEnable()
        {
            if (m_TransferFunction == null)
            {
                throw new Exception("Init should be appropriately called before enabling this UI.");
            }

            TransferFunctionEvents.ModelTF1DColorControlAddition += OnModelTF1DColorControlAddition;
            TransferFunctionEvents.ModelTF1DColorControlRemoval += OnModelTF1DColorControlRemoval;
            TransferFunctionEvents.ModelTF1DAlphaControlAddition += OnModelTF1DAlphaControlAddition;
            TransferFunctionEvents.ModelTF1DAlphaControlRemoval += OnModelTF1DAlphaControlRemoval;

            m_RemoveSelection.onClick.AddListener(OnRemoveSelectedControlPoint);
            m_AddAlpha.onClick.AddListener(OnAddAlphaControlPointClick);
            m_AddColor.onClick.AddListener(OnAddColorControlPointClick);
            m_ClearColors.onClick.AddListener(OnClearColorsClick);
            m_ClearAlphas.onClick.AddListener(OnClearAlphasClick);
            m_ColorPicker.onClick.AddListener(OnColorPickerClick);
            m_Save.onClick.AddListener(OnSave);
            m_Load.onClick.AddListener(OnLoad);

            m_HistogramUI.OnAddAlphaControlPoint += OnAddAlphaControlPoint;
            m_HistogramUI.OnHistogramZoom += OnHistogramZoom;
            m_GradientColorUI.OnAddColorControlPoint += OnAddColorControlPoint;

            m_ColorPickerWrapper.gameObject.SetActive(false);
            StartCoroutine(ConstructAxes());
        }


        void OnDisable()
        {
            TransferFunctionEvents.ModelTF1DColorControlAddition -= OnModelTF1DColorControlAddition;
            TransferFunctionEvents.ModelTF1DColorControlRemoval -= OnModelTF1DColorControlRemoval;
            TransferFunctionEvents.ModelTF1DAlphaControlAddition -= OnModelTF1DAlphaControlAddition;
            TransferFunctionEvents.ModelTF1DAlphaControlRemoval -= OnModelTF1DAlphaControlRemoval;

            m_RemoveSelection.onClick.RemoveAllListeners();
            m_AddAlpha.onClick.RemoveAllListeners();
            m_AddColor.onClick.RemoveAllListeners();
            m_ClearColors.onClick.RemoveAllListeners();
            m_ClearAlphas.onClick.RemoveAllListeners();
            m_ColorPicker.onClick.RemoveAllListeners();
            m_Save.onClick.RemoveAllListeners();
            m_Load.onClick.RemoveAllListeners();

            m_HistogramUI.OnAddAlphaControlPoint -= OnAddAlphaControlPoint;
            m_HistogramUI.OnHistogramZoom -= OnHistogramZoom;
            m_GradientColorUI.OnAddColorControlPoint -= OnAddColorControlPoint;
        }


        IEnumerator ConstructAxes()
        {
            yield return new WaitUntil(() => m_XAxisContainer.rect.width != 0 && m_YAxisContainer.rect.height != 0);

            m_YScaleReferenceDomain = new(0.0f, 1.0f);

            // create the x/y scales and axes
            m_x_scale = new ScaleLinearIntFloat(0, 255, 0, m_XAxisContainer.rect.width);

            // create/update Y scale
            m_y_scale = new ScaleLinearFloatFloat(m_YScaleReferenceDomain.x, m_YScaleReferenceDomain.y, 0, m_YAxisContainer.rect.height);
            m_y_scale.SetTickFormat("0.000");
            m_y_scale.RangeChanged += OnScaleYDomainRangeChange;
            m_y_scale.DomainChanged += OnScaleYDomainRangeChange;

            m_x_axis = new AxisTop<int>(m_x_scale)
                .SetAxisStrokeWidth(1.75f)
                .SetAxisMaterial(AxisMaterial)
                .SetTickCount(4)
                .SetTickMaterial(TickMaterial)
                .SetTickTextColor(Color.white)
                .SetTickStrokeWidth(1.0f)
                .SetTickSize(m_XAxisContainer.rect.height / 3.0f)
                .SetTickFontSize(10.0f)
                .SetIsTickText2D(true)
                .SetUseWorldSpace(false)
                .SetAlignment(LineAlignment.TransformZ)
                .Attach(m_XAxisContainer.gameObject);

            m_y_axis = new AxisLeft<float>(m_y_scale)
                .SetAxisStrokeWidth(1.5f)
                .SetAxisMaterial(AxisMaterial)
                .SetTickCount(4)
                .SetTickMaterial(TickMaterial)
                .SetTickTextColor(Color.white)
                .SetTickStrokeWidth(1.0f)
                .SetTickSize(m_YAxisContainer.rect.width / 3.0f)
                .SetTickFontSize(10.0f)
                .SetIsTickText2D(true)
                .SetUseWorldSpace(false)
                .SetAlignment(LineAlignment.TransformZ)
                .Attach(m_YAxisContainer.gameObject);
        }


        public void Init(TransferFunction1D tf)
        {
            m_TransferFunction = tf;

            InitializeHistogramTex();

            m_ColorControlPoints.Clear();

            // synchronize control points UI array with underlying transfer function data
            foreach (var colorCpID in m_TransferFunction.GetColorControlPointIDs())
            {
                m_TransferFunction.TryGetColorControlPoint(colorCpID, out var cp);
                AddColorControlPointUI(colorCpID, cp);
            }

            foreach (var alphaCpID in m_TransferFunction.GetAlphaControlPointIDs())
            {
                m_TransferFunction.TryGetAlphaControlPoint(alphaCpID, out var cp);
                AddAlphaControlPointUI(alphaCpID, cp);
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

            // the color lookup texture is managed by the underlying transfer function - we just need to
            // assign it and forget about it
            m_GradientColorImage.texture = m_TransferFunction.GetColorLookupTex();
        }


        private void InitializeHistogramTex()
        {
            m_HistogramTex = new(m_HistogramTexWidth, 1, TextureFormat.R16, mipChain: false, linear: false, createUninitialized: false)
            {
                filterMode = FilterMode.Point
            };
            m_HistogramTexData = new UInt16[m_HistogramTexWidth];

            m_HistogramImage.texture = m_HistogramTex;
        }


        // TODO: add proper interactions for adding ignore ranges
        private List<Tuple<float, float>> m_HistogramIgnoreRanges = new (){};


        public void SetHistogramData(UInt64[] histogram)
        {
            m_OriginalHistogramData = histogram;
            m_DirtyHistogramTex = true;
        }


        private void GPUUpdateHistogramTex()
        {
            int r = m_OriginalHistogramData.Length / m_HistogramTexWidth;
            float currScale = 1.0f / m_CurrHistogramScaleY;

            double total = 0;
            for (int i = 0; i < m_OriginalHistogramData.Length;)
            {
                bool shouldBeIgnored = false;
                int rangeEnd = 0;
                foreach (var range in m_HistogramIgnoreRanges)
                {
                    rangeEnd = (int)(range.Item2 * m_OriginalHistogramData.Length);
                    if (i >= (int)(range.Item1 * m_OriginalHistogramData.Length) && i < rangeEnd)
                    {
                        shouldBeIgnored = true;
                        break;
                    }
                }
                if (shouldBeIgnored)
                {
                    i = rangeEnd;
                    continue;
                }
                total += currScale * m_OriginalHistogramData[i];
                ++i;
            }

            for (int i = 0; i < m_HistogramTexWidth; ++i)
            {
                bool shouldBeIgnored = false;

                foreach (var range in m_HistogramIgnoreRanges)
                {
                    if (i >= (int)(range.Item1 * m_HistogramTexWidth) && i < (int)(range.Item2 * m_HistogramTexWidth))
                    {
                        shouldBeIgnored = true;
                        break;
                    }
                }

                if (shouldBeIgnored)
                {
                    m_HistogramTexData[i] = 0;
                    continue;
                }

                double b = 0;
                for (int j = 0; j < r; ++j)
                {
                    b += m_OriginalHistogramData[i * r + j];
                }
                m_HistogramTexData[i] = (UInt16)Mathf.RoundToInt(UInt16.MaxValue * Mathf.Clamp01((float)(b / total)));
            }

            m_HistogramTex.SetPixelData(m_HistogramTexData, 0);
            m_HistogramTex.Apply();
        }


        void AddColorControlPoint(ControlPoint<float, Color> cp)
        {
        }

        private void AddColorControlPointUI(int cpID, ControlPoint<float, Color> cp)
        {
            var newCp = Instantiate(
                    m_ColorControlPointUIPrefab,
                    parent: m_ColorGradientControlRange
                )
                .GetComponent<ColorControlPointUI>();
            newCp.Init(cpID, cp);
            newCp.ControlPointDeselected += OnColorControlPointDeselected;
            newCp.ControlPointSelected += OnColorControlPointSelected;
            m_ColorControlPoints.Add(cpID, newCp);
        }


        /// <summary>
        ///     Adds an alpha control point UI corresponding to the provided alpha control point.
        /// </summary>
        /// 
        /// <remarks>
        ///     This does NOT touch the underlying transfer function data and only adds a UI element
        ///     corresponding to the provided control point.
        /// </remarks>
        /// 
        /// <param name="cp">
        ///     Underlying alpha control point.
        /// </param>
        /// 
        /// <param name="cpID">
        ///     Underlying alpha control point ID.
        /// </param>
        private void AddAlphaControlPointUI(int cpID, ControlPoint<float, float> cp)
        {
            var newCp = Instantiate(m_AlphaControlPointUIPrefab, parent: m_HistogramTransform)
                .GetComponent<AlphaControlPointUI>();
            newCp.Init(cpID, cp, m_HistogramTransform, scale: new(1.0f, m_CurrHistogramScaleY),
                translation: Vector2.zero);
            newCp.ControlPointDeselected += OnAlphaControlPointDeselected;
            newCp.ControlPointSelected += OnAlphaControlPointSelect;
            newCp.ControlPointData.OnValueChange += OnAlphaControlPointDataChange;
            m_AlphaControlPoints.Add(cpID, newCp);
            m_DirtyAlphaCps = true;
        }


        private void RemoveAlphaControlPointUI(int cpID)
        {
            AlphaControlPointUI cpToRemove = m_AlphaControlPoints[cpID];

            // remove it from alpha control points UI list
            m_AlphaControlPoints.Remove(cpID);

            // destroy UI element (no need to unsubscribe)
            Destroy(cpToRemove.gameObject);

            m_DirtyAlphaCps = true;
        }


        private void RemoveColorControlPointUI(int cpID)
        {
            var cpToRemove = m_ColorControlPoints[cpID];

            // remove it from color control points UI list
            m_ColorControlPoints.Remove(cpID);

            // destroy UI element (no need to unsubscribe)
            Destroy(cpToRemove.gameObject);
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
                throw new Exception("only one control point should be active at a given time!");
            }

            if (m_CurrAlphaControlPointID != -1)
            {
                // issue a request for removing the current alpha control point from the TF1D model
                TransferFunctionEvents.ViewTF1DAlphaControlRemoval?.Invoke(m_CurrAlphaControlPointID);
                // no alpha control point is currently selected
                UpdateCurrAlphaControlPointID(-1);
                return;
            }

            if (m_CurrColorControlPointID != -1)
            {
                // issue a request for removing the current alpha control point from the TF1D model
                TransferFunctionEvents.ViewTF1DColorControlRemoval?.Invoke(m_CurrColorControlPointID);
                // no color control point is currently selected
                UpdateCurrColorControlPointID(-1);
                return;
            }

            Debug.LogError("No control point is currently selected."
                    + " This selected control point remove handler should not have been active!");
        }


        void OnClearColorsClick()
        {
            var keys = m_ColorControlPoints.Keys.ToList();
            foreach (int k in keys)
            {
                TransferFunctionEvents.ViewTF1DColorControlRemoval?.Invoke(k);
            }

            // no color control point is currently selected
            UpdateCurrColorControlPointID(-1);
        }


        void OnClearAlphasClick()
        {
            var keys = m_AlphaControlPoints.Keys.ToList();
            foreach (int k in keys)
            {
                TransferFunctionEvents.ViewTF1DAlphaControlRemoval?.Invoke(k);
            }

            // no alpha control point is currently selected
            UpdateCurrAlphaControlPointID(-1);
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


        void OnSave()
        {
            m_TransferFunction.Serialize();
        }


        void OnLoad()
        {
            m_TransferFunction.Deserialize("TODO");
        }


        void EnableInteractiveness()
        {
            foreach (var cp in m_AlphaControlPoints)
                cp.Value.Interactable = true;

            foreach (var cp in m_ColorControlPoints)
                cp.Value.Interactable = true;

            m_AddColor.interactable = true;
            m_AddAlpha.interactable = true;
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
            m_AddColor.interactable = false;
            m_AddAlpha.interactable = false;
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
        }


        void OnColorPickerDoneClick(Color _)
        {
            m_ColorPickerWrapper.ColorPickerChange -= OnColorPickerChange;
            m_ColorPickerWrapper.ColorPickerDone -= OnColorPickerDoneClick;

            // disable whole color picker Canvas UI
            m_ColorPickerWrapper.gameObject.SetActive(false);

            // make sure to re-enable TF1D interactiveness BEFORE re-selecting the control point
            EnableInteractiveness();

            // re-select the corresponding color control point
            EventSystem.current.SetSelectedGameObject(m_ColorControlPoints[m_CurrColorControlPointID].gameObject);
        }


        void OnColorControlPointSelected(int cpID)
        {
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


        void OnModelTF1DColorControlAddition(int cpID, ControlPoint<float, Color> cp) => AddColorControlPointUI(cpID, cp);


        void OnModelTF1DAlphaControlAddition(int cpID, ControlPoint<float, float> cp) => AddAlphaControlPointUI(cpID, cp);


        void OnModelTF1DColorControlRemoval(int cpID) => RemoveColorControlPointUI(cpID);


        void OnModelTF1DAlphaControlRemoval(int cpID) => RemoveAlphaControlPointUI(cpID);


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


        /// <summary>
        ///     Should be called whenever underlying data for any alpha control point is changed
        /// </summary>
        void OnAlphaControlPointDataChange()
        {
            m_DirtyAlphaCps = true;
        }


        private readonly float[] m_AlphaPositionsOrdered = new float[TransferFunction1D.MAX_ALPHA_CONTROL_POINTS];
        private readonly float[] m_AlphaValuesOrdered = new float[TransferFunction1D.MAX_ALPHA_CONTROL_POINTS];
        private readonly List<ControlPoint<float, float>> m_AlphaCps = new(TransferFunction1D.MAX_ALPHA_CONTROL_POINTS);


        private void GPUUpdateAlphaControlPoints()
        {
            m_AlphaCps.Clear();

            foreach (var cpID in m_TransferFunction.GetAlphaControlPointIDs())
            {
                m_TransferFunction.TryGetAlphaControlPoint(cpID, out var cp);
                m_AlphaCps.Add(cp);
            }
            m_AlphaCps.Sort((x, y) => x.Position.CompareTo(y.Position));

            if (m_AlphaCps.Count == 0 || m_AlphaCps[0].Position > 0)
            {
                var firstVal = m_AlphaCps[0].Value;
                m_AlphaCps.Insert(0, new(0, firstVal));
            }

            if (m_AlphaCps[^1].Position < 1)
            {
                var lastVal = m_AlphaCps[^1].Value;
                m_AlphaCps.Add(new(1, lastVal));
            }

            for (int i = 0; i < m_AlphaCps.Count; ++i)
            {
                m_AlphaPositionsOrdered[i] = m_AlphaCps[i].Position;
                m_AlphaValuesOrdered[i] = m_AlphaCps[i].Value * m_CurrHistogramScaleY;
            }

            m_HistogramImage.material.SetInteger(ALPHA_COUNT_SHADER_ID, m_AlphaCps.Count);
            m_HistogramImage.material.SetFloatArray(ALPHA_POSITIONS_SHADER_ID, m_AlphaPositionsOrdered);
            m_HistogramImage.material.SetFloatArray(ALPHA_VALUES_SHADER_ID, m_AlphaValuesOrdered);
            OnHistogramDimsChange();
        }


        void OnAddAlphaControlPoint(Vector2 histogramPos)
        {
            // reason for the -2 is for the extreme points at position 0 and 1 respectively.
            if (m_AlphaControlPoints.Count < TransferFunction1D.MAX_ALPHA_CONTROL_POINTS - 2)
            {
                var cp = new ControlPoint<float, float>(histogramPos.x, histogramPos.y / m_CurrHistogramScaleY);
                // issue that this UI has requested the addittion of a new alpha control point
                TransferFunctionEvents.ViewTF1DAlphaControlAddition?.Invoke(cp);
            }
        }


        void OnAddAlphaControlPointClick()
        {
            if (m_AlphaControlPoints.Count < TransferFunction1D.MAX_ALPHA_CONTROL_POINTS - 2)
            {
                var cp = new ControlPoint<float, float>(0.5f, 0.5f);
                // issue that this UI has requested the addittion of a new alpha control point
                TransferFunctionEvents.ViewTF1DAlphaControlAddition?.Invoke(cp);
            }
        }


        void OnAddColorControlPoint(float xPos)
        {
            if (m_ColorControlPoints.Count < TransferFunction1D.MAX_COLOR_CONTROL_POINTS - 2)
            {
                var cp = new ControlPoint<float, Color>(xPos, Color.white);
                // issue that this UI has requested the addition of a new color control point
                TransferFunctionEvents.ViewTF1DColorControlAddition?.Invoke(cp);
            }
        }


        void OnAddColorControlPointClick() => OnAddColorControlPoint(0.5f);


        void OnScaleYDomainRangeChange(float v0, float v1)
        {
            // make sure the histogram gets scaled (i.e., regenerated so that we end up with better quality)
            m_DirtyHistogramTex = true;

            // reposition the alpha control points
            Vector2 scale = new(1.0f, m_CurrHistogramScaleY);
            foreach (var cp in m_AlphaControlPoints.Values)
            {
                cp.SetScale(scale);
            }
        }


        private Vector2 m_YScaleReferenceDomain;

        [Tooltip("Sensitivity of histogram (1D transfer function) zooming"), Range(0.03f, 3.0f)]
        public float HistogramZoomSensitivity = 0.1f;

        public Vector2 HistogramScaleExtentY = new (1.0f, 200.0f);
        private float m_CurrHistogramScaleY = 1.0f;

        void OnHistogramZoom(float scrollDelta, Vector2 _ /* normalizedPos */)
        {
            float zoomVal = Mathf.Clamp(m_CurrHistogramScaleY + HistogramZoomSensitivity * scrollDelta,
                HistogramScaleExtentY.x, HistogramScaleExtentY.y);

            // in case the new zoom value is the same as the current one, do nothing
            if (zoomVal == m_CurrHistogramScaleY)
                return;

            m_CurrHistogramScaleY = zoomVal;

            // scale along the Y axis
            float ky = 1.0f / m_CurrHistogramScaleY;

            // translation along the Y axis - currently not supported, very little reason to add it
            float ty = 0.0f;

            m_y_scale.Domain(m_YScaleReferenceDomain.x * ky + ty, m_YScaleReferenceDomain.y * ky + ty);

            m_DirtyAlphaCps = true;
        }


        private void OnHistogramDimsChange()
        {
            float _min = Mathf.Min(m_HistogramTransform.rect.width, m_HistogramTransform.rect.height);
            Vector4 d = new (m_HistogramTransform.rect.width / _min, m_HistogramTransform.rect.height / _min, 0, 0);
            m_HistogramImage.material.SetVector(SCALE_ID, d);
        }

    }
}
