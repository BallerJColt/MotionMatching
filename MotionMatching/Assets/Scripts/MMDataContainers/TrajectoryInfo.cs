using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class TrajectoryInfo
{
    public Vector3[] trajectoryPoints;
    public Vector3[] trajectoryForwards;

    public TrajectoryInfo(Vector3[] trajectoryPoints, Vector3[] trajectoryForwards)
    {
        this.trajectoryPoints = trajectoryPoints;
        this.trajectoryForwards = trajectoryForwards;
    }
}
