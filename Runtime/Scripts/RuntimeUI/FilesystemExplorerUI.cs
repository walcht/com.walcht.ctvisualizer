using System;
using System.Collections.Generic;
using System.IO;
using TMPro;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{

    public enum FilesystemExplorerMode
    {
        /// <summary>
        ///     Search for SEARCH_CVDS entries in Application.persistentDataPath.
        /// </summary>
        SEARCH_CVDS,

        /// <summary>
        ///     Search for serialized (i.e., saved) 1D transfer functions in Application.persistentDataPath.
        /// </summary>
        SEARCH_TF1D,

        /// <summary>
        ///     Search for serialized (i.e., saved) visualization parameters in Application.persistentDataPath.
        /// </summary>
        SEARCH_VISUALIZATION_PARAMETERS,

        /// <summary>
        ///     Request saving location for 1D transfer function.
        /// </summary>
        SAVE_TF1D,

        /// <summary>
        ///     Request saving location for visualization parameters.
        /// </summary>
        SAVE_VISUALIZATION_PARAMETERS,
    }

    public class FilesystemExplorerUI : MonoBehaviour
    {
        public event Action FilesystemExplorerExit;

        /// <summary>
        ///     Invoked when an entry from the filesystem explorer is selected or when
        ///     a save path is chosen. The file/directory is passed.
        /// </summary>
        public event Action<string> FilesystemEntrySelection;

        [SerializeField]
        GameObject m_FilesystemEntry;

        [SerializeField, Tooltip("UI GameObject where filesystem entries are added as children")]
        RectTransform m_EntriesContainer;

        [SerializeField]
        Button m_Exit;

        // filename of saved assets have a constant prefix and suffix, and a changeable part
        [SerializeField]
        TMP_Text m_SaveFilenameConstantPartPre;

        [SerializeField]
        TMP_Text m_SaveFilenameConstantPartSuf;

        [SerializeField]
        TMP_InputField m_SaveFilename;

        [SerializeField]
        Button m_Save;

        [SerializeField]
        Button m_GenerateFilename;

        [SerializeField]
        GameObject m_SaveWidgetsContainer;


        private readonly List<GameObject> m_Entries = new();


        private void Awake()
        {
            Debug.Log($"CVDS datasets only accessible at: {Application.persistentDataPath}");
        }


        private void OnEnable()
        {
            m_Exit.onClick.AddListener(OnFilesystemExplorerExit);
            m_Save.onClick.AddListener(OnSaveClick);
            m_GenerateFilename.onClick.AddListener(OnGenerateFilenameClick);
        }


        private void OnDisable()
        {
            m_Exit.onClick.RemoveAllListeners();
            m_Save.onClick.RemoveAllListeners();
            m_GenerateFilename.onClick.RemoveAllListeners();
        }


        public void UpdateMode(FilesystemExplorerMode mode)
        {
            // clear current entries
            foreach (GameObject entry in m_Entries)
            {
                Destroy(entry);
            }
            m_Entries.Clear();

            // add new entries depending on the provided mode
            switch (mode)
            {
                case FilesystemExplorerMode.SEARCH_CVDS:
                {
                    HideSaveModeUIs();
                    foreach (string directoryPath in Directory.EnumerateDirectories(Application.persistentDataPath))
                    {
                        if (File.Exists(Path.Join(directoryPath, "metadata.json")))
                        {
                            var obj = Instantiate<GameObject>(m_FilesystemEntry, parent: m_EntriesContainer);
                            m_Entries.Add(obj);
                            obj.GetComponent<FilesystemExplorerEntry>().Init(new DirectoryInfo(directoryPath).Name,
                                mode, () => OnFilesystemEntryClick(directoryPath));
                        }
                    }
                    break;
                }
                case FilesystemExplorerMode.SEARCH_TF1D:
                {
                    HideSaveModeUIs();
                    foreach (string fp in Directory.EnumerateFiles(Application.persistentDataPath, "tf1d_*.json"))
                    {
                        var obj = Instantiate<GameObject>(m_FilesystemEntry, parent: m_EntriesContainer);
                        m_Entries.Add(obj);
                        obj.GetComponent<FilesystemExplorerEntry>().Init(Path.GetFileNameWithoutExtension(fp), mode,
                            () => OnFilesystemEntryClick(fp));
                    }
                    break;
                }
                case FilesystemExplorerMode.SEARCH_VISUALIZATION_PARAMETERS:
                {
                    HideSaveModeUIs();
                    foreach (string fp in Directory.EnumerateFiles(Application.persistentDataPath, "visualization_parameters_*.json"))
                    {
                        var obj = Instantiate<GameObject>(m_FilesystemEntry, parent: m_EntriesContainer);
                        m_Entries.Add(obj);
                        obj.GetComponent<FilesystemExplorerEntry>().Init(Path.GetFileNameWithoutExtension(fp), mode,
                            () => OnFilesystemEntryClick(fp));
                    }
                    break;
                }
                case FilesystemExplorerMode.SAVE_TF1D:
                {
                    ShowSaveModeUIs();
                    m_SaveFilenameConstantPartPre.text = "tf1d_";
                    m_SaveFilenameConstantPartSuf.text = ".json";
                    break;
                }
                case FilesystemExplorerMode.SAVE_VISUALIZATION_PARAMETERS:
                {
                    ShowSaveModeUIs();
                    m_SaveFilenameConstantPartPre.text = "visualization_parameters_";
                    m_SaveFilenameConstantPartSuf.text = ".json";
                    break;
                }
                default:
                {
                    return;
                }
            }
        }


        private void ShowSaveModeUIs()
        {
            m_SaveWidgetsContainer.SetActive(true);
        }


        private void HideSaveModeUIs()
        {
            m_SaveWidgetsContainer.SetActive(false);
        }


        private void OnFilesystemEntryClick(string fp) => FilesystemEntrySelection?.Invoke(fp);


        private void OnFilesystemExplorerExit() => FilesystemExplorerExit?.Invoke();


        private void OnSaveClick()
        {
            // this means that the save path SHOULD be a directory path
            if (String.IsNullOrWhiteSpace(m_SaveFilenameConstantPartSuf.text))
            {
                if (Directory.Exists(Path.Join(Application.persistentDataPath, m_SaveFilenameConstantPartPre.text + m_SaveFilename.text)))
                {
                    // TODO: use notfication UI to warn user about replacing a directory
                }
            }
            // this means the save path SHOULD be a file path
            else
            {
                string fp = Path.Join(Application.persistentDataPath, m_SaveFilenameConstantPartPre.text + m_SaveFilename.text + m_SaveFilenameConstantPartSuf.text);
                if (File.Exists(fp))
                {
                    // TODO: use notfication UI to warn user about replacing a file
                }
                FilesystemEntrySelection?.Invoke(fp);
            }
        }


        private void OnGenerateFilenameClick()
        {
            m_SaveFilename.text = DateTimeOffset.Now.ToUnixTimeMilliseconds().ToString();
        }
    }
}
