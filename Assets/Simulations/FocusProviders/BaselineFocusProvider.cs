using UnityEngine;

[DisallowMultipleComponent]
public class BaselineFocusProvider : FocusProviderBase
{
    private void Update()
    {
        if (!TryGetFocusRay(out Ray focusRay))
        {
            return;
        }

        DrawFocusRay(focusRay, debugRayLength, Color.cyan);

        if (!TryRaycast(focusRay, out RaycastHit hit))
        {
            return;
        }

        ApplyFocusDistance(hit.distance);
    }
}