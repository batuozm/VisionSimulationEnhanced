using UnityEngine;

[DisallowMultipleComponent]
public class ImprovedFocusProvider : MonoBehaviour
{
    public Defocus defocus;

    [Header("Debug")]
    public bool drawDebugRay = true;
    public float debugRayLength = 10f;
    public float maxFocusRayDistance = 100f;

    [Header("Stabilization")]
    public float distanceDeadband = 0.20f;
    public float candidateConfirmationTime = 0.08f;
    public float distanceLerpSpeed = 12f;

    private EyeTrackingToolbox toolbox;

    private bool hasConfirmedFocus = false;
    private float confirmedDistance = 0f;
    private Collider confirmedCollider = null;

    private Collider candidateCollider = null;
    private float candidateDistance = 0f;
    private float candidateStartTime = 0f;

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
            Debug.LogError("ImprovedFocusProvider: EyeTrackingToolbox.Instance not found.");
        }

        if (defocus == null)
        {
            Debug.LogError("ImprovedFocusProvider: Defocus reference missing.");
            return;
        }

        // Improved-Skript setzt die Fokusdistanz selbst.
        defocus.useMouse = false;
        defocus.preferExternalRay = false;
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

        bool hasHit = Physics.Raycast(focusRay, out RaycastHit hit, maxFocusRayDistance);

        if (drawDebugRay)
        {
            float rayLength = hasHit ? hit.distance : debugRayLength;
            Debug.DrawRay(focusRay.origin, focusRay.direction * rayLength, hasHit ? Color.cyan : Color.yellow);
        }

        if (!hasHit)
        {
            ApplyConfirmedFocus();
            return;
        }

        float hitDistance = hit.distance;
        Collider hitCollider = hit.collider;

        if (!hasConfirmedFocus)
        {
            ConfirmFocus(hitCollider, hitDistance);
            ApplyConfirmedFocus();
            return;
        }

        bool sameObject = hitCollider == confirmedCollider;
        float distanceDelta = Mathf.Abs(hitDistance - confirmedDistance);

        // Kleine Distanzänderungen oder derselbe Collider => Fokus halten / sanft nachführen
        if (sameObject || distanceDelta <= distanceDeadband)
        {
            confirmedDistance = Damp(confirmedDistance, hitDistance, distanceLerpSpeed);
            if (sameObject)
            {
                confirmedCollider = hitCollider;
            }

            ClearCandidate();
            ApplyConfirmedFocus();
            return;
        }

        // Neuer Kandidat
        if (hitCollider != candidateCollider)
        {
            candidateCollider = hitCollider;
            candidateDistance = hitDistance;
            candidateStartTime = Time.time;

            ApplyConfirmedFocus();
            return;
        }

        // Kandidat bleibt stabil -> nach kurzer Bestätigung übernehmen
        candidateDistance = Damp(candidateDistance, hitDistance, distanceLerpSpeed);

        if (Time.time - candidateStartTime >= candidateConfirmationTime)
        {
            ConfirmFocus(candidateCollider, candidateDistance);
        }

        ApplyConfirmedFocus();
    }

    private void ConfirmFocus(Collider col, float dist)
    {
        hasConfirmedFocus = true;
        confirmedCollider = col;
        confirmedDistance = Mathf.Max(0.0001f, dist);
        ClearCandidate();
    }

    private void ClearCandidate()
    {
        candidateCollider = null;
        candidateDistance = 0f;
        candidateStartTime = 0f;
    }

    private void ApplyConfirmedFocus()
    {
        if (hasConfirmedFocus)
        {
            defocus.SetFocusDistance(confirmedDistance);
        }
    }

    private float Damp(float current, float target, float speed)
    {
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        return Mathf.Lerp(current, target, t);
    }
}