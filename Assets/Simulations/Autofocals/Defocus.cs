using System;
using UnityEngine;

public class Defocus : MonoBehaviour
{
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

    [Header("Focus Input")]
    public bool useMouse = true;
    public bool preferExternalRay = true;
    public float maxFocusRayDistance = 100f;

    [NonSerialized]
    Material defocusMaterial;

    private Camera cam;

    private bool hasExternalRayThisFrame = false;
    private Ray externalFocusRay;
    private float externalDistanceOffset = 0f;

    const int cocPass = 0;
    const int preFilterPass = 1;
    const int blurPass = 2;
    const int postFilterPass = 3;
    const int combinePass = 4;
    const int depthPass = 5;
    const int distancePass = 6;

    void Awake()
    {
        cam = GetComponent<Camera>();
    }

    void Start()
    {
        cam.depthTextureMode = DepthTextureMode.Depth;
        Debug.Log(cam.depthTextureMode);
        opticalPower = targetOpticalPower;
    }

    void OnRenderImage(RenderTexture source, RenderTexture destination)
    {
        if (defocusMaterial == null)
        {
            defocusMaterial = new Material(defocusShader);
            defocusMaterial.hideFlags = HideFlags.HideAndDontSave;
        }

        cocConstant = 0.057f * pupilSize * Mathf.Deg2Rad;
        cocConstant = Mathf.Tan(cocConstant / 2f) / Mathf.Tan(cam.fieldOfView * Mathf.Deg2Rad / 2f) * cam.pixelHeight;

        RenderTexture coc = RenderTexture.GetTemporary(
            source.width, source.height, 0,
            RenderTextureFormat.RHalf, RenderTextureReadWrite.Linear
        );

        int downscaledWidth = source.width / downscaleFactor;
        int downscaledHeight = source.height / downscaleFactor;
        RenderTexture tmpTex1 = RenderTexture.GetTemporary(downscaledWidth, downscaledHeight, 0, source.format);
        RenderTexture tmpTex2 = RenderTexture.GetTemporary(downscaledWidth, downscaledHeight, 0, source.format);

        defocusMaterial.SetTexture("_defocusTexture", coc);
        defocusMaterial.SetTexture("_blurredTex", tmpTex1);

        defocusMaterial.SetFloat("_OpticalPower", opticalPower);
        defocusMaterial.SetFloat("_CocConstant", cocConstant);
        defocusMaterial.SetFloat("_BokehRadius", bokehRadius);
        defocusMaterial.SetInt("_downscaleFactor", downscaleFactor);

        if (showDepth)
        {
            showDistance = false;
            Graphics.Blit(source, destination, defocusMaterial, depthPass);
        }
        else if (showDistance)
        {
            showDepth = false;
            Graphics.Blit(source, destination, defocusMaterial, distancePass);
        }
        else
        {
            Graphics.Blit(source, coc, defocusMaterial, cocPass);
            Graphics.Blit(source, tmpTex1, defocusMaterial, preFilterPass);
            Graphics.Blit(tmpTex1, tmpTex2, defocusMaterial, blurPass);

            if (usePostPass)
            {
                Graphics.Blit(tmpTex2, tmpTex1, defocusMaterial, postFilterPass);
            }
            else
            {
                Graphics.Blit(tmpTex2, tmpTex1);
            }

            Graphics.Blit(tmpTex2, destination);
        }

        RenderTexture.ReleaseTemporary(coc);
        RenderTexture.ReleaseTemporary(tmpTex1);
        RenderTexture.ReleaseTemporary(tmpTex2);
    }

    void Update()
    {
        UpdateOpticalPower();

        bool usedExternalRay = false;

        if (preferExternalRay && hasExternalRayThisFrame)
        {
            usedExternalRay = TrySetFocusFromRay(externalFocusRay, externalDistanceOffset);
        }

        if (!usedExternalRay && useMouse)
        {
            Ray mouseRay = cam.ScreenPointToRay(Input.mousePosition);
            TrySetFocusFromRay(mouseRay, cam.nearClipPlane);
        }

        hasExternalRayThisFrame = false;
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

    private bool TrySetFocusFromRay(Ray ray, float distanceOffset)
    {
        if (Physics.Raycast(ray, out RaycastHit hit, maxFocusRayDistance))
        {
            float focusDistance = hit.distance + distanceOffset;
            SetFocusDistance(focusDistance);
            return true;
        }

        return false;
    }

    public void SetExternalFocusRay(Ray ray, float distanceOffset = 0f)
    {
        externalFocusRay = ray;
        externalDistanceOffset = distanceOffset;
        hasExternalRayThisFrame = true;
    }

    public void ClearExternalFocusRay()
    {
        hasExternalRayThisFrame = false;
    }

    public void SetFocusDistance(float focusDistance)
    {
        if (focusDistance <= 0.0001f)
        {
            return;
        }

        targetOpticalPower = 1.0f / focusDistance;
    }
}