using System.Collections;
using System.Collections.Generic;
using UnityEngine;

public class MMAnimationClip : ScriptableObject
{
    public int frameRate;
    public int Length;
    public AnimationClip animClip;
    public BaseTrajectory[] baseTrajectory;
    public Pose[] basePoses;

    //Maybe we should format the data here so that we don't have to do it in the debugger

    public void PreformatData()
    {
        if (basePoses.Length != baseTrajectory.Length)
        {
            Debug.Log("Pose and trajectory data does not match!");
            return;
        }

        BaseTrajectory[] formattedBaseTrajectories = new BaseTrajectory[baseTrajectory.Length];
        Pose[] formattedBasePoses = new Pose[basePoses.Length];
        for (int i = 0; i < baseTrajectory.Length; i++)
        {
            //Formatting base trajectories so they begin at (0,0,0);(0,0,1).

            /*Vector3 localPos = baseTrajectory[0].rootWorldToLocalMatrix.MultiplyPoint3x4(baseTrajectory[i].position);
            Vector3 localFwd = baseTrajectory[0].rootWorldToLocalMatrix.MultiplyVector(baseTrajectory[i].forward);
            Matrix4x4 localMatrix = baseTrajectory[0].rootWorldToLocalMatrix.inverse * baseTrajectory[i].rootWorldToLocalMatrix;
            formattedBaseTrajectories[i] =
                new BaseTrajectory(localMatrix, localPos, localFwd, baseTrajectory[i].timeStamp);*/

            //Formatting base poses so they correspond to the new trajectory
            
            List<Vector3> localBonePositionList = new List<Vector3>();
            List<Vector3> localBoneVelocityList = new List<Vector3>();
            for (int j = 0; j < basePoses[i].jointPositions.Count; j++)
            {
                Vector3 localBonePos = basePoses[i].jointPositions[j];
                
                Vector3 localBoneVel = i == 0
                    ? Vector3.zero
                    : (basePoses[i].jointPositions[j] -
                       basePoses[i - 1].jointPositions[j]) * frameRate;
                
                localBonePositionList.Add(localBonePos);
                localBoneVelocityList.Add(localBoneVel);
            }
            formattedBasePoses[i] = new Pose(localBonePositionList,localBoneVelocityList);
        }

        //baseTrajectory = formattedBaseTrajectories;
        basePoses = formattedBasePoses;    
    }
}