using System;
using System.Collections.Generic;
using System.IO;
using UnityEngine;
using UnityEngine.UI;

namespace UnityCTVisualizer
{

    public enum FilesystemExplorerMode
    {
        CVDS,
        TF1D,
        VISUALIZATION_PARAMETERS,
    }

    public class FilesystemExplorerUI : MonoBehaviour
    {
        public event Action FilesystemExplorerExit;
        public event Action<string> FilesystemEntrySelection;

        [SerializeField]
        GameObject m_FilesystemEntry;

        [SerializeField, Tooltip("UI GameObject where filesystem entries are added as children")]
        RectTransform m_EntriesContainer;

        [SerializeField]
        Button m_Exit;

        private readonly List<GameObject> m_Entries = new();


        private void Awake()
        {
            Debug.Log($"CVDS datasets only accessible at: {Application.persistentDataPath}");
        }


        private void OnEnable()
        {
            m_Exit.onClick.AddListener(OnFilesystemExplorerExit);
        }


        private void OnDisable()
        {
            m_Exit.onClick.RemoveAllListeners();
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
                case FilesystemExplorerMode.CVDS:
                {
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
                    Debug.Log($"found {m_Entries.Count} CVDS directory path entries in: {Application.persistentDataPath}");
                    break;
                }
                case FilesystemExplorerMode.TF1D:
                {
                    foreach (string fp in Directory.EnumerateFiles(Application.persistentDataPath, ""))
                    {
                        var obj = Instantiate<GameObject>(m_FilesystemEntry, parent: m_EntriesContainer);
                        m_Entries.Add(obj);
                        obj.GetComponent<FilesystemExplorerEntry>().Init(Path.GetFileNameWithoutExtension(fp), mode,
                            () => OnFilesystemEntryClick(fp));
                    }
                    Debug.Log($"found {m_Entries.Count} 1D transfer function data entries in: {Application.persistentDataPath}");
                    break;
                }
                case FilesystemExplorerMode.VISUALIZATION_PARAMETERS:
                {
                    // TODO
                    break;
                }
                default:
                {
                    return;
                }
            }
        }


        private void OnFilesystemEntryClick(string fp) => FilesystemEntrySelection?.Invoke(fp);


        private void OnFilesystemExplorerExit() => FilesystemExplorerExit?.Invoke();
    }
}
