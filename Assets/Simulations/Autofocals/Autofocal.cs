using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class Autofocal : MonoBehaviour
{
    // Start is called before the first frame update
    Defocus focusControllerLeft,focusControllerRight;
    public enum FocusPlane
    {
        near,
        mid,
        far
    }
    public float[] focusPlaneList = new float[] {0.3f , 1.0f , 6.0f};
    public float focusDistance;
    int focusPlaneInd = 0;
    public GameObject leftEye;
    public GameObject rightEye;
    
    void Start()
    {
        if (leftEye== null)
            leftEye = this.transform.Find("Left Eye").gameObject;
        if (rightEye == null)
            rightEye = this.transform.Find("Right Eye").gameObject;
        
        focusControllerLeft = leftEye.GetComponent<Defocus>();
        focusControllerRight = rightEye.GetComponent<Defocus>();
        if (focusControllerLeft == null)
        {
            // add Defocus component to camera/child cameras
            focusControllerLeft = leftEye.AddComponent<Defocus>();
        }
        if (focusControllerRight == null)
        {
            // add Defocus component to camera/child cameras
            focusControllerRight = rightEye.AddComponent<Defocus>();
        }
        focusControllerLeft.defocusShader = Shader.Find("Hidden/Defocus");
        focusControllerRight.defocusShader = Shader.Find("Hidden/Defocus");
    }

    // manually set the focus distance in meters
    void SetFocusDistance(float dist)
    {
        focusDistance = dist;
        focusControllerLeft.SetFocusDistance(focusPlaneList[focusPlaneInd]);
        focusControllerRight.SetFocusDistance(focusPlaneList[focusPlaneInd]);
    }

    // set focus distance to one of the defined focus planes (near, mid, far)
    void SetFocusDistance(FocusPlane fcspln)
    {
        SetFocusDistance(focusPlaneList[(int)fcspln]); // use the enum fcspln as an index of the focus plane distance list
    }

    void SetFocusWithRay(Ray ray)
    {
        RaycastHit hit;
        if (Physics.Raycast(ray, out hit, 100))
           {
               Debug.Log(hit.transform.gameObject.name);
               SetFocusDistance(hit.distance);
           }
    }

    // Update is called once per frame
    void Update()
    {
        // set focus by clicking on point
        if (Input.GetMouseButtonDown(0))
        {
            Ray ray = GetComponent<Camera>().ScreenPointToRay(Input.mousePosition);
            RaycastHit hit;

            if (Physics.Raycast(ray, out hit, 100))
            {
                Debug.Log(hit.transform.gameObject.name);
                // the ray originates in the near clipping plane, to get the focus distance we have to add the near clip distance
                float focusDistance = hit.distance + GetComponent<Camera>().nearClipPlane;
                Debug.Log(focusDistance);
                focusControllerLeft.SetFocusDistance(focusDistance);// set lens power to focus on hit object
                focusControllerRight.SetFocusDistance(focusDistance);// set lens power to focus on hit object

            }
        }
        if (Input.GetKeyDown(KeyCode.UpArrow))
        {
            if(++focusPlaneInd >= focusPlaneList.Length)
            {
                focusPlaneInd = focusPlaneList.Length-1;
            }
            SetFocusDistance(focusPlaneList[focusPlaneInd]);
        }
        if (Input.GetKeyDown(KeyCode.DownArrow))
        {
            if(--focusPlaneInd < 0)
            {
                focusPlaneInd =0;
            }
            SetFocusDistance(focusPlaneList[focusPlaneInd]);
        }
    }
}