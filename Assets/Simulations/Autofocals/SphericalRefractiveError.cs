using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class SphericalRefractiveError : MonoBehaviour
{
    public enum PresbyopiaStage
    {
        None,
        Early,
        Moderate,
        Advanced
    }

    public Defocus defocus;
    public Shader refractiveBlurShader;

    [Header("Enable")]
    public bool enableRefractiveError = true;

    [Header("Clinical Refraction (Diopters)")]
    [Tooltip("Negative = myopia, positive = hyperopia")]
    public float leftSphereDiopters = 0.0f;

    [Tooltip("Negative = myopia, positive = hyperopia")]
    public float rightSphereDiopters = 0.0f;

    [Header("Presbyopia")]
    public PresbyopiaStage presbyopiaStage = PresbyopiaStage.None;

    [Header("Accommodation Capacity (Base, before Presbyopia scaling)")]
    [Tooltip("Base maximum accommodation amplitude for the left eye in diopters")]
    public float leftMaxAccommodationDiopters = 6.0f;

    [Tooltip("Base maximum accommodation amplitude for the right eye in diopters")]
    public float rightMaxAccommodationDiopters = 6.0f;

    [Header("Paper-Inspired Accommodation Dynamics (Base)")]
    [Tooltip("Base time constant for far -> near focusing")]
    public float accommodationTauSeconds = 0.22f;

    [Tooltip("Base time constant for near -> far relaxation")]
    public float disaccommodationTauSeconds = 0.14f;

    [Tooltip("Small phasic boost for larger focus steps")]
    public float pulseGain = 0.60f;

    [Header("Subjective Sharpness")]
    [Tooltip("Residual refractive error inside this range is treated as subjectively sharp")]
    public float sharpnessToleranceDiopters = 0.25f;

    [Header("Blur Mapping")]
    [Tooltip("How many screen pixels of residual blur are added per diopter of residual error")]
    public float pixelsPerDiopter = 2.0f;

    [Tooltip("Clamp for the refractive blur radius in pixels")]
    public float maxBlurRadiusPixels = 12.0f;

    [Header("Debug")]
    public bool logOnChange = false;

    public float CurrentResidualLeftDiopters => residualLeftDiopters;
    public float CurrentResidualRightDiopters => residualRightDiopters;

    private Material blurMaterial;

    private float currentAccommodationLeft = 0.0f;
    private float currentAccommodationRight = 0.0f;

    private float residualLeftDiopters = 0.0f;
    private float residualRightDiopters = 0.0f;

    private float lastLoggedResidualLeft = float.NaN;
    private float lastLoggedResidualRight = float.NaN;

    private const float PresbyopiaLagStartDiopters = 1.0f;

    private struct PresbyopiaProfile
    {
        public float maxAccommodationMultiplier;
        public float responseGain;
        public float lagBase;
        public float lagPerDiopter;
        public float accommodationTauMultiplier;
        public float disaccommodationTauMultiplier;
    }

    private void Awake()
    {
        EnsureReferences();
    }

    private void OnEnable()
    {
        EnsureReferences();
        InitializeAccommodationFromCurrentDemand();
        ForceDisableLegacyDefocusOffset();
    }

    private void Update()
    {
        EnsureReferences();

        leftMaxAccommodationDiopters = Mathf.Max(0.0f, leftMaxAccommodationDiopters);
        rightMaxAccommodationDiopters = Mathf.Max(0.0f, rightMaxAccommodationDiopters);

        accommodationTauSeconds = Mathf.Max(0.001f, accommodationTauSeconds);
        disaccommodationTauSeconds = Mathf.Max(0.001f, disaccommodationTauSeconds);
        pulseGain = Mathf.Max(0.0f, pulseGain);

        sharpnessToleranceDiopters = Mathf.Max(0.0f, sharpnessToleranceDiopters);
        pixelsPerDiopter = Mathf.Max(0.0f, pixelsPerDiopter);
        maxBlurRadiusPixels = Mathf.Max(0.0f, maxBlurRadiusPixels);

        UpdateAccommodationModel();
        ForceDisableLegacyDefocusOffset();
        MaybeLog();
    }

    private void OnValidate()
    {
        EnsureReferences();

        leftMaxAccommodationDiopters = Mathf.Max(0.0f, leftMaxAccommodationDiopters);
        rightMaxAccommodationDiopters = Mathf.Max(0.0f, rightMaxAccommodationDiopters);

        accommodationTauSeconds = Mathf.Max(0.001f, accommodationTauSeconds);
        disaccommodationTauSeconds = Mathf.Max(0.001f, disaccommodationTauSeconds);
        pulseGain = Mathf.Max(0.0f, pulseGain);

        sharpnessToleranceDiopters = Mathf.Max(0.0f, sharpnessToleranceDiopters);
        pixelsPerDiopter = Mathf.Max(0.0f, pixelsPerDiopter);
        maxBlurRadiusPixels = Mathf.Max(0.0f, maxBlurRadiusPixels);

        if (!Application.isPlaying)
        {
            float demand = GetCurrentDemand();
            PresbyopiaProfile profile = GetPresbyopiaProfile();

            currentAccommodationLeft = ComputeTargetAccommodation(
                demand,
                leftSphereDiopters,
                leftMaxAccommodationDiopters,
                profile
            );

            currentAccommodationRight = ComputeTargetAccommodation(
                demand,
                rightSphereDiopters,
                rightMaxAccommodationDiopters,
                profile
            );

            float effectiveLeft = ComputeEffectivePower(leftSphereDiopters, currentAccommodationLeft);
            float effectiveRight = ComputeEffectivePower(rightSphereDiopters, currentAccommodationRight);

            residualLeftDiopters = ApplySharpnessTolerance(
                effectiveLeft - demand,
                sharpnessToleranceDiopters
            );

            residualRightDiopters = ApplySharpnessTolerance(
                effectiveRight - demand,
                sharpnessToleranceDiopters
            );
        }

        ForceDisableLegacyDefocusOffset();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        EnsureReferences();

        if (!enableRefractiveError || refractiveBlurShader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        float pupilScale = 1.0f;
        if (defocus != null)
        {
            pupilScale = Mathf.Max(0.25f, defocus.pupilSize / 5.0f);
        }

        float leftBlurRadiusPx = Mathf.Min(
            maxBlurRadiusPixels,
            Mathf.Abs(residualLeftDiopters) * pixelsPerDiopter * pupilScale
        );

        float rightBlurRadiusPx = Mathf.Min(
            maxBlurRadiusPixels,
            Mathf.Abs(residualRightDiopters) * pixelsPerDiopter * pupilScale
        );

        if (leftBlurRadiusPx < 0.01f && rightBlurRadiusPx < 0.01f)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (blurMaterial == null || blurMaterial.shader != refractiveBlurShader)
        {
            blurMaterial = new Material(refractiveBlurShader);
            blurMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        blurMaterial.SetFloat("_LeftBlurRadiusPx", leftBlurRadiusPx);
        blurMaterial.SetFloat("_RightBlurRadiusPx", rightBlurRadiusPx);

        Graphics.Blit(source, destination, blurMaterial, 0);
    }

    private void OnDisable()
    {
        ForceDisableLegacyDefocusOffset();

        if (blurMaterial != null)
        {
            DestroyImmediate(blurMaterial);
            blurMaterial = null;
        }
    }

    private void EnsureReferences()
    {
        if (defocus == null)
        {
            defocus = GetComponent<Defocus>();
        }

        if (refractiveBlurShader == null)
        {
            refractiveBlurShader = Shader.Find("Hidden/RefractiveErrorPSF");
        }
    }

    private void ForceDisableLegacyDefocusOffset()
    {
        if (defocus != null)
        {
            defocus.opticalPowerOffsetDiopters = 0.0f;
        }
    }

    private PresbyopiaProfile GetPresbyopiaProfile()
    {
        switch (presbyopiaStage)
        {
            case PresbyopiaStage.Early:
                return new PresbyopiaProfile
                {
                    maxAccommodationMultiplier = 0.80f,
                    responseGain = 0.92f,
                    lagBase = 0.10f,
                    lagPerDiopter = 0.08f,
                    accommodationTauMultiplier = 1.15f,
                    disaccommodationTauMultiplier = 1.05f
                };

            case PresbyopiaStage.Moderate:
                return new PresbyopiaProfile
                {
                    maxAccommodationMultiplier = 0.55f,
                    responseGain = 0.82f,
                    lagBase = 0.22f,
                    lagPerDiopter = 0.14f,
                    accommodationTauMultiplier = 1.40f,
                    disaccommodationTauMultiplier = 1.10f
                };

            case PresbyopiaStage.Advanced:
                return new PresbyopiaProfile
                {
                    maxAccommodationMultiplier = 0.30f,
                    responseGain = 0.68f,
                    lagBase = 0.35f,
                    lagPerDiopter = 0.20f,
                    accommodationTauMultiplier = 1.80f,
                    disaccommodationTauMultiplier = 1.20f
                };

            default:
                return new PresbyopiaProfile
                {
                    maxAccommodationMultiplier = 1.00f,
                    responseGain = 1.00f,
                    lagBase = 0.00f,
                    lagPerDiopter = 0.00f,
                    accommodationTauMultiplier = 1.00f,
                    disaccommodationTauMultiplier = 1.00f
                };
        }
    }

    private void InitializeAccommodationFromCurrentDemand()
    {
        float demand = GetCurrentDemand();
        PresbyopiaProfile profile = GetPresbyopiaProfile();

        currentAccommodationLeft = ComputeTargetAccommodation(
            demand,
            leftSphereDiopters,
            leftMaxAccommodationDiopters,
            profile
        );

        currentAccommodationRight = ComputeTargetAccommodation(
            demand,
            rightSphereDiopters,
            rightMaxAccommodationDiopters,
            profile
        );

        float effectiveLeft = ComputeEffectivePower(leftSphereDiopters, currentAccommodationLeft);
        float effectiveRight = ComputeEffectivePower(rightSphereDiopters, currentAccommodationRight);

        residualLeftDiopters = ApplySharpnessTolerance(
            effectiveLeft - demand,
            sharpnessToleranceDiopters
        );

        residualRightDiopters = ApplySharpnessTolerance(
            effectiveRight - demand,
            sharpnessToleranceDiopters
        );
    }

    private void UpdateAccommodationModel()
    {
        float demand = GetCurrentDemand();
        PresbyopiaProfile profile = GetPresbyopiaProfile();

        float targetAccommodationLeft = ComputeTargetAccommodation(
            demand,
            leftSphereDiopters,
            leftMaxAccommodationDiopters,
            profile
        );

        float targetAccommodationRight = ComputeTargetAccommodation(
            demand,
            rightSphereDiopters,
            rightMaxAccommodationDiopters,
            profile
        );

        if (Application.isPlaying)
        {
            currentAccommodationLeft = UpdateAccommodationState(
                currentAccommodationLeft,
                targetAccommodationLeft,
                profile
            );

            currentAccommodationRight = UpdateAccommodationState(
                currentAccommodationRight,
                targetAccommodationRight,
                profile
            );
        }
        else
        {
            currentAccommodationLeft = targetAccommodationLeft;
            currentAccommodationRight = targetAccommodationRight;
        }

        float effectiveLeft = ComputeEffectivePower(leftSphereDiopters, currentAccommodationLeft);
        float effectiveRight = ComputeEffectivePower(rightSphereDiopters, currentAccommodationRight);

        residualLeftDiopters = ApplySharpnessTolerance(
            effectiveLeft - demand,
            sharpnessToleranceDiopters
        );

        residualRightDiopters = ApplySharpnessTolerance(
            effectiveRight - demand,
            sharpnessToleranceDiopters
        );
    }

    private float GetCurrentDemand()
    {
        if (defocus == null)
        {
            return 0.0f;
        }

        return Mathf.Max(0.0f, defocus.opticalPower);
    }

    private float ComputeTargetAccommodation(
        float demand,
        float sphere,
        float baseMaxAccommodation,
        PresbyopiaProfile profile)
    {
        float stageMaxAccommodation = Mathf.Max(
            0.0f,
            baseMaxAccommodation * profile.maxAccommodationMultiplier
        );

        float safeMax = Mathf.Max(0.0001f, stageMaxAccommodation);
        float requiredAccommodation = Mathf.Max(0.0f, demand + sphere);

        // Reduced static response with increasing presbyopia
        float effectiveRequired = requiredAccommodation * profile.responseGain;

        // Additional near lag for presbyopia
        float lag = profile.lagBase +
                    profile.lagPerDiopter *
                    Mathf.Max(0.0f, requiredAccommodation - PresbyopiaLagStartDiopters);

        // Saturating static response
        float targetAccommodation =
            safeMax * (1.0f - Mathf.Exp(-effectiveRequired / safeMax)) - lag;

        return Mathf.Clamp(targetAccommodation, 0.0f, safeMax);
    }

    private float UpdateAccommodationState(
        float currentAccommodation,
        float targetAccommodation,
        PresbyopiaProfile profile)
    {
        float error = targetAccommodation - currentAccommodation;

        float baseTau = error > 0.0f
            ? accommodationTauSeconds * profile.accommodationTauMultiplier
            : disaccommodationTauSeconds * profile.disaccommodationTauMultiplier;

        float effectiveTau = baseTau / (1.0f + pulseGain * Mathf.Abs(error));
        effectiveTau = Mathf.Max(0.001f, effectiveTau);

        float alpha = 1.0f - Mathf.Exp(-Time.deltaTime / effectiveTau);
        return Mathf.Lerp(currentAccommodation, targetAccommodation, alpha);
    }

    private float ComputeEffectivePower(float sphere, float actualAccommodation)
    {
        // Baseline refractive state + current accommodation
        return -sphere + actualAccommodation;
    }

    private float ApplySharpnessTolerance(float residualError, float tolerance)
    {
        float magnitude = Mathf.Abs(residualError);

        if (magnitude <= tolerance)
        {
            return 0.0f;
        }

        return Mathf.Sign(residualError) * (magnitude - tolerance);
    }

    private void MaybeLog()
    {
        if (!logOnChange)
        {
            return;
        }

        bool changed =
            !Mathf.Approximately(residualLeftDiopters, lastLoggedResidualLeft) ||
            !Mathf.Approximately(residualRightDiopters, lastLoggedResidualRight);

        if (!changed)
        {
            return;
        }

        lastLoggedResidualLeft = residualLeftDiopters;
        lastLoggedResidualRight = residualRightDiopters;

        Debug.Log(
            $"SphericalRefractiveError | " +
            $"Stage={presbyopiaStage}, " +
            $"L Sphere={leftSphereDiopters:F2}, R Sphere={rightSphereDiopters:F2}, " +
            $"L Acc={currentAccommodationLeft:F2}, R Acc={currentAccommodationRight:F2}, " +
            $"L Residual={residualLeftDiopters:F2}, R Residual={residualRightDiopters:F2}"
        );
    }
}