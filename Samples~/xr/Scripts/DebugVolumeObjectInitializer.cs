using UnityEngine;
using UnityCTVisualizer;
#if UNITY_ANDROID
using UnityEngine.Android;
#endif

public class DebugVolumeObjectInitializer : MonoBehaviour
{
    public string m_DatasetPath = "/storage/emulated/0/CVDS Datasets/";
    public GameObject m_VolumetricObjectPrefab;

    private VolumetricObject m_VolumetricObject;
    private VolumetricDataset m_VolumetricDataset;
    void Start()
    {
#if UNITY_ANDROID
        // request permissions
        if (!Permission.HasUserAuthorizedPermission(Permission.ExternalStorageRead)) {
            Permission.RequestUserPermission(Permission.ExternalStorageRead);
        }
#endif

        // import dataset
        m_VolumetricDataset = ScriptableObject.CreateInstance<VolumetricDataset>();
        m_VolumetricDataset.Init(m_DatasetPath);
        Debug.Log($"dataset initialized successfully from: {m_DatasetPath}");

        // create volumetric object
        m_VolumetricObject = Instantiate<GameObject>(
                m_VolumetricObjectPrefab,
                position: Vector3.zero,
                rotation: Quaternion.identity
            )
            .GetComponent<VolumetricObject>();
        m_VolumetricObject.enabled = true;
        m_VolumetricObject.gameObject.transform.SetParent(transform, worldPositionStays: false);
        m_VolumetricObject.Init(m_VolumetricDataset, RenderingMode.IN_CORE, resolution_lvl: 0,
            progressHandler: null);
        m_VolumetricObject.enabled = true;
        Debug.Log("volumetric object created successfully");
    }
}
