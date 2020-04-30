using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class MMAnimationController : MonoBehaviour
{
    public MMMotionData poseData;
    public MotionMatchable predictor;
    public Animator animator;
    [Range(1, 60)] public int poseRefreshRate;
    [Range(0f, 1f)] public float crossFadeTime;
    public bool isMotionMatchingRunning;
    private NativeArray<float3> motionDataNativeArray;
    private NativeArray<float> weightNativeArray;
    private NativeArray<int> tagNativeArray;
    private NativeArray<float3> costCompareNativeArray;
    public bool applyRootMotion;
    public int bestIndex;
    public IgnoreTags ignoreTag;
    public float trajectoryToPoseRatio;
    public float[] weights;
    public Queue<int[]> banQueue;
    private int trajPoints;
    private int boneCount;
    private int chunkLength;
    private string current;
    public bool isDefault;
    void Awake()
    {
        trajPoints = poseData.config.trajectoryTimePoints.Count;
        boneCount = poseData.config.trackedBones.Count;
        chunkLength = 2 * (boneCount + trajPoints);

        float3[] flatMotionDataArray = new float3[poseData.Length * chunkLength];
        int[] tempTags = new int[poseData.Length];
        for (int i = 0; i < poseData.Length; i++)
        {
            for (int j = 0; j < trajPoints; j++)
            {
                flatMotionDataArray[(i * chunkLength) + j] = poseData.frameInfo[i].trajectoryInfo.trajectoryPoints[j];
                flatMotionDataArray[(i * chunkLength) + trajPoints + j] =
                    poseData.frameInfo[i].trajectoryInfo.trajectoryForwards[j];
            }

            for (int j = 0; j < boneCount; j++)
            {
                flatMotionDataArray[(i * chunkLength) + 2 * trajPoints + j] = poseData.frameInfo[i].pose.jointPositions[j];
                flatMotionDataArray[(i * chunkLength) + 2 * trajPoints + boneCount + j] =
                    poseData.frameInfo[i].pose.jointVelocities[j];
            }

            tempTags[i] = poseData.frameInfo[i].tag;
        }

        motionDataNativeArray = new NativeArray<float3>(poseData.Length * chunkLength,
            Allocator.Persistent);
        costCompareNativeArray = new NativeArray<float3>(chunkLength, Allocator.Persistent);
        motionDataNativeArray.CopyFrom(flatMotionDataArray);
        tagNativeArray = new NativeArray<int>(tempTags, Allocator.Persistent);
        for (int i = 0; i < weights.Length; i++)
        {
            if (i < 2 * trajPoints)
                weights[i] *= trajectoryToPoseRatio;
            else
                weights[i] *= (1 - trajectoryToPoseRatio);
        }

        weightNativeArray = new NativeArray<float>(weights, Allocator.Persistent);


        
    }

    private void Start()
    {
        StartMotionMatching();
    }


    private void OnDisable()
    {
        motionDataNativeArray.Dispose();
        weightNativeArray.Dispose();
        tagNativeArray.Dispose();
        costCompareNativeArray.Dispose();
    }

    private IEnumerator QueryForPose()
    {
        while (true)
        {

            costCompareNativeArray.CopyFrom(CreateDesiredChunk(chunkLength));

            NativeArray<float> result = new NativeArray<float>(poseData.Length, Allocator.TempJob);
            CostJob cJob = new CostJob
            {
                positions = motionDataNativeArray,
                compare = costCompareNativeArray,
                weights = weightNativeArray,
                tags = tagNativeArray,
                desiredTag = (int) ignoreTag,
                result = result
            };
            JobHandle handle = cJob.Schedule(result.Length, chunkLength);
            handle.Complete();


            float best = float.MaxValue;
            for (int i = 0; i < cJob.result.Length; i++)
            {
                if (cJob.result[i] < best)
                {
                    best = cJob.result[i];
                    bestIndex = i;
                }
            }

            //result.CopyTo(resultSnapshot);
            
            result.Dispose();


            var isBanned = IsFrameTooClose(bestIndex,0.5f);
            //Debug.Log("frame: " + bestIndex + ", " + isBanned);

            if (!isBanned)
            {
                PlayAtUniqueFrame(bestIndex);

                //int[] banRange = poseData.GetStartAndLength(bestIndex, 0, 30);
                //Debug.Log(banRange[0] + " - " + banRange[1]);
                /*NativeSlice<int> banSlice = new NativeSlice<int>(banArray, banRange[0], banRange[1]);
                TagForBanJob tagForBanJob = new TagForBanJob
                {
                    rangeToBan = banSlice,
                    banLayer = unBanMask
                };
                JobHandle banHandle = tagForBanJob.Schedule(banSlice.Length, 32);
                banHandle.Complete();
                banArray.CopyTo(banArraySnapshot);*/
                //banQueue.Enqueue(new int[] {banRange[0], banRange[1]});
            }

            /*animPhaseIndex++;
            animPhaseIndex %= poseRefreshRate;
            unBanMask = (1 << animPhaseIndex);*/
            yield return new WaitForSeconds(1f / poseRefreshRate);
        }
    }

/*    private bool IsFrameBanned(int frame)
    {
        foreach (var pair in banQueue)
        {
            if (frame >= pair[0] && frame <= pair[0] + pair[1])
                return true;
        }

        return false;
    }*/

    private bool IsFrameTooClose(int frame, float threshold)
    {
        float currentAnimTime = animator.GetCurrentAnimatorStateInfo(0).normalizedTime;
        AnimLookup aLookup = poseData.GetAnimationAtFrame(frame);
        return (aLookup.Value == current && Mathf.Abs((aLookup.Key / 30f) - currentAnimTime) < threshold);
    }

    public void StartMotionMatching()
    {
        if (isMotionMatchingRunning) return;
        StartCoroutine(nameof(QueryForPose));
        isMotionMatchingRunning = true;
    }

    public void StopMotionMatching()
    {
        if (!isMotionMatchingRunning) return;
        StopCoroutine(nameof(QueryForPose));
        animator.Play("Exit Proxy");
        isMotionMatchingRunning = false;
        bestIndex = 0;
    }

    private void PlayAtUniqueFrame(int frame)
    {
        int layer = isDefault ? 0 : animator.GetLayerIndex("Motion Matching");
        AnimLookup toPlay = poseData.GetAnimationAtFrame(frame);
        animator.CrossFadeInFixedTime(toPlay.Value, crossFadeTime, layer, toPlay.Key / 30f);
        //Debug.Log("playing " + toPlay.Value + " at frame " + toPlay.Key);
        current = toPlay.Value;
    }

    private void PlayMMAnim(AnimLookup candidate)
    {
        animator.CrossFadeInFixedTime(candidate.Value, crossFadeTime, 0, candidate.Key / 30f);
        current = candidate.Value;
    }

    [BurstCompile]
    private struct CostJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> positions;
        [ReadOnly] public NativeArray<float3> compare;
        [ReadOnly] public NativeArray<float> weights;
        [ReadOnly] public NativeArray<int> tags;
        [ReadOnly] public int desiredTag;
        public NativeArray<float> result;

        public void Execute(int index)
        {
            int chunkLength = compare.Length;
            int ignoreTagMultiplier = ((tags[index] & desiredTag) >> 31) - (-(tags[index] & desiredTag) >> 31);

            for (int j = 0; j < compare.Length; j++)
            {
                result[index] += math.distance(compare[j], positions[index * chunkLength + j]) * weights[j];
            }

            result[index] += (ignoreTagMultiplier * 100f);
        }
    }
    
    [BurstCompile]
    public struct UnbanAllJob : IJobParallelFor
    {
        public NativeArray<int> banTagArray;
        public int unbanLayer;

        public void Execute(int index)
        {
            banTagArray[index] &= ~unbanLayer;
        }
    }

    [BurstCompile]
    public struct TagForBanJob : IJobParallelFor
    {
        public NativeSlice<int> rangeToBan;
        public int banLayer;

        public void Execute(int index)
        {
            rangeToBan[index] |= banLayer;
        }
    }


    private float3[] CreateDesiredChunk(int size) // not dynamic
    {
        float3[] chunkArr = new float3[size];
        var desiredTraj = predictor.PredictTrajectory();
        var currentPose = predictor.GetPose();
        for (int j = 0; j < desiredTraj.trajectoryPoints.Length; j++)
        {
            chunkArr[j] = desiredTraj.trajectoryPoints[j];
            chunkArr[j + 4] = desiredTraj.trajectoryForwards[j];
        }

        for (int j = 0; j < currentPose.jointPositions.Count; j++)
        {
            chunkArr[j + 7] = currentPose.jointPositions[j];
            chunkArr[j + 11] = currentPose.jointVelocities[j];
        }

        return chunkArr;
    }

    private void OnAnimatorMove()
    {
        if (!applyRootMotion) return;
        transform.position = animator.rootPosition;
        transform.rotation = animator.rootRotation;
    }

    private void OnDrawGizmos()
    {
        for (var i = 0; i < poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints.Length; i++)
        {
            var position = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints[i];
            var fwd = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryForwards[i];
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(transform.position + position, 1f / 10);
            DrawArrow(transform.position + position, fwd, 1f);
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 fwd, float scale)
    {
        pos.y += 1f / 10;
        Gizmos.DrawRay(pos, Vector3.up / 10f);
        pos.y += 1f / 10;
        float arrowHeadScale = scale / 5;
        Gizmos.DrawRay(pos, fwd * scale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 160f + 1f, 0) * fwd * arrowHeadScale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 200f - 1f, 0) * fwd * arrowHeadScale);
    }
}