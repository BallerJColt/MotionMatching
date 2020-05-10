using System;
using System.Collections;
using System.Collections.Generic;
using Unity.Burst;
using Unity.Collections;
using Unity.Jobs;
using Unity.Mathematics;
using UnityEngine;

public class MMSeparatedController : MonoBehaviour
{
    public MMMotionData poseData;
    public MotionMatchable predictor;
    public Animator animator;
    [Range(1, 60)] public int poseRefreshRate = 3;
    [Range(0f, 1f)] public float crossFadeTime = 0.3f;
    public bool isMotionMatchingRunning;
    private NativeArray<float3> trajectoryNativeArray;
    private NativeArray<float> trajWeightNativeArray;
    private NativeArray<float3> trajCostCompareNativeArray;
    private NativeArray<float> poseWeightNativeArray;
    private NativeArray<float3> poseCostCompareNativeArray;
    private NativeArray<int> banArray;
    public int bestTrajectoryAmount;
    [Range(0f, 1f)] public float banThreshold = 0.3f;
    public float[] trajWeights;
    public float[] poseWeights;
    private int trajPoints;
    private int boneCount;
    private int chunkLength;
    public int bestIndex;
    public string current;
    public int animPhaseIndex;
    public int unBanMask;
    public int prevBanRange;
    public int nextBanRange;
    public int banSeconds;
    public int lookAheadFrames = 10;
    public float cumulativeErrorThreshold;
    public float currentCumulativeError;
    private int currentFrame;
    public bool showBestTrajectory;
    public bool showLookAheadTrajectory;
    private void Awake()
    {
        trajPoints = poseData.config.trajectoryTimePoints.Count;
        boneCount = poseData.config.trackedBones.Count;

        trajectoryNativeArray = new NativeArray<float3>(BuildTrajectoryArray(), Allocator.Persistent);

        trajWeightNativeArray = new NativeArray<float>(trajWeights, Allocator.Persistent);
        poseWeightNativeArray = new NativeArray<float>(poseWeights, Allocator.Persistent);

        trajCostCompareNativeArray = new NativeArray<float3>(2 * trajPoints, Allocator.Persistent);
        poseCostCompareNativeArray = new NativeArray<float3>(2 * boneCount, Allocator.Persistent);

        banArray = new NativeArray<int>(poseData.Length, Allocator.Persistent);
    }

    private void Start()
    {
        StartMotionMatching();
    }

    /*private void FixedUpdate()
    {
        
        Debug.Log(animator.GetCurrentAnimatorStateInfo(0).IsName(current));
        realCurrentFrame = GetTrajectoryFrame();
    }*/

    private void OnDisable()
    {
        trajectoryNativeArray.Dispose();
        trajWeightNativeArray.Dispose();
        poseWeightNativeArray.Dispose();
        trajCostCompareNativeArray.Dispose();
        poseCostCompareNativeArray.Dispose();
        banArray.Dispose();
    }

    private void PlayAtUniqueFrame(int frame)
    {
        AnimLookup toPlay = poseData.GetAnimationAtFrame(frame);
        animator.CrossFadeInFixedTime(toPlay.Value, crossFadeTime, 0, toPlay.Key / 30f);
        current = toPlay.Value;
        currentFrame = frame;
    }

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

    private IEnumerator QueryForPose()
    {
        while (true)
        {
            trajCostCompareNativeArray.CopyFrom(CreateFlatTrajectoryArray(2 * trajPoints));
            currentFrame = Mathf.Clamp(currentFrame+3,0,poseData.Length-lookAheadFrames-1);
            //Calculate cumulative trajectory error, if it's below threshold, just keep playing the current animation
            NativeArray<float> errorResult = new NativeArray<float>(1, Allocator.TempJob);

            //currentFrame = GetTrajectoryFrameFromAnimation();
            // int lastFrameInClip =
            //     Mathf.RoundToInt(animator.GetCurrentAnimatorStateInfo(0).length * poseData.config.frameRate);
            // //int endFrame = Mathf.Min(currentFrame + (2 * trajPoints) * lookAheadFrames, lastFrameInClip);
            // int endFrame = currentFrame + (2 * trajPoints) * lookAheadFrames;
            // NativeSlice<float3> trajectorySlice =
            //     new NativeSlice<float3>(trajectoryNativeArray, currentFrame, endFrame);
            // CumulativeErrorJob errorJob = new CumulativeErrorJob
            // {
            //     predictedTrajectory = trajCostCompareNativeArray,
            //     continuedMovementTrajectories = trajectorySlice,
            //     trajectoryWeights = trajWeightNativeArray,
            //     result = errorResult
            // };

            // BIG DEBUG STUFF

            NativeSlice<float3> singleSlice =
                new NativeSlice<float3>(trajectoryNativeArray, 8 * (currentFrame+lookAheadFrames), 8);
            float dist = 0f;
            var alma = predictor.PredictTrajectory();
            float natDist = 0f;
            /*for (int i = 0; i < 4; i++)
            {
                var point = alma.trajectoryPoints[i];
                Debug.Log("predicted trajectory points: " + point);
                Debug.Log("animation trajectory points" +
                          poseData.frameInfo[currentFrame].trajectoryInfo.trajectoryPoints[i]);
                dist += Vector3.SqrMagnitude(alma.trajectoryPoints[i] -
                                             poseData.frameInfo[currentFrame].trajectoryInfo.trajectoryPoints[i]);
                dist += Vector3.SqrMagnitude(alma.trajectoryForwards[i] -
                                             poseData.frameInfo[currentFrame].trajectoryInfo.trajectoryForwards[i]);
                Debug.Log("predicted pos in nativearray: " + trajCostCompareNativeArray[i]);
                Debug.Log("animation pos in nativearray: " + singleSlice[i]);
                natDist += math.distancesq(trajCostCompareNativeArray[i], singleSlice[i]);
                natDist += math.distancesq(trajCostCompareNativeArray[4+i], singleSlice[4+i]);
                
            }

            Debug.Log("sqr distance: " + dist);
            Debug.Log("native sq dist: " + natDist);*/

            //Keep animation frames rolling, add look-ahead again!
            
            ErrorForNextFrameJob singleFrameErrorJob = new ErrorForNextFrameJob
            {
                predictedTrajectory = trajCostCompareNativeArray,
                continuedMovementTrajectories = singleSlice,
                trajectoryWeights = trajWeightNativeArray,
                result = errorResult,
            };


            JobHandle errorJobHandle = singleFrameErrorJob.Schedule(trajCostCompareNativeArray.Length, 8);
            errorJobHandle.Complete();
            currentCumulativeError = errorResult[0];
            errorResult.Dispose();
            //Debug.Log("error in job: " + currentCumulativeError);
            if (currentCumulativeError <= cumulativeErrorThreshold)
            {
                //Debug.Log("We close!");
                UnbanAllJob unbanJob = new UnbanAllJob
                {
                    banTagArray = banArray,
                    unbanLayer = unBanMask
                };
                JobHandle unbanHandle = unbanJob.Schedule(banArray.Length, 32);
                unbanHandle.Complete();
            }
            else
            {
                NativeArray<float> trajResult = new NativeArray<float>(poseData.Length, Allocator.TempJob);

                UnbanAllJob unbanJob = new UnbanAllJob
                {
                    banTagArray = banArray,
                    unbanLayer = unBanMask
                };
                NativeArray<int> tagm = new NativeArray<int>(poseData.Length, Allocator.TempJob);
                TrajectoryCostJob trajJob = new TrajectoryCostJob
                {
                    trajectoryFlatArray = trajectoryNativeArray,
                    predictedTrajectory = trajCostCompareNativeArray,
                    trajectoryWeights = trajWeightNativeArray,
                    banFrames = banArray,
                    banMask = unBanMask,
                    result = trajResult,
                    btm = tagm,
                };


                NativeArray<int> poseIndices = new NativeArray<int>(bestTrajectoryAmount, Allocator.TempJob);
                SortTrajectoryJob sortJob = new SortTrajectoryJob
                {
                    results = trajResult,
                    indices = poseIndices
                };

                JobHandle trajHandle = trajJob.Schedule(trajResult.Length, 8);
                JobHandle sortHandle = sortJob.Schedule(trajHandle);
                sortHandle.Complete();
                JobHandle unbanHandle = unbanJob.Schedule(banArray.Length, 32);
                unbanHandle.Complete();

                tagm.Dispose();
                trajResult.Dispose();

                NativeArray<float3> bestPoses =
                    new NativeArray<float3>(BuildBestPoseArray(poseIndices), Allocator.TempJob);
                poseCostCompareNativeArray.CopyFrom(CreateFlatPoseArray(2 * boneCount));
                NativeArray<float> poseResults = new NativeArray<float>(bestTrajectoryAmount, Allocator.TempJob);

                PoseCostJob poseJob = new PoseCostJob
                {
                    bestPoseArray = bestPoses,
                    currentPose = poseCostCompareNativeArray,
                    poseWeights = poseWeightNativeArray,
                    result = poseResults
                };

                NativeArray<int> bestIndexArr = new NativeArray<int>(1, Allocator.TempJob);

                AnimationSelectionJob animJob = new AnimationSelectionJob
                {
                    indices = poseIndices,
                    results = poseResults,
                    selectedIndex = bestIndexArr
                };

                JobHandle poseHandle = poseJob.Schedule(poseResults.Length, 6);
                JobHandle animHandle = animJob.Schedule(poseHandle);
                animHandle.Complete();

                poseIndices.Dispose();
                poseResults.Dispose();
                bestPoses.Dispose();

                bestIndex = bestIndexArr[0];

                bestIndexArr.Dispose();

                var isBanned = IsFrameTooClose(bestIndex, banThreshold);
                if (!isBanned)
                {
                    PlayAtUniqueFrame(bestIndex);

                    int[] banRange = poseData.GetStartAndLength(bestIndex, prevBanRange, nextBanRange);
                    //Debug.Log(banRange[0] + " - " + banRange[1]);
                    NativeSlice<int> banSlice = new NativeSlice<int>(banArray, banRange[0], banRange[1]);
                    TagForBanJob tagForBanJob = new TagForBanJob
                    {
                        rangeToBan = banSlice,
                        banLayer = unBanMask
                    };
                    JobHandle banHandle = tagForBanJob.Schedule(banSlice.Length, 32);
                    banHandle.Complete();
                }

                animPhaseIndex++;
                animPhaseIndex %= (banSeconds * poseRefreshRate);
                unBanMask = (1 << animPhaseIndex);
            }

            yield return new WaitForSeconds(1f / poseRefreshRate);
        }
    }

    [BurstCompile]
    public struct TrajectoryCostJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> trajectoryFlatArray;
        [ReadOnly] public NativeArray<float3> predictedTrajectory;
        [ReadOnly] public NativeArray<float> trajectoryWeights;
        [ReadOnly] public NativeArray<int> banFrames;
        [ReadOnly] public int banMask;
        public NativeArray<float> result;
        public NativeArray<int> btm;

        public void Execute(int index)
        {
            int chunkLength = predictedTrajectory.Length;

            int banTagMultiplier = ((banFrames[index] & banMask) >> 31) - (-(banFrames[index] & banMask) >> 31);
            btm[index] = banTagMultiplier;

            for (int j = 0; j < chunkLength; j++)
            {
                result[index] += math.distancesq(predictedTrajectory[j], trajectoryFlatArray[index * chunkLength + j]) *
                                 trajectoryWeights[j];
            }

            result[index] += banTagMultiplier * 100f;
        }
    }

    [BurstCompile]
    public struct SortTrajectoryJob : IJob
    {
        [ReadOnly] public NativeArray<float> results;
        public NativeArray<int> indices;

        public void Execute()
        {
            int k = 0;
            for (int i = 0; i < results.Length; i++)
            {
                if (k < indices.Length - 1)
                {
                    if (results[i] >= results[indices[k]])
                    {
                        k++;
                        indices[k] = i;
                    }
                    else
                    {
                        for (int j = k; j >= 0; j--)
                        {
                            if (results[i] < results[indices[j]])
                            {
                                indices[j + 1] = indices[j];
                                indices[j] = i;
                            }
                            else
                            {
                                break;
                            }
                        }

                        k++;
                    }
                }
                else
                {
                    if (results[i] < results[indices[k]])
                    {
                        for (int j = k - 1; j >= 0; j--)
                        {
                            if (results[i] < results[indices[j]])
                            {
                                indices[j + 1] = indices[j];
                                indices[j] = i;
                            }
                            else
                            {
                                break;
                            }
                        }
                    }
                }
            }
        }
    }

    [BurstCompile]
    public struct PoseCostJob : IJobParallelFor
    {
        [ReadOnly] public NativeArray<float3> bestPoseArray;
        [ReadOnly] public NativeArray<float3> currentPose;
        [ReadOnly] public NativeArray<float> poseWeights;
        public NativeArray<float> result;

        public void Execute(int index)
        {
            int chunkLength = currentPose.Length;
            for (int j = 0; j < chunkLength; j++)
            {
                result[index] += math.distancesq(currentPose[j], bestPoseArray[index * chunkLength + j]) *
                                 poseWeights[j];
            }
        }
    }

    [BurstCompile]
    public struct AnimationSelectionJob : IJob
    {
        [ReadOnly] public NativeArray<float> results;
        [ReadOnly] public NativeArray<int> indices;
        public NativeArray<int> selectedIndex;

        public void Execute()
        {
            float best = float.MaxValue;
            for (int i = 0; i < results.Length; i++)
            {
                if (results[i] < best)
                {
                    selectedIndex[0] = indices[i];
                    best = results[i];
                }
            }
        }
    }

    private float3[] BuildTrajectoryArray()
    {
        int cL = trajPoints * 2; //8
        float3[] trajArr = new float3[poseData.Length * cL];

        for (int i = 0; i < poseData.Length; i++)
        {
            for (int j = 0; j < trajPoints; j++)
            {
                trajArr[(i * cL) + j] = poseData.frameInfo[i].trajectoryInfo.trajectoryPoints[j];
                trajArr[(i * cL) + trajPoints + j] = poseData.frameInfo[i].trajectoryInfo.trajectoryForwards[j];
            }
        }

        return trajArr;
    }

    private float3[] BuildBestPoseArray(NativeArray<int> indices)
    {
        int cL = 2 * boneCount;
        float3[] poseArr = new float3[cL * indices.Length];
        for (int i = 0; i < indices.Length; i++)
        {
            for (int j = 0; j < boneCount; j++)
            {
                poseArr[(i * cL) + j] = poseData.frameInfo[indices[i]].pose.jointPositions[j];
                poseArr[(i * cL) + boneCount + j] = poseData.frameInfo[indices[i]].pose.jointPositions[j];
            }
        }

        return poseArr;
    }


    private float3[] CreateFlatTrajectoryArray(int size) // not dynamic, has to be 8
    {
        float3[] chunkArr = new float3[size];
        var desiredTraj = predictor.PredictTrajectory();
        for (int j = 0; j < desiredTraj.trajectoryPoints.Length; j++)
        {
            chunkArr[j] = desiredTraj.trajectoryPoints[j];
            chunkArr[j + 4] = desiredTraj.trajectoryForwards[j];
        }

        return chunkArr;
    }

    private float3[] CreateFlatPoseArray(int size) // not dynamic, has to be 6
    {
        float3[] chunkArr = new float3[size];
        var currentPose = predictor.GetPose();
        for (int j = 0; j < currentPose.jointPositions.Count; j++)
        {
            chunkArr[j] = currentPose.jointPositions[j];
            chunkArr[j + 3] = currentPose.jointVelocities[j];
        }

        return chunkArr;
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

    [BurstCompile]
    public struct CumulativeErrorJob : IJobParallelFor
    {
        [ReadOnly] public NativeSlice<float3> continuedMovementTrajectories;
        [ReadOnly] public NativeArray<float3> predictedTrajectory;
        [ReadOnly] public NativeArray<float> trajectoryWeights;
        public NativeArray<float> result;

        public void Execute(int index)
        {
            int chunkLength = predictedTrajectory.Length;
            for (int j = 0; j < chunkLength; j++)
            {
                result[0] += math.distancesq(predictedTrajectory[j],
                                 continuedMovementTrajectories[index * chunkLength + j]) *
                             trajectoryWeights[j];
            }
        }
    }

    [BurstCompile]
    public struct ErrorForNextFrameJob : IJobParallelFor
    {
        [ReadOnly] public NativeSlice<float3> continuedMovementTrajectories;
        [ReadOnly] public NativeArray<float3> predictedTrajectory;
        [ReadOnly] public NativeArray<float> trajectoryWeights;
        public NativeArray<float> result;

        public void Execute(int index)
        {
            result[0] += math.distancesq(predictedTrajectory[index], continuedMovementTrajectories[index]) *
                         trajectoryWeights[index];
        }
    }

    private int GetCurrentAnimatorFrame()
    {
        var currentState = animator.GetCurrentAnimatorStateInfo(0);
        int currentFrame =
            Mathf.RoundToInt(currentState.length * currentState.normalizedTime * poseData.config.frameRate);
        return currentFrame;
    }

    private int GetTrajectoryFrameFromAnimation()
    {
        int currentAnimFrame = GetCurrentAnimatorFrame();
        int trajFrame = 0;

        AnimatorClipInfo[] currentClips = animator.GetCurrentAnimatorClipInfo(0);
        float highest = float.MinValue;
        string clipName = "";
        for (int i = 0; i < currentClips.Length; i++)
        {
            if (currentClips[i].weight > highest)
            {
                highest = currentClips[i].weight;
                clipName = currentClips[i].clip.name;
            }
        }

        Debug.Log(currentClips.Length);
        int j = 0;
        while (j < poseData.satelliteData.Length && current != poseData.satelliteData[j].Value)
        {
            trajFrame += poseData.satelliteData[j].Key;
            j++;
        }

        trajFrame += currentAnimFrame;
        return trajFrame;
    }

    private int GetTrajectoryFrame()
    {
        AnimLookup[] satData = poseData.satelliteData;
        int frameCount = 0;
        for (int i = 0; i < satData.Length; i++)
        {
            if (animator.GetCurrentAnimatorStateInfo(0).IsName(satData[i].Value))
            {
                Debug.Log("current animation playing is " + satData[i].Value);
                break;
            }

            frameCount += satData[i].Key;
        }

        frameCount += GetCurrentAnimatorFrame();
        return frameCount;
    }

    private void OnDrawGizmos()
    {

        if (showBestTrajectory)
        {
            for (var i = 0; i < poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints.Length; i++)
            {
                var position = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints[i];
                var fwd = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryForwards[i];
                Gizmos.color = Color.green;
                Gizmos.DrawWireSphere(Quaternion.LookRotation(transform.forward)* position + transform.position, 1f / 10);
                DrawArrow(Quaternion.LookRotation(transform.forward)* position + transform.position, Quaternion.LookRotation(transform.forward)*fwd, 1f);
            }
        }

        if (showLookAheadTrajectory)
        {

            for (var i = 0; i < poseData.frameInfo[currentFrame+lookAheadFrames].trajectoryInfo.trajectoryPoints.Length; i++)
            {
                var position = poseData.frameInfo[currentFrame+lookAheadFrames].trajectoryInfo.trajectoryPoints[i];
                var fwd = poseData.frameInfo[currentFrame+lookAheadFrames].trajectoryInfo.trajectoryForwards[i];
                Gizmos.color = Color.magenta;
                Gizmos.DrawWireSphere(Quaternion.LookRotation(transform.forward)* position + transform.position, 1f / 10);
                DrawArrow(Quaternion.LookRotation(transform.forward)* position + transform.position, Quaternion.LookRotation(transform.forward)*fwd, 1f);
            }
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