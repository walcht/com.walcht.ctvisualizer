using System;
using System.IO;
using System.Collections.Generic;
using TMPro;
using UnityEngine;
using UnityEngine.UI;
#if (UNITY_ANDROID && !UNITY_EDITOR)
using UnityEngine.Android;
#endif

namespace UnityCTVisualizer {
    public class ImporterUI : MonoBehaviour {

        [SerializeField] Button m_ImportBtn;
        [SerializeField] TMP_InputField m_FilepathInputField;
        [SerializeField] TMP_Dropdown m_NbrImporterThreadsDropDown;
        [SerializeField] TMP_Dropdown m_BrickSizeDropDown;
        [SerializeField] TMP_Dropdown m_HighestResLvlDropDown;
        [SerializeField] ProgressHandler m_ProgressHandler;

        CVDSMetadata m_CurrentMetadata;
        Color m_DefaultTextColor;

        void OnEnable() {
            // make sure that initially the progress handler is disabled
            m_ProgressHandler.gameObject.SetActive(false);
            // disable import button
            m_ImportBtn.interactable = false;
            m_FilepathInputField.onSubmit.AddListener(OnFilepathSubmit);
            m_ImportBtn.onClick.AddListener(OnImportClick);
        }

        void OnDisable() {
            m_FilepathInputField.onSubmit.RemoveListener(OnFilepathSubmit);
            m_ImportBtn.onClick.RemoveListener(OnImportClick);
        }

#if (UNITY_ANDROID && !UNITY_EDITOR)
        internal void PermissionCallbacks_PermissionGranted(string permissionName)
        {
            Debug.Log("permission to read from the file system was granted");
        }

        internal void PermissionCallbacks_PermissionDenied(string permissionName)
        {
            throw new Exception($"permission to read from the file system was NOT granted. Aborting ...")
        }
#endif

        void Awake() {
          m_DefaultTextColor = m_FilepathInputField.textComponent.color;
#if (UNITY_ANDROID && !UNITY_EDITOR)
          var callbacks = new PermissionCallbacks();
          callbacks.PermissionDenied += PermissionCallbacks_PermissionDenied;
          callbacks.PermissionGranted += PermissionCallbacks_PermissionGranted;
          // request permissions
          if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead))
              Permission.RequestUserPermission(Permission.ExternalStorageRead);
#endif
        }

        /////////////////////////////////
        /// UI CALLBACKS (VIEW INVOKES)
        /////////////////////////////////
        void OnFilepathSubmit(string fp) {
            try {
              m_CurrentMetadata = Importer.ImportMetadata(fp);

              // fill the allowed brick size options
              List<TMP_Dropdown.OptionData> nbrImporterThreadsOptions = new();
              for (int i = 1; i <= Math.Max(Environment.ProcessorCount - 2, 1); ++i)
                  nbrImporterThreadsOptions.Add(new(i.ToString()));
              m_NbrImporterThreadsDropDown.options = nbrImporterThreadsOptions;

              // fill the allowed number of importer threads options
              List<TMP_Dropdown.OptionData> brickSizeOptions = new();
              for (int i = 32; i <= m_CurrentMetadata.ChunkSize; i <<= 1)
                  brickSizeOptions.Add(new(i.ToString()));
              m_BrickSizeDropDown.options = brickSizeOptions;

              // fill the allowed highest resolution level options
              List<TMP_Dropdown.OptionData> highestResLvlOptions = new();
              for (int i = 0; i < m_CurrentMetadata.NbrResolutionLvls; ++i)
                  highestResLvlOptions.Add(new(i.ToString()));
              m_HighestResLvlDropDown.options = highestResLvlOptions;

              m_ImportBtn.interactable = true;
              m_NbrImporterThreadsDropDown.interactable = true;
              m_BrickSizeDropDown.interactable = true;
              m_HighestResLvlDropDown.interactable = true;
              m_FilepathInputField.textComponent.color = m_DefaultTextColor;

              Debug.Log($"successfully imported CVDS metadta from: {fp}");
            }
            catch (Exception) {
              m_CurrentMetadata = null;
              m_ImportBtn.interactable = false;
              m_NbrImporterThreadsDropDown.interactable = false;
              m_BrickSizeDropDown.interactable = false;
              m_HighestResLvlDropDown.interactable = false;
              m_FilepathInputField.textComponent.color = Color.red;
              Debug.LogError($"failed to import CVDS metadta from: {fp}");
            }
        }

        void OnImportClick() => InitializationEvents.OnMetadataImport?.Invoke(
            new Tuple<CVDSMetadata, VolumeInitializationParams>(
              m_CurrentMetadata,
              new VolumeInitializationParams() {
                brickSize = Int32.Parse(m_BrickSizeDropDown.options[m_BrickSizeDropDown.value].text),
                highestResolutionLvl = Int32.Parse(m_HighestResLvlDropDown.options[m_HighestResLvlDropDown.value].text),
                nbrImporterThreads =
                  Int32.Parse(m_NbrImporterThreadsDropDown.options[m_NbrImporterThreadsDropDown.value].text)
              })
            );
    }
}
