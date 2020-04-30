using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class BaseTrajectoryDebugger : MonoBehaviour
{
    public MMAnimationClip mmAnimClip;
    public MMMotionData motionData;


    public int step;
    [Range(0f, 10f)] public float arrowLength;
    public bool showTrajectory;
    public bool showSkeleton;
    public bool showJointVelocities;
    public bool showLocalTrajectory;
    public bool showCharSpaceSkeleton;
    public bool showCharSpaceVelocities;
    public int charSpaceFrame;
    public bool showJointPositionCluster;

    private void Update()
    {
        if (Input.GetKeyDown("space"))
        {
            Debug.Log(motionData.satelliteData.Length);
            foreach (var data in motionData.satelliteData)
            {
                Debug.Log("start frame: " + data.Key + "; anim name: " + data.Value);   
            }
        }
    }

    private void OnDrawGizmos()
    {
        if (mmAnimClip == null || step <= 0 || step >= mmAnimClip.baseTrajectory.Length)
            return;
        for (int i = 0; i < mmAnimClip.baseTrajectory.Length; i += step)
        {
            if (showTrajectory)
            {
                Gizmos.color = Color.yellow;
                Gizmos.DrawWireSphere(transform.position + mmAnimClip.baseTrajectory[i].position, arrowLength / 10);
                DrawArrow(transform.position + mmAnimClip.baseTrajectory[i].position,
                    mmAnimClip.baseTrajectory[i].forward, arrowLength);


                if (showSkeleton)
                {
                    for (var index = 0; index < mmAnimClip.basePoses[i].jointPositions.Count; index++)
                    {
                        if (index == 2) Gizmos.color = Color.cyan;
                        else
                            Gizmos.color = Color.green;
                        var pose = mmAnimClip.basePoses[i].jointPositions[index];
                        var vel = mmAnimClip.basePoses[i].jointVelocities[index];
                        Gizmos.DrawRay(transform.position + mmAnimClip.baseTrajectory[i].position, pose);

                        if (showJointVelocities)
                        {
                            Gizmos.color = Color.red;
                            Gizmos.DrawRay(transform.position + mmAnimClip.baseTrajectory[i].position + pose, vel);
                        }
                    }
                }
            }
        }

        if (showCharSpaceSkeleton && charSpaceFrame < motionData.Length && charSpaceFrame >= 0)
        {
            Gizmos.color = Color.blue;
            for (var i = 0; i < motionData.frameInfo[charSpaceFrame].pose.jointPositions.Count; i++)
            {
                var position = motionData.frameInfo[charSpaceFrame].pose.jointPositions[i];
                var vel = motionData.frameInfo[charSpaceFrame].pose.jointVelocities[i];
                Gizmos.DrawRay(transform.position, position);
                if (showCharSpaceVelocities)
                {
                    Gizmos.color = Color.red;
                    Gizmos.DrawRay(transform.position + position, vel);
                }

                Gizmos.color = Color.blue;
            }
        }

        if (showLocalTrajectory && charSpaceFrame >= 0 && charSpaceFrame < motionData.Length)
        {
            for (var i = 0; i < motionData.frameInfo[charSpaceFrame].trajectoryInfo.trajectoryPoints.Length; i++)
            {
                var position = motionData.frameInfo[charSpaceFrame].trajectoryInfo.trajectoryPoints[i];
                var fwd = motionData.frameInfo[charSpaceFrame].trajectoryInfo.trajectoryForwards[i];
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(transform.position + position, arrowLength / 10);
                DrawArrow(transform.position + position, fwd, arrowLength);
            }
        }

        if (showJointPositionCluster)
        {
            Gizmos.color = Color.white;
            foreach (var f in motionData.frameInfo)
            {
                foreach (var position in f.pose.jointPositions)
                {
                    Gizmos.DrawWireSphere(position, 0.01f);
                }
            }
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 fwd, float scale)
    {
        pos.y += arrowLength / 10;
        Gizmos.DrawRay(pos, Vector3.up * arrowLength / 10);
        pos.y += arrowLength / 10;
        float arrowHeadScale = scale / 5;
        Gizmos.DrawRay(pos, fwd * scale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 160f + arrowLength, 0) * fwd * arrowHeadScale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 200f - arrowLength, 0) * fwd * arrowHeadScale);
    }
}