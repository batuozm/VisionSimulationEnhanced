/*using UnityEngine;
using VIVE.OpenXR;
using VIVE.OpenXR.EyeTracker;

[DisallowMultipleComponent]
public class ViveEyeTrackerFocusProvider : MonoBehaviour
{
    public Defocus defocus;
    public bool averageBothEyes = true;
    public bool drawDebugRay = true;
    public float debugRayLength = 10f;

    private void Awake()
    {
        if (defocus == null)
        {
            defocus = GetComponent<Defocus>();
        }
    }

    private void Update()
    {
        if (defocus == null)
        {
            return;
        }

        XR_HTC_eye_tracker.Interop.GetEyeGazeData(out XrSingleEyeGazeDataHTC[] gazes);

        if (gazes == null || gazes.Length <= (int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC)
        {
            return;
        }

        if (!TryBuildFocusRay(gazes, out Ray focusRay))
        {
            return;
        }

        // Für echten Eye-Tracking-Ray kein nearClipPlane-Offset addieren.
        defocus.SetExternalFocusRay(focusRay, 0f);

        if (drawDebugRay)
        {
            Debug.DrawRay(focusRay.origin, focusRay.direction * debugRayLength);
        }
    }

    private bool TryBuildFocusRay(XrSingleEyeGazeDataHTC[] gazes, out Ray focusRay)
    {
        XrSingleEyeGazeDataHTC left =
            gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_LEFT_HTC];

        XrSingleEyeGazeDataHTC right =
            gazes[(int)XrEyePositionHTC.XR_EYE_POSITION_RIGHT_HTC];

        if (averageBothEyes && left.isValid && right.isValid)
        {
            Vector3 leftOrigin = left.gazePose.position.ToUnityVector();
            Vector3 rightOrigin = right.gazePose.position.ToUnityVector();

            Vector3 leftForward = left.gazePose.orientation.ToUnityQuaternion() * Vector3.forward;
            Vector3 rightForward = right.gazePose.orientation.ToUnityQuaternion() * Vector3.forward;

            Vector3 origin = 0.5f * (leftOrigin + rightOrigin);
            Vector3 direction = (leftForward + rightForward).normalized;

            if (direction.sqrMagnitude < 0.0001f)
            {
                direction = leftForward.normalized;
            }

            focusRay = new Ray(origin, direction);
            return true;
        }

        if (left.isValid)
        {
            focusRay = BuildRayFromSingleEye(left);
            return true;
        }

        if (right.isValid)
        {
            focusRay = BuildRayFromSingleEye(right);
            return true;
        }

        focusRay = default;
        return false;
    }

    private Ray BuildRayFromSingleEye(XrSingleEyeGazeDataHTC eyeGaze)
    {
        Vector3 origin = eyeGaze.gazePose.position.ToUnityVector();
        Vector3 direction = eyeGaze.gazePose.orientation.ToUnityQuaternion() * Vector3.forward;
        return new Ray(origin, direction.normalized);
    }
} */