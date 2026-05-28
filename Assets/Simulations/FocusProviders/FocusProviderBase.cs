using UnityEngine;

/// <summary>
/// Shared infrastructure for focus providers.
///
/// Responsibility:
/// - Access EyeTrackingToolbox.
/// - Resolve the Defocus component.
/// - Provide the current gaze ray in world space.
/// - Provide common raycast and Defocus output helpers.
///
/// This class does not implement a focus algorithm.
/// BaselineFocusProvider and EnhancedFocusProvider decide how the focus distance is selected.
/// </summary>
public abstract class FocusProviderBase : MonoBehaviour
{
    [Header("References")]
    public Defocus defocus;

    [Header("Raycast")]
    public float maxFocusRayDistance = 100f;

    [Header("Debug")]
    public bool drawDebugRay = true;
    public float debugRayLength = 10f;

    protected EyeTrackingToolbox toolbox;

    private const float MinRayDirectionSqrMagnitude = 0.0001f;
    private const float MinFocusDistance = 0.0001f;

    protected virtual void Awake()
    {
        ResolveDefocusReference();
    }

    protected virtual void Start()
    {
        toolbox = EyeTrackingToolbox.Instance;

        if (toolbox == null)
        {
            Debug.LogError($"{GetType().Name}: EyeTrackingToolbox.Instance not found.");
        }

        if (defocus == null)
        {
            Debug.LogError($"{GetType().Name}: Defocus reference missing.");
        }
    }

    protected virtual void OnValidate()
    {
        ResolveDefocusReference();

        maxFocusRayDistance = Mathf.Max(0.01f, maxFocusRayDistance);
        debugRayLength = Mathf.Max(0.01f, debugRayLength);
    }

    private void ResolveDefocusReference()
    {
        if (defocus == null)
        {
            defocus = GetComponent<Defocus>();
        }
    }

    protected bool IsReady()
    {
        return toolbox != null && defocus != null;
    }

    protected bool TryGetFocusRay(out Ray focusRay)
    {
        return TryGetFocusRay(out focusRay, out _);
    }

    protected bool TryGetFocusRay(out Ray focusRay, out GazeData gazeData)
    {
        focusRay = default(Ray);
        gazeData = default(GazeData);

        if (!IsReady())
        {
            return false;
        }

        gazeData = toolbox.GetGazeData();
        focusRay = gazeData.combinedRayWorld;

        return IsValidRay(focusRay);
    }

    protected bool TryRaycast(Ray focusRay, out RaycastHit hit)
    {
        hit = default(RaycastHit);

        if (!IsValidRay(focusRay))
        {
            return false;
        }

        return Physics.Raycast(focusRay, out hit, maxFocusRayDistance);
    }

    protected bool ApplyFocusDistance(float distanceMeters)
    {
        if (defocus == null || distanceMeters <= MinFocusDistance)
        {
            return false;
        }

        defocus.SetFocusDistance(distanceMeters);
        return true;
    }

    protected void DrawFocusRay(Ray focusRay, float length, Color color)
    {
        if (!drawDebugRay || !IsValidRay(focusRay))
        {
            return;
        }

        float safeLength = length > MinFocusDistance ? length : debugRayLength;
        Debug.DrawRay(focusRay.origin, focusRay.direction * safeLength, color);
    }

    protected static bool IsValidRay(Ray ray)
    {
        return IsFinite(ray.origin)
            && IsFinite(ray.direction)
            && ray.direction.sqrMagnitude > MinRayDirectionSqrMagnitude;
    }

    protected static bool IsFinite(Vector3 value)
    {
        return float.IsFinite(value.x)
            && float.IsFinite(value.y)
            && float.IsFinite(value.z);
    }

    protected static float OpticalPowerFromDistance(float distanceMeters)
    {
        if (distanceMeters <= MinFocusDistance)
        {
            return 0f;
        }

        return 1.0f / distanceMeters;
    }

    protected static string ColliderName(Collider collider)
    {
        return collider != null ? collider.name : "NA";
    }
}