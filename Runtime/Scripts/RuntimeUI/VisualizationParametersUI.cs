using System;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
using UnityD3;
using System.Collections;
using System.Collections.Generic;

namespace UnityCTVisualizer
{
    public enum INTERPOLATION
    {
        NEAREST_NEIGHBOR = 0,
        TRILLINEAR,
    }


    [Serializable]
    public struct VisualizationParameters
    {

        public string TF;
        public float OpacityCutoff;
        public string Interpolation;
        public float SamplingQualityFactor;
        public List<float> LODDistances;
        public byte HomogeneityTolerance;
        public float VolumetricObjectScaleFactor;


        public readonly void Serialize(string fp)
        {
            if (File.Exists(fp))
            {
                throw new Exception($"{Path.GetFileName(fp)} already exists in {Path.GetDirectoryName(fp)}");
            }

            using (StreamWriter sw = File.CreateText(fp))
            {
                sw.Write(JsonConvert.SerializeObject(this));
            }
        }


        public static VisualizationParameters Deserialize(string fp)
        {
            if (!File.Exists(fp))
            {
                throw new Exception($"{Path.GetFileName(fp)} does NOT exist in {Path.GetDirectoryName(fp)}");
            }

            return JsonConvert.DeserializeObject<VisualizationParameters>(File.ReadAllText(fp));
        }
    }


    public class VisualizationParametersUI : MonoBehaviour
    {

        /////////////////////////////////
        // UI MODIFIERS
        /////////////////////////////////
        [SerializeField] Toggle m_PerspectiveToggle;
        [SerializeField] TMP_Dropdown m_TFDropDown;
        [SerializeField] Slider m_OpacityCutoffSlider;
        [SerializeField] TMP_InputField m_OpacityCutoffInputField;
        [SerializeField] Slider m_SamplingQualityFactorSlider;
        [SerializeField] TMP_InputField m_SamplingQualityFactorInputField;
        [SerializeField] TMP_Dropdown m_InterpolationDropDown;
        [SerializeField] Slider m_HomogeneityToleranceSlider;
        [SerializeField] TMP_InputField m_HomogeneityToleranceInputField;
        [SerializeField] Slider m_ObjectScaleSlider;
        [SerializeField] TMP_InputField m_ObjectScaleInputField;

        [SerializeField] Button m_Save;
        [SerializeField] Button m_Load;
        [SerializeField] RectTransform m_LODDistancesControlContainer;
        [SerializeField] Material m_AxisAndTicksMaterial;
        [SerializeField] GameObject m_LODDistanceControlPrefab;
        [SerializeField] GameObject m_LODPanelPrefab;
        [SerializeField] List<Color> m_LODColors = new() {Color.green, Color.cyan, Color.blue, Color.black, Color.red };

        /////////////////////////////////
        // CACHED COMPONENTS
        /////////////////////////////////
        private int m_PrevTFIndex = -1;
        private int m_PrevInterIndex = -1;
        private List<LODDistanceControlPointUI> m_LODDistanceControls = new();
        private List<LODPanelComponent> m_LODPanels = new();
        private List<float> m_LODDistances = new();

        private ManagerUI m_ManagerUI;
        private Axis<float> m_LODDistancesAxis;
        private ScaleLog m_LODDistancesScale;


        public void Init(ManagerUI managerUI)
        {
            m_ManagerUI = managerUI;
        }


        private void Awake()
        {
            m_TFDropDown.options.Clear();
            foreach (string enumName in Enum.GetNames(typeof(TF)))
            {
                m_TFDropDown.options.Add(new TMP_Dropdown.OptionData(enumName));
            }
            m_TFDropDown.onValueChanged.AddListener(OnTFDropDownChange);

            m_InterpolationDropDown.options.Clear();
            foreach (string enumName in Enum.GetNames(typeof(INTERPOLATION)))
            {
                m_InterpolationDropDown.options.Add(new TMP_Dropdown.OptionData(enumName.Replace("_", " ").ToLower()));
            }

            m_PerspectiveToggle.isOn = !Camera.main.orthographic;

            m_OpacityCutoffSlider.minValue = 0;
            m_OpacityCutoffSlider.maxValue = 255;
            m_OpacityCutoffInputField.readOnly = false;
            m_OpacityCutoffInputField.contentType = TMP_InputField.ContentType.IntegerNumber;


            m_SamplingQualityFactorSlider.minValue = VolumetricDataset.SamplingQualityFactorRange.x;
            m_SamplingQualityFactorSlider.maxValue = VolumetricDataset.SamplingQualityFactorRange.y;
            m_SamplingQualityFactorInputField.readOnly = false;
            m_SamplingQualityFactorInputField.contentType = TMP_InputField.ContentType.DecimalNumber;

            m_HomogeneityToleranceSlider.wholeNumbers = true;
            m_HomogeneityToleranceSlider.minValue = 0;
            m_HomogeneityToleranceSlider.maxValue = 255;
            m_HomogeneityToleranceInputField.readOnly = false;
            m_HomogeneityToleranceInputField.contentType = TMP_InputField.ContentType.IntegerNumber;

            m_ObjectScaleSlider.minValue = VolumetricDataset.VolumetricObjectScaleFactorRange.x;
            m_ObjectScaleSlider.maxValue = VolumetricDataset.VolumetricObjectScaleFactorRange.y;
            m_ObjectScaleInputField.readOnly = false;
            m_ObjectScaleInputField.contentType = TMP_InputField.ContentType.DecimalNumber;
        }


        private void Start()
        {
            if (m_ManagerUI == null)
            {
                throw new Exception("Init has to be called before enabling this transfer function UI");
            }

            StartCoroutine(ConstructAxes());
        }


        private void OnEnable()
        {
#if !UNITY_ANDROID
            m_PerspectiveToggle.onValueChanged.AddListener(OnPerspectiveToggleChange);
#else
            m_PerspectiveToggle.interactable = false;
#endif
            m_InterpolationDropDown.onValueChanged.AddListener(OnInterDropDownChange);
            m_OpacityCutoffSlider.onValueChanged.AddListener(OnOpacityCutoffInput);
            m_OpacityCutoffInputField.onSubmit.AddListener(OnOpacityCutoffInput);
            m_SamplingQualityFactorSlider.onValueChanged.AddListener(OnSamplingQualityFactorInput);
            m_SamplingQualityFactorInputField.onSubmit.AddListener(OnSamplingQualityFactorInput);
            m_HomogeneityToleranceSlider.onValueChanged.AddListener(OnHomogeneityToleranceInput);
            m_HomogeneityToleranceInputField.onSubmit.AddListener(OnHomogeneityToleranceInput);
            m_ObjectScaleSlider.onValueChanged.AddListener(OnVolumetricObjectScaleFactorInput);
            m_ObjectScaleInputField.onValueChanged.AddListener(OnVolumetricObjectScaleFactorInput);

            m_Save.onClick.AddListener(OnSave);
            m_Load.onClick.AddListener(OnLoad);

            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange += OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODDistancesChange += OnModelLODDistancesChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange += OnModelHomogeneityToleranceChange;
            VisualizationParametersEvents.ModelVolumetricObjectScaleFactorChange += OnModelVolumetricObjectScaleFactorChange;
        }

        private void OnDisable()
        {
#if !UNITY_ANDROID
            m_PerspectiveToggle.onValueChanged.RemoveAllListeners();
#endif
            m_InterpolationDropDown.onValueChanged.RemoveAllListeners();
            m_OpacityCutoffSlider.onValueChanged.RemoveAllListeners();
            m_OpacityCutoffInputField.onSubmit.RemoveAllListeners();
            m_SamplingQualityFactorSlider.onValueChanged.RemoveAllListeners();
            m_SamplingQualityFactorInputField.onSubmit.RemoveAllListeners();
            m_HomogeneityToleranceSlider.onValueChanged.RemoveAllListeners();
            m_HomogeneityToleranceInputField.onSubmit.RemoveAllListeners();
            m_ObjectScaleSlider.onValueChanged.RemoveAllListeners();
            m_ObjectScaleInputField.onValueChanged.RemoveAllListeners();

            m_Save.onClick.RemoveAllListeners();
            m_Load.onClick.RemoveAllListeners();

            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange -= OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODDistancesChange -= OnModelLODDistancesChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange -= OnModelHomogeneityToleranceChange;
            VisualizationParametersEvents.ModelVolumetricObjectScaleFactorChange -= OnModelVolumetricObjectScaleFactorChange;
        }


        IEnumerator ConstructAxes()
        {
            yield return new WaitUntil(() => m_LODDistancesControlContainer.rect.width != 0);

            // create the x/y scales and axes
            m_LODDistancesScale = new ScaleLog(VolumetricDataset.LODDistancesRange.x, VolumetricDataset.LODDistancesRange.y, 0, m_LODDistancesControlContainer.rect.width);

            m_LODDistancesAxis = new AxisBottom<float>(m_LODDistancesScale)
                .SetAxisStrokeWidth(1.5f)
                .SetAxisMaterial(m_AxisAndTicksMaterial)
                .SetTickCount(2)
                .SetTickMaterial(m_AxisAndTicksMaterial)
                .SetTickTextColor(Color.white)
                .SetTickStrokeWidth(1.0f)
                .SetTickSize(m_LODDistancesControlContainer.rect.height / 5.0f)
                .SetTickFontSize(10.0f)
                .SetIsTickText2D(true)
                .SetUseWorldSpace(false)
                .SetAlignment(LineAlignment.TransformZ)
                .Attach(m_LODDistancesControlContainer.gameObject);
        }



        private void Update()
        {
            m_LODDistancesAxis?.Update();
        }


        private void UpdateLODDistanceAxisTicks(List<float> distances)
        {
            if (m_LODDistancesAxis == null)
                return;
            // set the tick positions for the LOD distances axis
            List<float> tick_positions = new(distances);
            tick_positions.Insert(0, VolumetricDataset.LODDistancesRange.x);
            tick_positions.Add(VolumetricDataset.LODDistancesRange.y);
            m_LODDistancesAxis.SetTicks(tick_positions);
        }


        /////////////////////////////////
        /// UI CALLBACKS (VIEW INVOKES)
        /////////////////////////////////
        private void OnTFDropDownChange(int tfIndex)
        {
            if (tfIndex != m_PrevTFIndex)
            {
                VisualizationParametersEvents.ViewTFChange?.Invoke((TF)tfIndex);
                m_PrevTFIndex = tfIndex;
            }
        }


        private void OnPerspectiveToggleChange(bool val) => Camera.main.orthographic = !val;


        private void OnInterDropDownChange(int interIndex)
        {
            if (interIndex != m_PrevInterIndex)
            {
                m_PrevInterIndex = interIndex;
                VisualizationParametersEvents.ViewInterpolationChange?.Invoke((INTERPOLATION)interIndex);
            }
        }


        private void OnOpacityCutoffInput(float val) => VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(val / 255.0f);


        private void OnOpacityCutoffInput(string val) => VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(float.Parse(val) / 255.0f);


        private void OnSamplingQualityFactorInput(float val) => VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(val);


        private void OnSamplingQualityFactorInput(string val) => VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(float.Parse(val));


        private void OnLODDistancesInput(List<float> distances) => VisualizationParametersEvents.ViewLODDistancesChange?.Invoke(distances);


        private void OnHomogeneityToleranceInput(float val) => VisualizationParametersEvents.ViewHomogeneityToleranceChange?.Invoke((byte)Mathf.Clamp(val, 0, 255));


        private void OnHomogeneityToleranceInput(string val) => VisualizationParametersEvents.ViewHomogeneityToleranceChange?.Invoke((byte)Mathf.Clamp(int.Parse(val), 0, 255));


        private void OnVolumetricObjectScaleFactorInput(float val) => VisualizationParametersEvents.ViewVolumetricObjectScaleFactorChange?.Invoke(val);


        private void OnVolumetricObjectScaleFactorInput(string val) => VisualizationParametersEvents.ViewVolumetricObjectScaleFactorChange?.Invoke(float.Parse(val));


        /////////////////////////////////
        /// MODEL CALLBACKS
        /////////////////////////////////

        // do NOT set using value otherwise infinite event callbacks will occur!
        private void OnModelTFChange(TF new_tf, ITransferFunction _) => m_TFDropDown.SetValueWithoutNotify((int)new_tf);


        private void OnModelOpacityCutoffChange(float value)
        {
            int v = Mathf.RoundToInt(value * 255);
            m_OpacityCutoffSlider.SetValueWithoutNotify(v);
            m_OpacityCutoffInputField.SetTextWithoutNotify(v.ToString());
        }


        private void OnModelSamplingQualityFactorChange(float value)
        {
            m_SamplingQualityFactorSlider.SetValueWithoutNotify(value);
            m_SamplingQualityFactorInputField.SetTextWithoutNotify(value.ToString("0.00"));
        }


        private void OnLODDistanceControlPositionChange(float pos, int res_lvl)
        {
            m_LODDistances[res_lvl] = m_LODDistancesScale.I(pos * m_LODDistancesControlContainer.rect.width);

            VisualizationParametersEvents.ViewLODDistancesChange?.Invoke(m_LODDistances);
        }


        private void OnModelLODDistancesChange(List<float> distances)
        {
            // if this is the first time the LOD distances are set or a different dataset is loaded
            if (m_LODDistances.Count != distances.Count)
            {
                foreach (var cp in m_LODDistanceControls)
                {
                    cp.OnPositionChanged -= OnLODDistanceControlPositionChange;
                }

                m_LODDistances.Clear();
                m_LODDistanceControls.Clear();
                for (int i = 0; i <= distances.Count; ++i)
                {
                    if (i < distances.Count)
                    {
                        m_LODDistances.Add(distances[i]);
                        var cp = Instantiate(m_LODDistanceControlPrefab, m_LODDistancesControlContainer).GetComponent<LODDistanceControlPointUI>();
                        cp.Init(m_LODDistancesScale.F(distances[i]) / m_LODDistancesControlContainer.rect.width, i);
                        cp.OnPositionChanged += OnLODDistanceControlPositionChange;
                        m_LODDistanceControls.Add(cp);
                    }

                    var lodPanel = Instantiate(m_LODPanelPrefab, m_LODDistancesControlContainer).GetComponent<LODPanelComponent>();
                    Vector2 pos;
                    if (i == 0)
                    {
                        pos = new(
                            0,
                            m_LODDistancesScale.F(distances[i]) / m_LODDistancesControlContainer.rect.width
                        );
                    }
                    else if (i == distances.Count)
                    {
                        pos = new(
                            m_LODDistancesScale.F(distances[i - 1]) / m_LODDistancesControlContainer.rect.width,
                            1
                        );
                    }
                    else
                    {
                        pos = new(
                            m_LODDistancesScale.F(distances[i - 1]) / m_LODDistancesControlContainer.rect.width,
                            m_LODDistancesScale.F(distances[i]) / m_LODDistancesControlContainer.rect.width
                        );
                    }
                    lodPanel.Init(pos, m_LODDistancesControlContainer);
                    lodPanel.SetLODColor(i < m_LODColors.Count ? m_LODColors[i] : m_LODColors[^1]);
                    lodPanel.SetLODLevel(i);
                    m_LODPanels.Add(lodPanel);
                }
                UpdateLODDistanceAxisTicks(distances);
                return;
            }
            // otherwise if simply one or more LOD distances have changed (while their total number remained the same)
            for (int i = 0; i < distances.Count; ++i)
            {
                m_LODDistances[i] = distances[i];
                float x = m_LODDistancesScale.F(distances[i]) / m_LODDistancesControlContainer.rect.width;
                m_LODDistanceControls[i].SetPosition(x);

                m_LODPanels[i].SetEndPosition(x);
                m_LODPanels[i + 1].SetStartPosition(x);
            }
            UpdateLODDistanceAxisTicks(distances);
        }


        private void OnModelInterpolationChange(INTERPOLATION value) => m_InterpolationDropDown.SetValueWithoutNotify((int)value);


        private void OnModelHomogeneityToleranceChange(byte value)
        {
            m_HomogeneityToleranceSlider.SetValueWithoutNotify(value);
            m_HomogeneityToleranceInputField.SetTextWithoutNotify(value.ToString("0"));
        }


        private void OnModelVolumetricObjectScaleFactorChange(float value)
        {
            m_ObjectScaleSlider.SetValueWithoutNotify(value);
            m_ObjectScaleInputField.SetTextWithoutNotify(value.ToString("0.00"));
        }


        void OnSave()
        {
            m_ManagerUI.RequestFilesystemEntry(FilesystemExplorerMode.SAVE_VISUALIZATION_PARAMETERS);
            m_ManagerUI.FilesystemExplorerEntry += OnFilesystemExplorerSave;
        }


        private void OnFilesystemExplorerSave(string fp)
        {
            m_ManagerUI.FilesystemExplorerEntry -= OnFilesystemExplorerSave;
            if (String.IsNullOrWhiteSpace(fp))
            {
                return;
            }
            var visParams = new VisualizationParameters()
            {
                TF = m_TFDropDown.options[m_TFDropDown.value].text,
                OpacityCutoff = m_OpacityCutoffSlider.value,
                Interpolation = m_InterpolationDropDown.options[m_InterpolationDropDown.value].text,
                SamplingQualityFactor= m_SamplingQualityFactorSlider.value,
                LODDistances = m_LODDistances,
                HomogeneityTolerance = (byte)m_HomogeneityToleranceSlider.value,
                VolumetricObjectScaleFactor = m_ObjectScaleSlider.value,
            };
            visParams.Serialize(fp);
        }


        void OnLoad()
        {
            m_ManagerUI.RequestFilesystemEntry(FilesystemExplorerMode.SEARCH_VISUALIZATION_PARAMETERS);
            m_ManagerUI.FilesystemExplorerEntry += OnFilesystemExplorerLoad;
        }


        private void OnFilesystemExplorerLoad(string fp)
        {
            m_ManagerUI.FilesystemExplorerEntry -= OnFilesystemExplorerLoad;
            if (String.IsNullOrWhiteSpace(fp))
            {
                return;
            }
            var visParams = VisualizationParameters.Deserialize(fp);

            OnOpacityCutoffInput(visParams.OpacityCutoff);
            OnSamplingQualityFactorInput(visParams.SamplingQualityFactor);
            OnLODDistancesInput(visParams.LODDistances);
            OnHomogeneityToleranceInput(visParams.HomogeneityTolerance);
            OnVolumetricObjectScaleFactorInput(visParams.VolumetricObjectScaleFactor);
            OnTFDropDownChange(m_TFDropDown.options.FindIndex((m) => m.text == visParams.TF.ToString()));
            OnInterDropDownChange(m_InterpolationDropDown.options.FindIndex((m) => m.text == visParams.Interpolation.ToString()));
        }

    }
}
