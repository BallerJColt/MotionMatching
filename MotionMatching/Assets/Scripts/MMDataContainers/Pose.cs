using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class Pose
{
    public List<Vector3> jointPositions;
    public List<Vector3> jointVelocities;

    public Pose(List<Vector3> jointPositions, List<Vector3> jointVelocities)
    {
        this.jointPositions = jointPositions;
        this.jointVelocities = jointVelocities;
    }
    
    
}
