using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class OffAxisProjection : MonoBehaviour
{
    // parameters for the physical screen positions
    public Vector3 screenLowerLeft;
    public Vector3 screenLowerRight;
    public Vector3 screenUpperLeft;
    
    public Matrix4x4 originalProjection;
    
    Camera cam;

    Vector3 vr; // right screen vector
    Vector3 vu; // up screen vector
    Vector3 vn; // normal screen vector

    Vector3 va; // cam position to lower left screen coordinate
    Vector3 vb; // cam position to lower right screen coordinate
    Vector3 vc; // cam position to upper left screen coordinate
    


    void Start()
    {
        cam = GetComponent<Camera>();
        originalProjection = cam.projectionMatrix;
    }

    void Update()
    {
        vr = screenLowerRight - screenLowerLeft;
        vr = Vector3.Normalize(vr);

        vu = screenUpperLeft - screenLowerLeft;
        vu = Vector3.Normalize(vu);

        vn = Vector3.Cross(vr,vu);
        vn = Vector3.Normalize(vn);

        va = screenLowerLeft - this.transform.position;
        vb = screenLowerRight - this.transform.position;
        vc = screenUpperLeft - this.transform.position;
    }

    void LateUpdate() // use LateUpdate for setting the projection, to make sure all tracking updates are done
    {
        float d = 1.0f * Vector3.Dot(vn,va);
        float near = cam.nearClipPlane;
        float far = cam.farClipPlane;

        float left = Vector3.Dot(vr,va) * near / d;
        float right = Vector3.Dot(vr,vb) * near / d;
        float bottom = Vector3.Dot(vu,va) * near / d;
        float top = Vector3.Dot(vu,vc) * near / d;


        Matrix4x4 M = Matrix4x4.zero;
        M[0, 0] = vr.x;
        M[0, 1] = vr.y;
        M[0, 2] = vr.z;

        M[1, 0] = vu.x;
        M[1, 1] = vu.y;
        M[1, 2] = vu.z;

        M[2, 0] = vn.x;
        M[2, 1] = vn.y;
        M[2, 2] = vn.z;

        M[3, 3] = 1.0f;


        Matrix4x4 P = PerspectiveOffCenter(left, right, bottom, top, near, far);
        
        //Translation to eye position
        Matrix4x4 T = Matrix4x4.Translate(-transform.position);

        Matrix4x4 R = Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation)); // * ProjectionScreen.transform.rotation);
        cam.ResetWorldToCameraMatrix();                
        
        cam.worldToCameraMatrix =  Matrix4x4.Rotate(Quaternion.Inverse(transform.rotation)) * cam.worldToCameraMatrix;//R * T;
        
        cam.projectionMatrix = P*M.inverse;
    }


    void OnDrawGizmos()
    {
        Gizmos.color = Color.red;
        Gizmos.DrawLine(screenLowerLeft, screenLowerRight);
        Gizmos.DrawLine(screenLowerLeft, screenUpperLeft);
        Gizmos.DrawLine(screenUpperLeft + screenLowerRight - screenLowerLeft, screenLowerRight);
        Gizmos.DrawLine(screenUpperLeft, screenUpperLeft + screenLowerRight - screenLowerLeft);
    }

    // general off axis projection matrix
    Matrix4x4 PerspectiveOffCenter(float left, float right, float bottom, float top, float near, float far)
    {
        float x = 2.0F * near / (right - left);
        float y = 2.0F * near / (top - bottom);
        float a = (right + left) / (right - left);
        float b = (top + bottom) / (top - bottom);
        float c = -(far + near) / (far - near);
        float d = -(2.0F * far * near) / (far - near);
        float e = -1.0F;
        Matrix4x4 m = new Matrix4x4();
        m[0, 0] = x;
        m[0, 1] = 0;
        m[0, 2] = a;
        m[0, 3] = 0;
        m[1, 0] = 0;
        m[1, 1] = y;
        m[1, 2] = b;
        m[1, 3] = 0;
        m[2, 0] = 0;
        m[2, 1] = 0;
        m[2, 2] = c;
        m[2, 3] = d;
        m[3, 0] = 0;
        m[3, 1] = 0;
        m[3, 2] = e;
        m[3, 3] = 0;
        return m;
    }
}