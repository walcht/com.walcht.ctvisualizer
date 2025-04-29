using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace UnityCTVisualizer
{
    [RequireComponent(typeof(Button))]
    public class DisablableButtonUI : MonoBehaviour
    {
        private Color m_DisabledColor;
        private Color m_EnabledTextColor;
        private Color m_EnabledImageColor;

        private Button m_Btn;
        [SerializeField] private TMP_Text m_BtnText;
        [SerializeField] private Image m_BtnImg = null;


        private void Awake()
        {
            m_Btn = GetComponent<Button>();
            m_DisabledColor = m_Btn.colors.disabledColor;

            if (m_BtnText != null)
                m_EnabledTextColor = m_BtnText.color;

            if (m_BtnImg != null)
                m_EnabledImageColor = m_BtnImg.color;
        }


        public bool Interactable
        {
            get => m_Btn.interactable;
            set
            {
                m_Btn.interactable = value;
                if (value)
                {
                    if (m_BtnText != null)
                        m_BtnText.color = m_EnabledTextColor;

                    if (m_BtnImg != null)
                        m_BtnImg.color = m_EnabledImageColor;
                }
                else
                {
                    if (m_BtnText != null)
                        m_BtnText.color = m_DisabledColor;

                    if (m_BtnImg != null)
                        m_BtnImg.color = m_DisabledColor;
                }
            }
        }
    }
}
