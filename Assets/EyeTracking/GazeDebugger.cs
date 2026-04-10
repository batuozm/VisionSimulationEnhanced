using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class GazeDebugger : MonoBehaviour
{
    public Color colorLeft = Color.green;
    public Color colorRight = Color.red;
    public Color colorCombined = Color.white;
    EyeTrackingToolbox eyeTracker;
    // Start is called before the first frame update
    void Start()
    {
        eyeTracker = EyeTrackingToolbox.Instance;
        if (eyeTracker == null)
        {
            Debug.LogError("EyeTrackingToolbox instance not found. Make sure it is initialized before using GazeDebugger.");
        }
    }

    // Update is called once per frame
    void Update()
    {
        // get current gaze data from the eye tracker
        GazeData gazeData = eyeTracker.GetGazeData();
        
        // plot debug rays for left, right and combined eye
        Debug.DrawRay(gazeData.leftRayWorld.origin, gazeData.leftRayWorld.direction * 10, colorLeft);
        Debug.DrawRay(gazeData.rightRayWorld.origin, gazeData.rightRayWorld.direction * 10, colorRight);
        Debug.DrawRay(gazeData.combinedRayWorld.origin, gazeData.combinedRayWorld.direction * 10, colorCombined);
    }
}
