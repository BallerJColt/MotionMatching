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
    private void Awake()
    {
        trajPoints = poseData.config.trajectoryTimePoints.Count;
        boneCount = poseData.config.trackedBones.Count;

        trajectoryNativeArray = new NativeArray<float3>(BuildTrajectoryArray(), Allocator.Persistent);

        trajWeightNativeArray = new NativeArray<float>(trajWeights, Allocator.Persistent);
        poseWeightNativeArray = new NativeArray<float>(poseWeights, Allocator.Persistent);

        trajCostCompareNativeArray = new NativeArray<float3>(2 * trajPoints, Allocator.Persistent);
        poseCostCompareNativeArray = new NativeArray<float3>(2 * boneCount, Allocator.Persistent);
        
        banArray = new NativeArray<int>(poseData.Length,Allocator.Persistent);
    }

    private void Start()
    {
        StartMotionMatching();
    }

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
            //if (animPhaseIndex % poseRefreshRate == 0)
            //{
            //    foreach (var alma in tagm)
            //    {
            //        Debug.Log("ban multip: " + alma);
            //    }
            //}

            tagm.Dispose();
            trajResult.Dispose();

            NativeArray<float3> bestPoses = new NativeArray<float3>(BuildBestPoseArray(poseIndices),Allocator.TempJob);
            poseCostCompareNativeArray.CopyFrom(CreateFlatPoseArray(2*boneCount));
            NativeArray<float> poseResults = new NativeArray<float>(bestTrajectoryAmount,Allocator.TempJob);
            
            PoseCostJob poseJob = new PoseCostJob
            {
                bestPoseArray = bestPoses,
                currentPose = poseCostCompareNativeArray,
                poseWeights = poseWeightNativeArray,
                result = poseResults
            };
            
            NativeArray<int> bestIndexArr = new NativeArray<int>(1,Allocator.TempJob);
            
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
    
    public int[] SortShit(NativeArray<float> results)
    {
        int[] indices = new int[bestTrajectoryAmount];
        int k = 0;
        for (int i = 0; i < results.Length; i++)
        {
            if (k < indices.Length - 1) //indices not full
            {
                if (results[i] >= results[indices[k]])
                {
                    //Debug.Log("array not full, placing " + results[i] + " at end of array");
                    //current is higher than highest in sorted array
                    //fill up array by concatenating index at end
                    k++;
                    //Debug.Log("array not full, replacing " + indices[k] + "with " + i);
                    indices[k] = i;
                }
                else
                {
                    //fill up array by sorting stuff
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
                    //Debug.Log("value " + results[i] + " lower than " + results[indices[k]]);
                    //sort stuff
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

        return indices;
    }
    
    private void OnDrawGizmos()
    {
        for (var i = 0; i < poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints.Length; i++)
        {
            var position = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryPoints[i];
            var fwd = poseData.frameInfo[bestIndex].trajectoryInfo.trajectoryForwards[i];
            Gizmos.color = Color.green;
            Gizmos.DrawWireSphere(Vector3.zero + position, 1f / 10);
            DrawArrow(Vector3.zero + position, fwd, 1f);
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