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
        }

        private void OnEnable()
        {
#if !UNITY_ANDROID
            m_PerspectiveToggle.onValueChanged.AddListener(OnPerspectiveToggleChange);
#else
            m_PerspectiveToggle.interactable = false;
#endif
            m_InterpolationDropDown.onValueChanged.AddListener(OnInterDropDownChange);
            m_OpacityCutoffSlider.onValueChanged.AddListener(OnOpacityCutoffSliderChange);
            m_SamplingQualityFactorSlider.onValueChanged.AddListener(OnSamplingQualityFactorSliderChange);
            m_LODQualityFactorSlider.onValueChanged.AddListener(OnLODQualityFactorSliderChange);

            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange += OnModelAlphaCutoffChange;
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
            m_SamplingQualityFactorSlider.onValueChanged.RemoveAllListeners();
            m_LODQualityFactorSlider.onValueChanged.RemoveAllListeners();

            VisualizationParametersEvents.ModelTFChange -= OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange -= OnModelAlphaCutoffChange;
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

        private void OnOpacityCutoffSliderChange(float val)
        {
            VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(val);
            m_OpacityCutoffInputField.text = val.ToString("0.00");
        }

        private void OnSamplingQualityFactorSliderChange(float val)
        {
            VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(val);
            m_SamplingQualityFactorInputField.text = val.ToString("0.00");
        }

        private void OnLODQualityFactorSliderChange(float val)
        {
            VisualizationParametersEvents.ViewLODQualityFactorChange?.Invoke(val);
            m_LODQualityFactorInputField.text = val.ToString("0.00");
        }


        /////////////////////////////////
        /// MODEL CALLBACKS
        /////////////////////////////////

        // do NOT set using value otherwise infinite event callbacks will occur!
        private void OnModelTFChange(TF new_tf, ITransferFunction _) => m_TFDropDown.SetValueWithoutNotify((int)new_tf);


        private void OnModelAlphaCutoffChange(float value)
        {
            m_OpacityCutoffSlider.SetValueWithoutNotify(value);
            m_OpacityCutoffInputField.text = value.ToString("0.00");
        }


        private void OnModelSamplingQualityFactorChange(float value)
        {
            m_SamplingQualityFactorSlider.SetValueWithoutNotify(value);
            m_SamplingQualityFactorInputField.text = value.ToString("0.00");
        }


        private void OnModelLODQualityFactorChange(float value)
        {
            m_LODQualityFactorSlider.SetValueWithoutNotify(value);
            m_LODQualityFactorInputField.text = value.ToString("0.00");
        }

        private void OnModelInterpolationChange(INTERPOLATION value) => m_InterpolationDropDown.SetValueWithoutNotify((int)value);
    }
}
