using UnityEngine;

[DisallowMultipleComponent]
[RequireComponent(typeof(Camera))]
public class SphericalRefractiveError : MonoBehaviour
{
    public Defocus defocus;
    public Shader refractiveBlurShader;

    [Header("Enable")]
    public bool enableRefractiveError = true;

    [Header("Clinical Refraction (Diopters)")]
    [Tooltip("Negative = myopia, positive = hyperopia")]
    public float leftSphereDiopters = 0.0f;

    [Tooltip("Negative = myopia, positive = hyperopia")]
    public float rightSphereDiopters = 0.0f;

    [Header("Accommodation (Linear Model)")]
    [Tooltip("Maximum accommodation amplitude for the left eye in diopters")]
    public float leftMaxAccommodationDiopters = 4.0f;

    [Tooltip("Maximum accommodation amplitude for the right eye in diopters")]
    public float rightMaxAccommodationDiopters = 4.0f;

    [Tooltip("Linear accommodation speed in diopters per second")]
    public float accommodationSpeedDioptersPerSecond = 8.0f;

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
        accommodationSpeedDioptersPerSecond = Mathf.Max(0.0f, accommodationSpeedDioptersPerSecond);
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
        accommodationSpeedDioptersPerSecond = Mathf.Max(0.0f, accommodationSpeedDioptersPerSecond);
        sharpnessToleranceDiopters = Mathf.Max(0.0f, sharpnessToleranceDiopters);
        pixelsPerDiopter = Mathf.Max(0.0f, pixelsPerDiopter);
        maxBlurRadiusPixels = Mathf.Max(0.0f, maxBlurRadiusPixels);

        if (!Application.isPlaying)
        {
            float demand = GetCurrentDemand();
            currentAccommodationLeft = ComputeTargetAccommodation(demand, leftSphereDiopters, leftMaxAccommodationDiopters);
            currentAccommodationRight = ComputeTargetAccommodation(demand, rightSphereDiopters, rightMaxAccommodationDiopters);

            float effectiveLeft = ComputeEffectivePower(leftSphereDiopters, currentAccommodationLeft);
            float effectiveRight = ComputeEffectivePower(rightSphereDiopters, currentAccommodationRight);

            residualLeftDiopters = ApplySharpnessTolerance(effectiveLeft - demand, sharpnessToleranceDiopters);
            residualRightDiopters = ApplySharpnessTolerance(effectiveRight - demand, sharpnessToleranceDiopters);
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

    private void InitializeAccommodationFromCurrentDemand()
    {
        float demand = GetCurrentDemand();
        currentAccommodationLeft = ComputeTargetAccommodation(demand, leftSphereDiopters, leftMaxAccommodationDiopters);
        currentAccommodationRight = ComputeTargetAccommodation(demand, rightSphereDiopters, rightMaxAccommodationDiopters);

        float effectiveLeft = ComputeEffectivePower(leftSphereDiopters, currentAccommodationLeft);
        float effectiveRight = ComputeEffectivePower(rightSphereDiopters, currentAccommodationRight);

        residualLeftDiopters = ApplySharpnessTolerance(effectiveLeft - demand, sharpnessToleranceDiopters);
        residualRightDiopters = ApplySharpnessTolerance(effectiveRight - demand, sharpnessToleranceDiopters);
    }

    private void UpdateAccommodationModel()
    {
        float demand = GetCurrentDemand();

        float targetAccommodationLeft = ComputeTargetAccommodation(
            demand,
            leftSphereDiopters,
            leftMaxAccommodationDiopters
        );

        float targetAccommodationRight = ComputeTargetAccommodation(
            demand,
            rightSphereDiopters,
            rightMaxAccommodationDiopters
        );

        if (Application.isPlaying)
        {
            float maxStep = accommodationSpeedDioptersPerSecond * Time.deltaTime;

            currentAccommodationLeft = Mathf.MoveTowards(
                currentAccommodationLeft,
                targetAccommodationLeft,
                maxStep
            );

            currentAccommodationRight = Mathf.MoveTowards(
                currentAccommodationRight,
                targetAccommodationRight,
                maxStep
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

    private float ComputeTargetAccommodation(float demand, float sphere, float maxAccommodation)
    {
        // Linear model:
        // requiredAccommodation = demand + sphere
        return Mathf.Clamp(demand + sphere, 0.0f, maxAccommodation);
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
            $"L Sphere={leftSphereDiopters:F2}, R Sphere={rightSphereDiopters:F2}, " +
            $"L Acc={currentAccommodationLeft:F2}, R Acc={currentAccommodationRight:F2}, " +
            $"L Residual={residualLeftDiopters:F2}, R Residual={residualRightDiopters:F2}"
        );
    }
}