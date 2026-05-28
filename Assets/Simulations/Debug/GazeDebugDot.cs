using UnityEngine;

public class GazeDebugDot : MonoBehaviour
{
    [Header("References")]
    [SerializeField] private EyeTrackingToolbox eyeTrackingToolbox;
    [SerializeField] private GameObject gazeDot;

    [Header("Raycast")]
    [SerializeField] private float maxDistance = 20f;
    [SerializeField] private LayerMask hitMask = ~0;
    [SerializeField] private float surfaceOffset = 0.01f;

    [Header("Dot Visual")]
    [SerializeField] private float dotSize = 0.025f;

    private void Awake()
    {
        if (eyeTrackingToolbox == null)
            eyeTrackingToolbox = EyeTrackingToolbox.Instance;

        if (gazeDot == null)
            CreateDefaultDot();

        gazeDot.SetActive(false);
    }

    private void Update()
    {
        if (eyeTrackingToolbox == null || gazeDot == null)
            return;

        GazeData gazeData = eyeTrackingToolbox.GetGazeData();
        Ray gazeRay = gazeData.combinedRayWorld;

        if (gazeRay.direction.sqrMagnitude < 0.0001f)
        {
            gazeDot.SetActive(false);
            return;
        }

        if (Physics.Raycast(gazeRay, out RaycastHit hit, maxDistance, hitMask, QueryTriggerInteraction.Ignore))
        {
            gazeDot.SetActive(true);

            // Slightly offset from the surface to avoid z-fighting.
            gazeDot.transform.position = hit.point + hit.normal * surfaceOffset;
            gazeDot.transform.rotation = Quaternion.LookRotation(hit.normal);
            gazeDot.transform.localScale = Vector3.one * dotSize;
        }
        else
        {
            gazeDot.SetActive(false);
        }
    }

    private void CreateDefaultDot()
    {
        gazeDot = GameObject.CreatePrimitive(PrimitiveType.Sphere);
        gazeDot.name = "Gaze Debug Dot";

        Collider dotCollider = gazeDot.GetComponent<Collider>();
        if (dotCollider != null)
            Destroy(dotCollider);

        gazeDot.layer = LayerMask.NameToLayer("Ignore Raycast");

        Renderer renderer = gazeDot.GetComponent<Renderer>();
        if (renderer != null)
        {
            Material mat = new Material(Shader.Find("Standard"));
            mat.color = Color.red;
            renderer.material = mat;
        }
    }
}