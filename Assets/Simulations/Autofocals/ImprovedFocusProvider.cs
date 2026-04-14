using System.Collections.Generic;
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

    [Header("Hysteresis")]
    [Tooltip("New object must be this much stronger than the currently confirmed object before switching is allowed.")]
    public float switchScoreBias = 1.30f;

    [Header("Distance Gating")]
    [Tooltip("If a new object is much farther/closer away than the currently confirmed focus, confirmation time is increased.")]
    public float switchDistanceGate = 1.00f;

    [Tooltip("Multiplier for confirmation time on large depth jumps.")]
    public float farJumpConfirmationMultiplier = 1.75f;

    [Header("Weighted Ray Cone")]
    [Tooltip("Angular radius of the ring rays in degrees.")]
    public float coneAngleDegrees = 0.35f;

    [Tooltip("Weight of the center ray.")]
    public float centerRayWeight = 1.0f;

    [Tooltip("Weight of each ring ray.")]
    public float ringRayWeight = 0.35f;

    private EyeTrackingToolbox toolbox;

    private bool hasConfirmedFocus = false;
    private float confirmedDistance = 0f;
    private Collider confirmedCollider = null;

    private Collider candidateCollider = null;
    private float candidateDistance = 0f;
    private float candidateStartTime = 0f;
    private float candidateRequiredTime = 0f;

    private struct HitAggregate
    {
        public float score;
        public float weightedDistanceSum;

        public float MeanDistance
        {
            get
            {
                return score > 0.0001f ? weightedDistanceSum / score : 0f;
            }
        }

        public void Add(float weight, float distance)
        {
            score += weight;
            weightedDistanceSum += weight * distance;
        }
    }

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

        // Improved provider controls focus itself.
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

        if (!TryGetBestConeHit(focusRay, out Collider bestCollider, out float bestDistance, out float bestScore, out Dictionary<Collider, HitAggregate> hitMap))
        {
            ApplyConfirmedFocus();
            return;
        }

        if (drawDebugRay)
        {
            float rayLength = hasConfirmedFocus ? confirmedDistance : bestDistance;
            if (rayLength <= 0.0001f)
            {
                rayLength = debugRayLength;
            }

            Debug.DrawRay(focusRay.origin, focusRay.direction * rayLength, Color.cyan);
        }

        if (!hasConfirmedFocus)
        {
            ConfirmFocus(bestCollider, bestDistance);
            ApplyConfirmedFocus();
            return;
        }

        // Same object: keep and gently update distance.
        if (bestCollider == confirmedCollider)
        {
            confirmedDistance = Damp(confirmedDistance, bestDistance, distanceLerpSpeed);
            ClearCandidate();
            ApplyConfirmedFocus();
            return;
        }

        // Hysteresis: prefer the currently confirmed object if it is still supported.
        float confirmedScore = 0f;
        float confirmedSeenDistance = confirmedDistance;
        bool confirmedStillVisible = false;

        if (confirmedCollider != null && hitMap.TryGetValue(confirmedCollider, out HitAggregate confirmedHit))
        {
            confirmedStillVisible = true;
            confirmedScore = confirmedHit.score;
            confirmedSeenDistance = confirmedHit.MeanDistance;
        }

        float distanceDelta = Mathf.Abs(bestDistance - confirmedDistance);
        bool withinDeadband = distanceDelta <= distanceDeadband;

        if (confirmedStillVisible)
        {
            bool challengerStrongEnough = bestScore >= Mathf.Max(0.0001f, confirmedScore) * switchScoreBias;

            if (!challengerStrongEnough || withinDeadband)
            {
                confirmedDistance = Damp(confirmedDistance, confirmedSeenDistance, distanceLerpSpeed);
                ClearCandidate();
                ApplyConfirmedFocus();
                return;
            }
        }

        // Distance gating: large depth jumps need longer confirmation.
        float requiredConfirmation = candidateConfirmationTime;
        if (distanceDelta > switchDistanceGate)
        {
            requiredConfirmation *= farJumpConfirmationMultiplier;
        }

        // Candidate logic
        if (bestCollider != candidateCollider)
        {
            candidateCollider = bestCollider;
            candidateDistance = bestDistance;
            candidateStartTime = Time.time;
            candidateRequiredTime = requiredConfirmation;

            ApplyConfirmedFocus();
            return;
        }

        candidateDistance = Damp(candidateDistance, bestDistance, distanceLerpSpeed);
        candidateRequiredTime = requiredConfirmation;

        if (Time.time - candidateStartTime >= candidateRequiredTime)
        {
            ConfirmFocus(candidateCollider, candidateDistance);
        }

        ApplyConfirmedFocus();
    }

    private bool TryGetBestConeHit(
        Ray baseRay,
        out Collider bestCollider,
        out float bestDistance,
        out float bestScore,
        out Dictionary<Collider, HitAggregate> hitMap)
    {
        bestCollider = null;
        bestDistance = 0f;
        bestScore = 0f;
        hitMap = new Dictionary<Collider, HitAggregate>();

        Vector3 origin = baseRay.origin;
        Vector3 forward = baseRay.direction.normalized;

        AddRayHit(origin, forward, centerRayWeight, hitMap);

        Vector3 upReference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upReference, forward));
        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        float tanAngle = Mathf.Tan(coneAngleDegrees * Mathf.Deg2Rad);

        for (int i = 0; i < 6; i++)
        {
            float angle = i * 60f * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);

            Vector3 offset = (right * x + up * y) * tanAngle;
            Vector3 dir = (forward + offset).normalized;

            AddRayHit(origin, dir, ringRayWeight, hitMap);
        }

        foreach (KeyValuePair<Collider, HitAggregate> kvp in hitMap)
        {
            float score = kvp.Value.score;
            if (score > bestScore)
            {
                bestCollider = kvp.Key;
                bestScore = score;
                bestDistance = kvp.Value.MeanDistance;
            }
        }

        return bestCollider != null;
    }

    private void AddRayHit(
        Vector3 origin,
        Vector3 direction,
        float weight,
        Dictionary<Collider, HitAggregate> hitMap)
    {
        if (weight <= 0f)
        {
            return;
        }

        if (!Physics.Raycast(origin, direction, out RaycastHit hit, maxFocusRayDistance))
        {
            return;
        }

        Collider col = hit.collider;

        if (hitMap.TryGetValue(col, out HitAggregate aggregate))
        {
            aggregate.Add(weight, hit.distance);
            hitMap[col] = aggregate;
        }
        else
        {
            HitAggregate newAggregate = new HitAggregate();
            newAggregate.Add(weight, hit.distance);
            hitMap.Add(col, newAggregate);
        }
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
        candidateRequiredTime = 0f;
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