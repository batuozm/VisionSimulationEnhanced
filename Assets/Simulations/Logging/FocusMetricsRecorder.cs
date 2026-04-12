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

        if (string.IsNullOrWhiteSpace(algorithmName))
        {
            if (GetComponent<ImprovedFocusProvider>() != null)
            {
                algorithmName = "Improved";
            }
            else if (GetComponent<ToolboxFocusProvider>() != null)
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

        bool hasValidRay = focusRay.direction.sqrMagnitude > 0.0001f;
        bool hasHit = false;
        string hitObject = "NA";
        float hitDistance = 0f;

        if (hasValidRay && Physics.Raycast(focusRay, out RaycastHit hit, maxFocusRayDistance))
        {
            hasHit = true;
            hitObject = hit.collider.name;
            hitDistance = hit.distance;
            UpdateSwitchMetrics(hitObject, hitDistance);
        }
        else
        {
            lostHitCount++;
        }

        float targetOpticalPower = defocus != null ? defocus.targetOpticalPower : 0f;
        float opticalPower = defocus != null ? defocus.opticalPower : 0f;

        float targetFocusDistance = targetOpticalPower > 0.0001f ? 1f / targetOpticalPower : 0f;
        float currentFocusDistance = opticalPower > 0.0001f ? 1f / opticalPower : 0f;

        float currentObjectDuration = currentHitObject != "NA" ? Time.time - currentHitStartTime : 0f;
        float frameTimeMs = Time.unscaledDeltaTime * 1000f;
        float recorderStepMs = (Time.realtimeSinceStartup - recorderStepStart) * 1000f;

        buffer.Append(Time.time.ToString("F6")).Append(",");
        buffer.Append(SceneManager.GetActiveScene().name).Append(",");
        buffer.Append(algorithmName).Append(",");
        buffer.Append(frameTimeMs.ToString("F4")).Append(",");
        buffer.Append(recorderStepMs.ToString("F4")).Append(",");
        buffer.Append(hasHit ? "1" : "0").Append(",");
        buffer.Append(hitObject).Append(",");
        buffer.Append(hitDistance.ToString("F4")).Append(",");
        buffer.Append(currentHitObject).Append(",");
        buffer.Append(currentHitDistance.ToString("F4")).Append(",");
        buffer.Append(currentObjectDuration.ToString("F4")).Append(",");
        buffer.Append(switchCount).Append(",");
        buffer.Append(switchBackCount).Append(",");
        buffer.Append(lostHitCount).Append(",");
        buffer.Append(targetOpticalPower.ToString("F6")).Append(",");
        buffer.Append(opticalPower.ToString("F6")).Append(",");
        buffer.Append(targetFocusDistance.ToString("F4")).Append(",");
        buffer.Append(currentFocusDistance.ToString("F4")).AppendLine();

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
            "current_focus_distance_m\n";
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