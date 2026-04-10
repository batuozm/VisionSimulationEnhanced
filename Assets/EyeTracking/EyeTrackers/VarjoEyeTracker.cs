#if USE_VARJO
using UnityEngine;
using Varjo.XR;
using System.Collections;
using System.Collections.Generic;

public class VarjoEyeTracker :  MonoBehaviour,IEyeTracker
{
    private GazeData currentGazeData;
    private bool backgroundSampling = false;
    private List<VarjoEyeTracking.GazeData> dataSinceLastUpdate; // Varjo-provided list of gaze data
    private List<VarjoEyeTracking.EyeMeasurements> eyeMeasurementsSinceLastUpdate; // Varjo-provided list of eye measurements
    private static Queue<GazeData> _gazeSamples; // reference to the queue of gaze samples
    private Coroutine samplingCoroutine;


    public void Initialize()
    {
        VarjoEyeTracking.SetGazeOutputFilterType(VarjoEyeTracking.GazeOutputFilterType.Standard); // Standard or None
        VarjoEyeTracking.SetGazeOutputFrequency(VarjoEyeTracking.GazeOutputFrequency.MaximumSupported); // MaximumSupported, Frequency100Hz, or Frequency200Hz
		Debug.Log("Varjo eye tracking initialized.");
    }

    public void Calibrate()
    {
        if (VarjoEyeTracking.RequestGazeCalibration())
        {
            Debug.Log("Varjo gaze calibration successful. Quality left: " + VarjoEyeTracking.GetGazeCalibrationQuality().left + " Quality right: " + VarjoEyeTracking.GetGazeCalibrationQuality().right);
        }
        else
        {
            Debug.Log("Varjo gaze calibration failed.");   
        }
    }

    public GazeData GetGazeData()
    {   
        if(backgroundSampling) // if we perform background sampling, return the latest gaze data from the queue
        {
            return currentGazeData;
        }
        else // ohterwise we have to get fresh Varjo data
        {
            return VarjoGazeDataToGazeData(VarjoEyeTracking.GetGaze(), VarjoEyeTracking.GetEyeMeasurements());
        }
    }
	
	private IEnumerator SamplingCoroutine()
    {
        while (backgroundSampling)
        {
            int nDataSamples = VarjoEyeTracking.GetGazeList(out dataSinceLastUpdate, out eyeMeasurementsSinceLastUpdate);
            Debug.Log(nDataSamples + " new samples from EyeTracker.");
			for(int i = 0; i < nDataSamples; i++)
            {
				
                GazeData gazeData = VarjoGazeDataToGazeData(dataSinceLastUpdate[i], eyeMeasurementsSinceLastUpdate[i]);
                EyeTrackingEvent.TriggerEvent(gazeData);
                
                // set the current gaze data to the last gaze data in the list
                if (i == dataSinceLastUpdate.Count - 1)
                {
                    currentGazeData = gazeData;
                }
            }
            yield return null;

            if (!backgroundSampling)
                break;
        }
        UnityEngine.Debug.Log("Stopped background gaze sampling.");
    }


    public void StartListening()
    {
        backgroundSampling = true;
        UnityEngine.Debug.Log("Varjo eye tracker started listening");

        if (samplingCoroutine == null)
        {
            samplingCoroutine = StartCoroutine(SamplingCoroutine());
        }
    }
    
    public void StopListening()
    {
        backgroundSampling = false;

        if (samplingCoroutine != null)
        {
            StopCoroutine(samplingCoroutine);
            samplingCoroutine = null;
        }
    }

    private GazeData VarjoGazeDataToGazeData(VarjoEyeTracking.GazeData varjoData)
    {
        GazeData gazeData = new GazeData();
        gazeData.deviceTimestamp = varjoData.captureTime;
        
        // eye tracking status: 0 – Data unavailable; 1 User is wearing the headset, but gaze tracking is being calibrated; 2 – Data is valid
        gazeData.leftValidataBitMap = (ulong) varjoData.leftStatus;
        gazeData.rightValidataBitMap = (ulong) varjoData.rightStatus;
        
        // left eye gaze ray
        gazeData.leftRayLocal = new Ray(varjoData.left.origin, varjoData.left.forward);
        // right eye gaze ray
        gazeData.rightRayLocal= new Ray(varjoData.right.origin, varjoData.right.forward);
        // combined gaze ray
        gazeData.combinedRayLocal = new Ray(varjoData.gaze.origin, varjoData.gaze.forward);
        // gaze distance
        gazeData.gazeDistance = varjoData.focusDistance;

        gazeData.leftValidity = (int) varjoData.leftStatus;;
        gazeData.rightValidity = (int) varjoData.rightStatus;

        return gazeData;
    }


    private GazeData VarjoGazeDataToGazeData(VarjoEyeTracking.GazeData varjoData, VarjoEyeTracking.EyeMeasurements varjoEyeMeasurements)
    {
        GazeData gazeData = VarjoGazeDataToGazeData(varjoData);
         
         // add pupil diameters from eyeMeasurements
        gazeData.leftEyePupilDiameter = varjoEyeMeasurements.leftPupilDiameterInMM;
        gazeData.rightEyePupilDiameter = varjoEyeMeasurements.rightPupilDiameterInMM;
        gazeData.leftEyeOpenness = varjoEyeMeasurements.leftEyeOpenness;
        gazeData.rightEyeOpenness = varjoEyeMeasurements.rightEyeOpenness;
        
        return gazeData;
    }
}
#endif // USE_VARJO