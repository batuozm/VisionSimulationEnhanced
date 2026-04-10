using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class ExperimentManager : MonoBehaviour
{
    ExperimentManager Instance;
    EyeTrackingToolbox eyetracker;
    void Start()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // Keep this object alive across scenes
        }
        else
        {
            Destroy(gameObject); // Destroy duplicate instances
        }


        // set output recording folder
        eyetracker = EyeTrackingToolbox.Instance;
        eyetracker.SetOutputFolder("./recordings/");
    }

    // Update is called once per frame
    void Update()
    {
     // change scene when space is pressed
        if (Input.GetKeyDown(KeyCode.Space))
        {
            // Load the next scene in the build settings
            int currentSceneIndex = UnityEngine.SceneManagement.SceneManager.GetActiveScene().buildIndex;
            int nextSceneIndex = (currentSceneIndex + 1) % UnityEngine.SceneManagement.SceneManager.sceneCountInBuildSettings;
            UnityEngine.SceneManagement.SceneManager.LoadScene(nextSceneIndex);
        }   

        // start gaze recording, when "s" is pressed
        if (Input.GetKeyDown(KeyCode.S))
        {
            if (!eyetracker.isRecording)
            {
                Debug.Log("StartRecording");
                eyetracker.StartRecording("test1.csv");
            }
            else
            {
                Debug.Log("StopRecording");
                eyetracker.StopRecording();
            }
        }
        GazeData gazeData = eyetracker.GetGazeData();
    }
}
