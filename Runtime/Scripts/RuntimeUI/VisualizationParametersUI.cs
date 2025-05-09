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
        // TRILINEAR_POST_CLASSIFICATION, - yields worse performance and worse visuals
    }

    public class VisualizationParametersUI : MonoBehaviour
    {

        /////////////////////////////////
        // UI MODIFIERS
        /////////////////////////////////
        [SerializeField] TMP_Dropdown m_TFDropDown;
        [SerializeField] Slider m_AlphaCutoffSlider;
        [SerializeField] Slider m_SamplingQualityFactorSlider;
        [SerializeField] Slider m_LODQualityFactorSlider;
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

            m_InterpolationDropDown.onValueChanged.AddListener(OnInterDropDownChange);
            m_AlphaCutoffSlider.onValueChanged.AddListener(OnAlphaCutoffSliderChange);
            m_SamplingQualityFactorSlider.onValueChanged.AddListener(OnSamplingQualityFactorSliderChange);
            m_LODQualityFactorSlider.onValueChanged.AddListener(OnLODQualityFactorSliderChange);
        }

        private void OnEnable()
        {
            VisualizationParametersEvents.ModelTFChange += OnModelTFChange;
            VisualizationParametersEvents.ModelAlphaCutoffChange += OnModelAlphaCutoffChange;
            VisualizationParametersEvents.ModelSamplingQualityFactorChange += OnModelSamplingQualityFactorChange;
            VisualizationParametersEvents.ModelLODQualityFactorChange += OnModelLODQualityFactorChange;
            VisualizationParametersEvents.ModelInterpolationChange += OnModelInterpolationChange;
        }

        private void OnDisable()
        {
            m_InterpolationDropDown.onValueChanged.RemoveAllListeners();
            m_AlphaCutoffSlider.onValueChanged.RemoveAllListeners();
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

        private void OnInterDropDownChange(int interIndex)
        {
            if (interIndex != m_PrevInterIndex)
            {
                m_PrevInterIndex = interIndex;
                VisualizationParametersEvents.ViewInterpolationChange?.Invoke((INTERPOLATION)interIndex);
            }
        }

        private void OnAlphaCutoffSliderChange(float newVal)
        {
            VisualizationParametersEvents.ViewAlphaCutoffChange?.Invoke(newVal);
        }

        private void OnSamplingQualityFactorSliderChange(float newVal)
        {
            VisualizationParametersEvents.ViewSamplingQualityFactorChange?.Invoke(newVal);
        }

        private void OnLODQualityFactorSliderChange(float val)
        {
            VisualizationParametersEvents.ViewLODQualityFactorChange?.Invoke(val);
        }


        /////////////////////////////////
        /// MODEL CALLBACKS
        /////////////////////////////////

        private void OnModelTFChange(TF new_tf, ITransferFunction _)
        {
            // do NOT set using value otherwise infinite event callbacks will occur!
            m_TFDropDown.SetValueWithoutNotify((int)new_tf);
        }


        private void OnModelAlphaCutoffChange(float value)
        {
            m_AlphaCutoffSlider.SetValueWithoutNotify(value);
        }

        private void OnModelSamplingQualityFactorChange(float value)
        {
            m_SamplingQualityFactorSlider.SetValueWithoutNotify(value);
        }

        private void OnModelLODQualityFactorChange(float value)
        {
            m_LODQualityFactorSlider.SetValueWithoutNotify(value);
        }


        private void OnModelInterpolationChange(INTERPOLATION value)
        {
            m_InterpolationDropDown.SetValueWithoutNotify((int)value);
        }
    }
}
