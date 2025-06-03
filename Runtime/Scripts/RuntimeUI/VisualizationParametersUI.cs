using System;
using System.IO;
using Newtonsoft.Json;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

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
        public float LODQualityFactor;
        public byte HomogeneityTolerance;


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
        [SerializeField] Slider m_LODQualityFactorSlider;
        [SerializeField] TMP_InputField m_LODQualityFactorInputField;
        [SerializeField] TMP_Dropdown m_InterpolationDropDown;
        [SerializeField] Slider m_HomogeneityToleranceSlider;
        [SerializeField] TMP_InputField m_HomogeneityToleranceInputField;

        [SerializeField] Button m_Save;
        [SerializeField] Button m_Load;

        /////////////////////////////////
        // CACHED COMPONENTS
        /////////////////////////////////
        int m_PrevTFIndex = -1;
        int m_PrevInterIndex = -1;

        private ManagerUI m_ManagerUI;

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

            m_OpacityCutoffSlider.minValue = 0.0f;
            m_OpacityCutoffSlider.maxValue = 1.0f;
            m_OpacityCutoffInputField.readOnly = false;
            m_OpacityCutoffInputField.contentType = TMP_InputField.ContentType.DecimalNumber;


            m_SamplingQualityFactorSlider.minValue = 0.5f;
            m_SamplingQualityFactorSlider.maxValue = 3.0f;
            m_SamplingQualityFactorInputField.readOnly = false;
            m_SamplingQualityFactorInputField.contentType = TMP_InputField.ContentType.DecimalNumber;

            m_LODQualityFactorSlider.minValue = 0.10f;
            m_LODQualityFactorSlider.maxValue = 5.00f;
            m_LODQualityFactorInputField.readOnly = false;
            m_LODQualityFactorInputField.contentType = TMP_InputField.ContentType.DecimalNumber;

            m_HomogeneityToleranceSlider.wholeNumbers = true;
            m_HomogeneityToleranceSlider.minValue = 0;
            m_HomogeneityToleranceSlider.maxValue = 255;
            m_HomogeneityToleranceInputField.readOnly = false;
            m_HomogeneityToleranceInputField.contentType = TMP_InputField.ContentType.IntegerNumber;
        }


        private void Start()
        {
            if (m_ManagerUI == null)
            {
                throw new Exception("Init has to be called before enabling this transfer function UI");
            }
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
            m_LODQualityFactorSlider.onValueChanged.AddListener(OnLODQualityFactorInput);
            m_LODQualityFactorInputField.onSubmit.AddListener(OnLODQualityFactorInput);
            m_HomogeneityToleranceSlider.onValueChanged.AddListener(OnHomogeneityToleranceInput);
            m_HomogeneityToleranceInputField.onSubmit.AddListener(OnHomogeneityToleranceInput);

            m_Save.onClick.AddListener(OnSave);
            m_Load.onClick.AddListener(OnLoad);

            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange += OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange += OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange += OnModelHomogeneityToleranceChange;
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
            m_LODQualityFactorSlider.onValueChanged.RemoveAllListeners();
            m_LODQualityFactorInputField.onSubmit.RemoveAllListeners();
            m_HomogeneityToleranceSlider.onValueChanged.RemoveAllListeners();
            m_HomogeneityToleranceInputField.onSubmit.RemoveAllListeners();

            m_Save.onClick.RemoveAllListeners();
            m_Load.onClick.RemoveAllListeners();

            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange -= OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange -= OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;
            VisualizationParametersEvents.ModelHomogeneityToleranceChange -= OnModelHomogeneityToleranceChange;
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


        private void OnOpacityCutoffInput(float val) => VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(val);


        private void OnOpacityCutoffInput(string val) => VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(float.Parse(val));


        private void OnSamplingQualityFactorInput(float val) => VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(val);


        private void OnSamplingQualityFactorInput(string val) => VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(float.Parse(val));


        private void OnLODQualityFactorInput(float val) => VisualizationParametersEvents.ViewLODQualityFactorChange?.Invoke(val);


        private void OnLODQualityFactorInput(string val) => VisualizationParametersEvents.ViewLODQualityFactorChange?.Invoke(float.Parse(val));


        private void OnHomogeneityToleranceInput(float val) => VisualizationParametersEvents.ViewHomogeneityToleranceChange?.Invoke((byte)Mathf.Clamp(val, 0, 255));


        private void OnHomogeneityToleranceInput(string val) => VisualizationParametersEvents.ViewHomogeneityToleranceChange?.Invoke((byte)Mathf.Clamp(int.Parse(val), 0, 255));


        /////////////////////////////////
        /// MODEL CALLBACKS
        /////////////////////////////////

        // do NOT set using value otherwise infinite event callbacks will occur!
        private void OnModelTFChange(TF new_tf, ITransferFunction _) => m_TFDropDown.SetValueWithoutNotify((int)new_tf);


        private void OnModelOpacityCutoffChange(float value)
        {
            m_OpacityCutoffSlider.SetValueWithoutNotify(value);
            m_OpacityCutoffInputField.SetTextWithoutNotify(value.ToString("0.00"));
        }


        private void OnModelSamplingQualityFactorChange(float value)
        {
            m_SamplingQualityFactorSlider.SetValueWithoutNotify(value);
            m_SamplingQualityFactorInputField.SetTextWithoutNotify(value.ToString("0.00"));
        }


        private void OnModelLODQualityFactorChange(float value)
        {
            m_LODQualityFactorSlider.SetValueWithoutNotify(value);
            m_LODQualityFactorInputField.SetTextWithoutNotify(value.ToString("0.00"));
        }


        private void OnModelInterpolationChange(INTERPOLATION value) => m_InterpolationDropDown.SetValueWithoutNotify((int)value);


        private void OnModelHomogeneityToleranceChange(byte value)
        {
            m_HomogeneityToleranceSlider.SetValueWithoutNotify(value);
            m_HomogeneityToleranceInputField.SetTextWithoutNotify(value.ToString("0"));
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
                LODQualityFactor = m_LODQualityFactorSlider.value,
                HomogeneityTolerance = (byte)m_HomogeneityToleranceSlider.value,
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
            OnLODQualityFactorInput(visParams.LODQualityFactor);
            OnHomogeneityToleranceInput(visParams.HomogeneityTolerance);
            OnTFDropDownChange(m_TFDropDown.options.FindIndex((m) => m.text == visParams.TF.ToString()));
            OnInterDropDownChange(m_InterpolationDropDown.options.FindIndex((m) => m.text == visParams.Interpolation.ToString()));
        }

    }
}
