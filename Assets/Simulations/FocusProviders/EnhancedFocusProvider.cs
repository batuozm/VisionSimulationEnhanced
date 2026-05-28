using System.Collections.Generic;
using UnityEngine;

[DisallowMultipleComponent]
public class EnhancedFocusProvider : FocusProviderBase
{
    [Header("Stabilization")]
    public float distanceDeadband = 0.20f;
    public float candidateConfirmationTime = 0.08f;
    public float distanceLerpSpeed = 12f;

    [Header("Hysteresis")]
    [Tooltip("New object must be this much stronger than the currently confirmed object before switching is allowed.")]
    public float switchScoreBias = 8.0f;

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

    public enum WideConeMode
    {
        Tight,
        Moderate,
        Wide
    }

    [Header("Wide Cone Retention")]
    public bool useWideConeRetention = true;

    [Tooltip("Tight = cone + 0.5°, Moderate = cone + 1.0°, Wide = cone + 1.5°")]
    public WideConeMode wideConeMode = WideConeMode.Tight;

    [Header("Vergence Retention")]
    [Range(0.0f, 0.5f)]
    [Tooltip("Relative tolerance based on the diopter difference between current focus and candidate. Example: 0.10 = 10%.")]
    public float vergenceHoldToleranceFraction = 0.10f;

    [Tooltip("Vergence distances below this value are ignored.")]
    public float minValidVergenceDistance = 0.20f;

    [Tooltip("Vergence distances above this value are ignored.")]
    public float maxValidVergenceDistance = 20.0f;

    [Tooltip("If current and candidate are too close in diopters, vergence is treated as not informative.")]
    public float minVergenceSeparationDiopters = 0.50f;

    // Read-only diagnostics for FocusMetricsRecorder.
    public bool LastFrameWideConeChecked { get; private set; }
    public bool LastFrameWideConeSawConfirmed { get; private set; }
    public bool LastFrameWideConeHeldFocus { get; private set; }
    public float LastFrameVergenceDiopters { get; private set; }

    private const int WideConeRayCount = 12;

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

    private void Update()
    {
        ResetFrameDiagnostics();

        if (!TryGetFocusRay(out Ray focusRay, out GazeData gazeData))
        {
            return;
        }

        if (!TryGetBestConeHit(
                focusRay,
                out Collider bestCollider,
                out float bestDistance,
                out float bestScore,
                out Dictionary<Collider, HitAggregate> hitMap))
        {
            ApplyConfirmedFocus();
            return;
        }

        float rayLength = hasConfirmedFocus ? confirmedDistance : bestDistance;
        if (rayLength <= 0.0001f)
        {
            rayLength = debugRayLength;
        }

        DrawFocusRay(focusRay, rayLength, Color.cyan);

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

        // Wide Cone Retention:
        // Only runs if the normal cone has completely lost support for the confirmed object.
        // The wide cone does not contribute to the normal score map.
        if (!confirmedStillVisible && useWideConeRetention && confirmedCollider != null)
        {
            LastFrameWideConeChecked = true;

            bool wideConeSeesConfirmed = WideConeSeesConfirmedTarget(focusRay, confirmedCollider);
            LastFrameWideConeSawConfirmed = wideConeSeesConfirmed;

            if (wideConeSeesConfirmed && VergenceClearlySupportsCurrent(gazeData.gazeDistance, confirmedDistance, bestDistance))
            {
                LastFrameWideConeHeldFocus = true;
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

        // Candidate logic.
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

    private bool WideConeSeesConfirmedTarget(Ray baseRay, Collider targetCollider)
    {
        if (targetCollider == null)
        {
            return false;
        }

        Vector3 origin = baseRay.origin;
        Vector3 forward = baseRay.direction.normalized;

        Vector3 upReference = Mathf.Abs(Vector3.Dot(forward, Vector3.up)) > 0.95f ? Vector3.right : Vector3.up;
        Vector3 right = Vector3.Normalize(Vector3.Cross(upReference, forward));
        Vector3 up = Vector3.Normalize(Vector3.Cross(forward, right));

        float tanAngle = Mathf.Tan(GetWideConeAngleDegrees() * Mathf.Deg2Rad);

        for (int i = 0; i < WideConeRayCount; i++)
        {
            float angle = i * (360f / WideConeRayCount) * Mathf.Deg2Rad;
            float x = Mathf.Cos(angle);
            float y = Mathf.Sin(angle);

            Vector3 offset = (right * x + up * y) * tanAngle;
            Vector3 dir = (forward + offset).normalized;

            if (Physics.Raycast(origin, dir, out RaycastHit hit, maxFocusRayDistance))
            {
                if (hit.collider == targetCollider)
                {
                    return true;
                }
            }
        }

        return false;
    }

    private float GetWideConeAngleDegrees()
    {
        switch (wideConeMode)
        {
            case WideConeMode.Tight:
                return coneAngleDegrees + 0.50f;

            case WideConeMode.Moderate:
                return coneAngleDegrees + 1.00f;

            case WideConeMode.Wide:
                return coneAngleDegrees + 1.50f;

            default:
                return coneAngleDegrees + 0.50f;
        }
    }

    private bool VergenceClearlySupportsCurrent(float gazeDistance, float currentDistance, float candidateDistance)
    {
        LastFrameVergenceDiopters = 0f;

        if (float.IsNaN(gazeDistance) || float.IsInfinity(gazeDistance))
        {
            return false;
        }

        if (gazeDistance < minValidVergenceDistance || gazeDistance > maxValidVergenceDistance)
        {
            return false;
        }

        if (currentDistance <= 0.0001f || candidateDistance <= 0.0001f)
        {
            return false;
        }

        float vergenceDpt = 1.0f / gazeDistance;
        float currentDpt = 1.0f / currentDistance;
        float candidateDpt = 1.0f / candidateDistance;

        LastFrameVergenceDiopters = vergenceDpt;

        float separation = Mathf.Abs(currentDpt - candidateDpt);

        if (separation < minVergenceSeparationDiopters)
        {
            return false;
        }

        float tolerance = separation * vergenceHoldToleranceFraction;

        if (currentDpt > candidateDpt)
        {
            // Current focus is nearer than the candidate.
            // Hold current if vergence remains close enough to current from below.
            // Values above currentDpt also support current, because the far candidate becomes even less plausible.
            return vergenceDpt >= currentDpt - tolerance;
        }

        if (currentDpt < candidateDpt)
        {
            // Current focus is farther than the candidate.
            // Hold current if vergence remains close enough to current from above.
            // Values below currentDpt also support current, because the near candidate becomes even less plausible.
            return vergenceDpt <= currentDpt + tolerance;
        }

        return false;
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
            ApplyFocusDistance(confirmedDistance);
        }
    }

    private float Damp(float current, float target, float speed)
    {
        float t = 1f - Mathf.Exp(-speed * Time.deltaTime);
        return Mathf.Lerp(current, target, t);
    }

    private void ResetFrameDiagnostics()
    {
        LastFrameWideConeChecked = false;
        LastFrameWideConeSawConfirmed = false;
        LastFrameWideConeHeldFocus = false;
        LastFrameVergenceDiopters = 0f;
    }
}