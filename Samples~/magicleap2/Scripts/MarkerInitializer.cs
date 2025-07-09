using Unity.XR.CoreUtils;
using UnityEngine;
using UnityEngine.XR.OpenXR;
using MagicLeap.OpenXR.Features.MarkerUnderstanding;
using UnityCTVisualizer;

public class MarkerTrackerExample : MonoBehaviour
{
    [Tooltip("Set the XR Origin so that the marker appears relative to headset's origin. If null, the script will try to find the component automatically.")]
    [SerializeField] XROrigin XROrigin;

    [SerializeField] GameObject m_ManagerUIInstance;
    [SerializeField] VolumetricObjectCreator m_VolumetricObjectCreator;

    public ArucoType ArucoType = ArucoType.Dictionary_6x6_50;

    public MarkerDetectorProfile DetectorProfile = MarkerDetectorProfile.Default;

    private MarkerDetectorSettings _detectorSettings;
    private MagicLeapMarkerUnderstandingFeature _markerFeature;

    private VolumetricObject m_VolumetricObject = null;

    private void Awake()
    {
        m_ManagerUIInstance.SetActive(false);
    }


    private void Start()
    {
        _markerFeature = OpenXRSettings.Instance.GetFeature<MagicLeapMarkerUnderstandingFeature>();

        if (_markerFeature == null || _markerFeature.enabled == false)
        {
            Debug.LogError("The Magic Leap 2 Marker Understanding OpenXR Feature is missing or disabled enabled. Disabling Script.");
            this.enabled = false;
            return;
        }

        if (XROrigin == null)
        {
            Debug.LogError("No XR Origin Found, markers sample will not work. Disabling Script.");
            this.enabled = false;
        }

        // Create the Marker Detector Settings
        _detectorSettings = new MarkerDetectorSettings();

        // Configure a generic detector with QR and Aruco Detector settings 
        // _detectorSettings.QRSettings.EstimateQRLength = true;
        // _detectorSettings.ArucoSettings.EstimateArucoLength = true;
        _detectorSettings.ArucoSettings.ArucoType = ArucoType;

        _detectorSettings.MarkerDetectorProfile = DetectorProfile;

        // We use the same settings on all 3 of the 
        // different detectors and target the specific marker by setting the Marker Type before creating the detector 

        // Create Aruco detector
        _detectorSettings.MarkerType = MarkerType.Aruco;
        _markerFeature.CreateMarkerDetector(_detectorSettings);
    }


    private void OnEnable()
    {
        InitializationEvents.OnVolumetricObjectCreation += OnVolumetricObjectCreation;
    }


    private void OnDisable()
    {

        InitializationEvents.OnVolumetricObjectCreation -= OnVolumetricObjectCreation;
    }


    private void OnDestroy()
    {
        if (_markerFeature != null)
        {
            _markerFeature.DestroyAllMarkerDetectors();
        }
    }

    void Update()
    {
        // Update the marker detector
        _markerFeature.UpdateMarkerDetectors();

        // Iterate through all of the marker detectors
        for (int i = 0; i < _markerFeature.MarkerDetectors.Count; i++)
        {
            // Verify that the marker detector is running
            if (_markerFeature.MarkerDetectors[i].Status == MarkerDetectorStatus.Ready)
            {
                // Cycle through the detector's data and log it to the debug log
                MarkerDetector currentDetector = _markerFeature.MarkerDetectors[i];
                OnUpdateDetector(currentDetector);
            }
        }
    }

    private bool m_FirstTime = true;
    private void OnUpdateDetector(MarkerDetector detector)
    {

        for (int i = 0; i < detector.Data.Count; i++)
        {
            var data = detector.Data[i];
            string id = data.MarkerNumber.ToString();

            if (!data.MarkerPose.HasValue)
            {
                Debug.Log("Marker Pose not estimated yet.");
                return;
            }

            if (!string.IsNullOrEmpty(id))
            {
                // Set the position of the marker. Since the pose is given relative to the XR Origin,
                // we need to transform it to world coordinates.
                var pos = XROrigin.CameraFloorOffsetObject.transform.TransformPoint(data.MarkerPose.Value.position);
                var rot = XROrigin.CameraFloorOffsetObject.transform.rotation * data.MarkerPose.Value.rotation;

                // If the marker ID has not been tracked create a new marker object
                if (m_FirstTime)
                {
                    m_VolumetricObjectCreator.m_InitialVolumetricObjectPosition = pos;
                    m_FirstTime = false;
                }

                m_ManagerUIInstance.transform.position = pos;
                m_ManagerUIInstance.transform.rotation = rot;

                if (m_VolumetricObject != null)
                {
                    m_VolumetricObject.transform.position = pos;
                    m_VolumetricObject.transform.rotation = rot;
                }

            }
        }
    }


    private void OnVolumetricObjectCreation(VolumetricObject volumetricObject) => m_VolumetricObject = volumetricObject;

}