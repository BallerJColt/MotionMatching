using System.Collections;
using System.Collections.Generic;
using UnityEngine;

//[CreateAssetMenu(fileName = "NewMMPoseData", menuName = "Motion Matching/Pose Data File", order = 10)]
public class MMMotionData : ScriptableObject
{
    public MMConfig config;
    public int Length;
    public MotionFrameInfo[] frameInfo;
    
    public AnimLookup[] satelliteData;
    private AnimLookup errorState = new AnimLookup(0, "Idle State");

    public AnimLookup GetAnimationAtFrame(int frame)
    {
        if (frame >= Length || frame < 0)
        {
            return errorState;
        }

        for (int i = satelliteData.Length - 1; i >= 0; i--)
        {
            if (frame >= satelliteData[i].Key)
            {
                return new AnimLookup(frame - satelliteData[i].Key, satelliteData[i].Value);
            }
        }

        return errorState;
    }

    public int GetAnimationStartFrame(int frame)
    {
        if (frame >= Length || frame < 0)
        {
            return 0;
        }

        for (int i = satelliteData.Length - 1; i >= 0; i--)
        {
            if (frame >= satelliteData[i].Key)
            {
                return  satelliteData[i].Key;
            }
        }

        return 0;
    }

    public int GetAnimationIndex(int frame)
    {
        if (frame >= Length || frame < 0)
        {
            return 0;
        }

        for (int i = satelliteData.Length - 1; i >= 0; i--)
        {
            if (frame >= satelliteData[i].Key)
            {
                return i;
            }
        }

        return 0;
    }

    public int[] GetStartAndLength(int frame, int prevBan, int nextBan)
    {
        int index = GetAnimationIndex(frame);
        int a = (frame - prevBan) < satelliteData[index].Key ? satelliteData[index].Key : (frame - prevBan);
        int b;
        if (index + 1 == satelliteData.Length)
        {
            b = (frame + nextBan) > Length ? Length : (frame + nextBan);    
        }
        else
        {
            b = (frame + nextBan) > satelliteData[index + 1].Key ? satelliteData[index + 1].Key : (frame + nextBan);
        }
       
        return new int[] {a, b-a};
    }

}