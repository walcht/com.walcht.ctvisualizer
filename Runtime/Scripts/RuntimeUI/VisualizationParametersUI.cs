using System;
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

        /////////////////////////////////
        // CACHED COMPONENTS
        /////////////////////////////////
        int m_PrevTFIndex = -1;
        int m_PrevInterIndex = -1;


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

            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange += OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange += OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
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

            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelOpacityCutoffChange -= OnModelOpacityCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange -= OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange -= OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange -= OnModelInterpolationChange;
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
    }
}
