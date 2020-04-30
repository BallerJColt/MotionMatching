using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class BaseTrajectory
{
    public Matrix4x4 rootWorldToLocalMatrix;
    public Vector3 position;
    public Vector3 forward;
    public float timeStamp;

    public BaseTrajectory(Matrix4x4 w2LMatrix, Vector3 position,Vector3 forward, float timeStamp)
    {
        rootWorldToLocalMatrix = w2LMatrix;
        this.position = position;
        this.forward = forward;
        this.timeStamp = timeStamp;
    }
}
