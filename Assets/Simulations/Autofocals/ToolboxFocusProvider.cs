using UnityEngine;

[DisallowMultipleComponent]
public class ToolboxFocusProvider : MonoBehaviour
{
    public Defocus defocus;
    public bool useCombinedRay = true;
    public bool drawDebugRay = true;
    public float debugRayLength = 10f;

    private EyeTrackingToolbox toolbox;

    private void Awake()
    {
        if (defocus == null)
        {
            defocus = GetComponent<Defocus>();
        }
    }

    private void Start()
    {
        toolbox = EyeTrackingToolbox.Instance;

        if (toolbox == null)
        {
            Debug.LogError("ToolboxFocusProvider: EyeTrackingToolbox.Instance not found.");
        }

        if (defocus == null)
        {
            Debug.LogError("ToolboxFocusProvider: Defocus reference missing.");
        }
    }

    private void Update()
    {
        if (toolbox == null || defocus == null)
        {
            return;
        }

        GazeData gazeData = toolbox.GetGazeData();
        Ray focusRay = gazeData.combinedRayWorld;

        if (focusRay.direction.sqrMagnitude < 0.0001f)
        {
            return;
        }

        defocus.SetExternalFocusRay(focusRay, 0f);

        if (drawDebugRay)
        {
            Debug.DrawRay(focusRay.origin, focusRay.direction * debugRayLength, Color.cyan);
        }
    }
}