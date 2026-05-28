using UnityEngine;

/// <summary>
/// Camera image effect for eye-tracking based defocus rendering.
///
/// Responsibility:
/// - Receive a focus distance from a FocusProvider.
/// - Convert the focus distance to target optical power.
/// - Smoothly approach the target optical power.
/// - Render the defocus effect using Defocus.shader.
///
/// This component does not read gaze data, mouse input, or perform raycasts.
/// </summary>
[RequireComponent(typeof(Camera))]
public class Defocus : MonoBehaviour
{
    private const float MinFocusDistance = 0.0001f;

    public Shader defocusShader;
    public bool usePostPass = true;
    public bool showDepth = false;
    public bool showDistance = false;

    [Range(0.0f, 10.0f)]
    public float targetOpticalPower = 0.0f;

    public float opticalPower = 0.0f;

    public float powerChangePerSec = 8.0f;
    public float pupilSize = 5.0f;

    [Range(1f, 30f)]
    public float bokehRadius = 13.0f;

    [Range(1f, 30f)]
    public float cocConstant = 2.0f;

    public int downscaleFactor = 2;

    private Material defocusMaterial;
    private Camera cam;

    private const int CocPass = 0;
    private const int PreFilterPass = 1;
    private const int BlurPass = 2;
    private const int PostFilterPass = 3;
    private const int CombinePass = 4;
    private const int DepthPass = 5;
    private const int DistancePass = 6;

    private void Awake()
    {
        cam = GetComponent<Camera>();
    }

    private void Start()
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (cam != null)
        {
            cam.depthTextureMode = DepthTextureMode.Depth;
        }

        opticalPower = targetOpticalPower;
    }

    private void LateUpdate()
    {
        UpdateOpticalPower();
    }

    private void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (cam == null)
        {
            cam = GetComponent<Camera>();
        }

        if (cam == null || defocusShader == null)
        {
            Graphics.Blit(source, destination);
            return;
        }

        if (defocusMaterial == null || defocusMaterial.shader != defocusShader)
        {
            defocusMaterial = new Material(defocusShader);
            defocusMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        int safeDownscaleFactor = Mathf.Max(1, downscaleFactor);

        cocConstant = 0.057f * pupilSize * Mathf.Deg2Rad;
        cocConstant = Mathf.Tan(cocConstant / 2f) / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2f) * cam.pixelHeight;

        RenderTexture coc = RenderTexture.GetTemporary(
            source.width,
            source.height,
            0,
            RenderTextureFormat.RHalf,
            RenderTextureReadWrite.Linear
        );

        int downscaledWidth = Mathf.Max(1, source.width / safeDownscaleFactor);
        int downscaledHeight = Mathf.Max(1, source.height / safeDownscaleFactor);

        RenderTexture tmpTex1 = RenderTexture.GetTemporary(downscaledWidth, downscaledHeight, 0, source.format);
        RenderTexture tmpTex2 = RenderTexture.GetTemporary(downscaledWidth, downscaledHeight, 0, source.format);

        defocusMaterial.SetFloat("_OpticalPower", opticalPower);
        defocusMaterial.SetFloat("_CocConstant", cocConstant);
        defocusMaterial.SetFloat("_BokehRadius", bokehRadius);
        defocusMaterial.SetInt("_downscaleFactor", safeDownscaleFactor);
        defocusMaterial.SetTexture("_defocusTexture", coc);

        if (showDepth)
        {
            showDistance = false;
            Graphics.Blit(source, destination, defocusMaterial, DepthPass);
        }
        else if (showDistance)
        {
            showDepth = false;
            Graphics.Blit(source, destination, defocusMaterial, DistancePass);
        }
        else
        {
            Graphics.Blit(source, coc, defocusMaterial, CocPass);
            Graphics.Blit(source, tmpTex1, defocusMaterial, PreFilterPass);
            Graphics.Blit(tmpTex1, tmpTex2, defocusMaterial, BlurPass);

            RenderTexture finalBlurTex;
            if (usePostPass)
            {
                Graphics.Blit(tmpTex2, tmpTex1, defocusMaterial, PostFilterPass);
                finalBlurTex = tmpTex1;
            }
            else
            {
                finalBlurTex = tmpTex2;
            }

            defocusMaterial.SetTexture("_blurredTex", finalBlurTex);
            Graphics.Blit(source, destination, defocusMaterial, CombinePass);
        }

        RenderTexture.ReleaseTemporary(coc);
        RenderTexture.ReleaseTemporary(tmpTex1);
        RenderTexture.ReleaseTemporary(tmpTex2);
    }

    private void UpdateOpticalPower()
    {
        if (targetOpticalPower > opticalPower)
        {
            opticalPower += powerChangePerSec * Time.deltaTime;
            if (opticalPower > targetOpticalPower)
            {
                opticalPower = targetOpticalPower;
            }
        }
        else if (targetOpticalPower < opticalPower)
        {
            opticalPower -= powerChangePerSec * Time.deltaTime;
            if (opticalPower < targetOpticalPower)
            {
                opticalPower = targetOpticalPower;
            }
        }
    }

    public void SetFocusDistance(float focusDistance)
    {
        if (focusDistance <= MinFocusDistance)
        {
            return;
        }

        targetOpticalPower = 1.0f / focusDistance;
    }

    private void OnDisable()
    {
        if (defocusMaterial != null)
        {
            DestroyImmediate(defocusMaterial);
            defocusMaterial = null;
        }
    }
}