using System.Collections;
using System.Collections.Generic;
using UnityEngine;

[System.Serializable]
public class MotionFrameInfo
{
    public TrajectoryInfo trajectoryInfo;
    public Pose pose;
    public int tag;

    public MotionFrameInfo(TrajectoryInfo trajectoryInfo, Pose pose, int tag)
    {
        this.trajectoryInfo = trajectoryInfo;
        this.pose = pose;
        this.tag = tag;
    }
}