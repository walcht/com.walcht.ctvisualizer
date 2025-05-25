using System;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{
    public class FilesystemExplorerEntry : MonoBehaviour
    {
        [SerializeField] Button m_Button;
        [SerializeField] TMP_Text m_Title;
        [SerializeField] Image m_Image;

        public void Init(string title, FilesystemExplorerMode entryType, Action clbk)
        {
            m_Title.text = title;
            m_Button.onClick.AddListener(() => clbk());
            switch (entryType)
            {
                case FilesystemExplorerMode.CVDS:
                {
                    break;
                }
                case FilesystemExplorerMode.TF1D:
                {
                    break;
                }
                case FilesystemExplorerMode.VISUALIZATION_PARAMETERS:
                {
                    break;
                }
                default:
                break;

            }
        }
    }
}
