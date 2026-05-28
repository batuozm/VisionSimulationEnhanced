using System.IO;
using System.Text;
using UnityEngine;
using UnityEngine.SceneManagement;

[DisallowMultipleComponent]
public class FocusMetricsRecorder : MonoBehaviour
{
    public Defocus defocus;

    [Header("Logging")]
    public bool autoStartOnPlay = true;
    public string outputFolderName = "FocusMetrics";
    public string fileBaseName = "focus_metrics";
    public string algorithmName = "";
    public float flushInterval = 1.0f;

    [Header("Raycast Metrics")]
    public float maxFocusRayDistance = 100f;
    public float minSwitchDistanceDelta = 0.05f;
    public float switchBackWindow = 0.35f;

    private EyeTrackingToolbox toolbox;
    private EnhancedFocusProvider enhancedProvider;

    private string filePath;
    private StringBuilder buffer = new StringBuilder(8192);

    private bool isLogging = false;
    private float lastFlushTime = 0f;

    private string currentHitObject = "NA";
    private string previousHitObject = "NA";
    private string prePreviousHitObject = "NA";

    private float currentHitDistance = 0f;
    private float currentHitStartTime = 0f;
    private float lastSwitchTime = -999f;

    private int switchCount = 0;
    private int switchBackCount = 0;
    private int lostHitCount = 0;

    private const float MinOpticalPower = 0.0001f;
    private const float MinRayDirectionSqrMagnitude = 0.0001f;

    private void Awake()
    {
        if (defocus == null)
        {
            defocus = GetComponent<Defocus>();
        }

        enhancedProvider = GetComponent<EnhancedFocusProvider>();
    }

    private void Start()
    {
        toolbox = EyeTrackingToolbox.Instance;

        if (enhancedProvider == null)
        {
            enhancedProvider = GetComponent<EnhancedFocusProvider>();
        }

        if (string.IsNullOrWhiteSpace(algorithmName))
        {
            if (enhancedProvider != null)
            {
                algorithmName = "Enhanced";
            }
            else if (GetComponent<BaselineFocusProvider>() != null)
            {
                algorithmName = "Baseline";
            }
            else
            {
                algorithmName = "Unknown";
            }
        }

        if (autoStartOnPlay)
        {
            StartLogging();
        }
    }

    private void Update()
    {
        if (!isLogging || toolbox == null)
        {
            return;
        }

        float recorderStepStart = Time.realtimeSinceStartup;

        GazeData gazeData = toolbox.GetGazeData();
        Ray focusRay = gazeData.combinedRayWorld;

        bool hasHit = false;
        string hitObject = "NA";
        float hitDistance = 0f;

        if (IsValidRay(focusRay) && Physics.Raycast(focusRay, out RaycastHit hit, maxFocusRayDistance))
        {
            hasHit = true;
            hitObject = hit.collider != null ? hit.collider.name : "NA";
            hitDistance = hit.distance;
            UpdateSwitchMetrics(hitObject, hitDistance);
        }
        else
        {
            lostHitCount++;
        }

        float targetOpticalPower = defocus != null ? defocus.targetOpticalPower : 0f;
        float opticalPower = defocus != null ? defocus.opticalPower : 0f;

        float targetFocusDistance = targetOpticalPower > MinOpticalPower ? 1f / targetOpticalPower : 0f;
        float currentFocusDistance = opticalPower > MinOpticalPower ? 1f / opticalPower : 0f;

        float currentObjectDuration = currentHitObject != "NA" ? Time.time - currentHitStartTime : 0f;
        float frameTimeMs = Time.unscaledDeltaTime * 1000f;
        float recorderStepMs = (Time.realtimeSinceStartup - recorderStepStart) * 1000f;

        bool wideConeChecked = enhancedProvider != null && enhancedProvider.LastFrameWideConeChecked;
        bool wideConeSawConfirmed = enhancedProvider != null && enhancedProvider.LastFrameWideConeSawConfirmed;
        bool wideConeHeldFocus = enhancedProvider != null && enhancedProvider.LastFrameWideConeHeldFocus;
        float vergenceDiopters = enhancedProvider != null ? enhancedProvider.LastFrameVergenceDiopters : 0f;

        buffer.Append(Time.time.ToString("F6")).Append(",");
        buffer.Append(Csv(SceneManager.GetActiveScene().name)).Append(",");
        buffer.Append(Csv(algorithmName)).Append(",");
        buffer.Append(frameTimeMs.ToString("F4")).Append(",");
        buffer.Append(recorderStepMs.ToString("F4")).Append(",");
        buffer.Append(hasHit ? "1" : "0").Append(",");
        buffer.Append(Csv(hitObject)).Append(",");
        buffer.Append(hitDistance.ToString("F6")).Append(",");
        buffer.Append(Csv(currentHitObject)).Append(",");
        buffer.Append(currentHitDistance.ToString("F6")).Append(",");
        buffer.Append(currentObjectDuration.ToString("F6")).Append(",");
        buffer.Append(switchCount).Append(",");
        buffer.Append(switchBackCount).Append(",");
        buffer.Append(lostHitCount).Append(",");
        buffer.Append(targetOpticalPower.ToString("F6")).Append(",");
        buffer.Append(opticalPower.ToString("F6")).Append(",");
        buffer.Append(targetFocusDistance.ToString("F6")).Append(",");
        buffer.Append(currentFocusDistance.ToString("F6")).Append(",");
        buffer.Append(wideConeChecked ? "1" : "0").Append(",");
        buffer.Append(wideConeSawConfirmed ? "1" : "0").Append(",");
        buffer.Append(wideConeHeldFocus ? "1" : "0").Append(",");
        buffer.Append(vergenceDiopters.ToString("F6")).Append("\n");

        if (Time.time - lastFlushTime >= flushInterval)
        {
            Flush();
        }
    }

    private void UpdateSwitchMetrics(string newHitObject, float newHitDistance)
    {
        if (currentHitObject == "NA")
        {
            currentHitObject = newHitObject;
            currentHitDistance = newHitDistance;
            currentHitStartTime = Time.time;
            lastSwitchTime = Time.time;
            return;
        }

        bool objectChanged = newHitObject != currentHitObject;
        bool distanceChangedEnough = Mathf.Abs(newHitDistance - currentHitDistance) >= minSwitchDistanceDelta;

        if (objectChanged && distanceChangedEnough)
        {
            prePreviousHitObject = previousHitObject;
            previousHitObject = currentHitObject;

            currentHitObject = newHitObject;
            currentHitDistance = newHitDistance;
            currentHitStartTime = Time.time;

            switchCount++;

            if (newHitObject == prePreviousHitObject && Time.time - lastSwitchTime <= switchBackWindow)
            {
                switchBackCount++;
            }

            lastSwitchTime = Time.time;
            return;
        }

        currentHitDistance = newHitDistance;
    }

    public void StartLogging()
    {
        if (isLogging)
        {
            return;
        }

        ResetState();

        string folder = Path.Combine(Application.persistentDataPath, outputFolderName);
        Directory.CreateDirectory(folder);

        string safeSceneName = SceneManager.GetActiveScene().name;
        string timestamp = System.DateTime.Now.ToString("yyyyMMdd_HHmmss");

        filePath = Path.Combine(folder, $"{fileBaseName}_{safeSceneName}_{algorithmName}_{timestamp}.csv");

        File.WriteAllText(filePath, BuildHeader());
        isLogging = true;
        lastFlushTime = Time.time;

        Debug.Log("FocusMetricsRecorder logging to: " + filePath);
    }

    public void StopLogging()
    {
        if (!isLogging)
        {
            return;
        }

        Flush();
        isLogging = false;

        Debug.Log("FocusMetricsRecorder stopped.");
    }

    private void Flush()
    {
        if (buffer.Length == 0 || string.IsNullOrEmpty(filePath))
        {
            lastFlushTime = Time.time;
            return;
        }

        File.AppendAllText(filePath, buffer.ToString());
        buffer.Length = 0;
        lastFlushTime = Time.time;
    }

    private string BuildHeader()
    {
        return
            "unity_time," +
            "scene_name," +
            "algorithm_name," +
            "frame_time_ms," +
            "recorder_step_ms," +
            "has_hit," +
            "raw_hit_object," +
            "raw_hit_distance_m," +
            "tracked_hit_object," +
            "tracked_hit_distance_m," +
            "tracked_hit_duration_s," +
            "switch_count," +
            "switch_back_count," +
            "lost_hit_count," +
            "target_optical_power," +
            "optical_power," +
            "target_focus_distance_m," +
            "current_focus_distance_m," +
            "enhanced_wide_cone_checked," +
            "enhanced_wide_cone_saw_confirmed," +
            "enhanced_wide_cone_held_focus," +
            "enhanced_vergence_diopters\n";
    }

    private void ResetState()
    {
        buffer.Length = 0;

        currentHitObject = "NA";
        previousHitObject = "NA";
        prePreviousHitObject = "NA";

        currentHitDistance = 0f;
        currentHitStartTime = 0f;
        lastSwitchTime = -999f;

        switchCount = 0;
        switchBackCount = 0;
        lostHitCount = 0;
    }

    private static bool IsValidRay(Ray ray)
    {
        return IsFinite(ray.origin)
            && IsFinite(ray.direction)
            && ray.direction.sqrMagnitude > MinRayDirectionSqrMagnitude;
    }

    private static bool IsFinite(Vector3 value)
    {
        return !float.IsNaN(value.x)
            && !float.IsNaN(value.y)
            && !float.IsNaN(value.z)
            && !float.IsInfinity(value.x)
            && !float.IsInfinity(value.y)
            && !float.IsInfinity(value.z);
    }

    private static string Csv(string value)
    {
        if (string.IsNullOrEmpty(value))
        {
            return "";
        }

        bool needsQuotes = value.Contains(",") || value.Contains("\"") || value.Contains("\n") || value.Contains("\r");

        if (!needsQuotes)
        {
            return value;
        }

        return "\"" + value.Replace("\"", "\"\"") + "\"";
    }

    private void OnDisable()
    {
        StopLogging();
    }

    private void OnApplicationQuit()
    {
        StopLogging();
    }
}