#if UNITY_EDITOR
using System.Collections;
using System.Collections.Generic;
using System.Diagnostics.Tracing;
using System.Linq;
using UnityEditor;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMMPreProcessor", menuName = "Motion Matching/PreProcessor", order = 10)]
public class MMPreProcessor : ScriptableObject
{
    public IgnoreTags ignoreTags;
    public MMConfig config;
    public GameObject mmAvatar;
    public Transform rootJoint;
    public AnimationClip clip;

    public MMAnimationClip[] mmAnimationClips;
//    public string[] tags = new string[32];

    public BaseTrajectory[] CreateBaseTrajectoryArray(AnimationClip animClip)
    {
        if (animClip == null)
            return new BaseTrajectory[0];
        float animLength = animClip.length;
        float timeStep = 1f / config.frameRate;
        float sampleTime = 0f;
        BaseTrajectory[] baseTrajectory = new BaseTrajectory[(int) animLength * config.frameRate];
        for (int i = 0; i < baseTrajectory.Length; i++)
        {
            animClip.SampleAnimation(mmAvatar, sampleTime);
            Transform temp = rootJoint.transform;
            baseTrajectory[i] = new BaseTrajectory(temp.worldToLocalMatrix, temp.position, temp.forward, sampleTime);
            sampleTime += timeStep;
        }

        return baseTrajectory;
    }

    public Pose[] CreateBasePoseArray(AnimationClip animClip)
    {
        if (animClip == null)
            return new Pose[0];

        Animator animator = mmAvatar.GetComponent<Animator>();
        float animLength = animClip.length;
        float timeStep = 1f / config.frameRate;
        float sampleTime = 0f;
        Pose[] basePoseArray = new Pose[(int) animLength * config.frameRate];
        for (int i = 0; i < basePoseArray.Length; i++)
        {
            animClip.SampleAnimation(mmAvatar, sampleTime);
            Vector3 temp = rootJoint.position;
            List<Vector3> tempWorldPositions = new List<Vector3>();
            List<Vector3> tempWorldVelocities = new List<Vector3>();
            int j = 0;
            foreach (var bone in config.trackedBones)
            {
                Debug.Log(animator.GetBoneTransform(bone));
                tempWorldPositions.Add(animator.GetBoneTransform(bone).position - temp);
                if (i == 0)
                    tempWorldVelocities.Add(Vector3.zero);
                else
                {
                    tempWorldVelocities.Add((tempWorldPositions[j] - basePoseArray[i - 1].jointPositions[j]) *
                                            config.frameRate);
                }
            }

            basePoseArray[i] = new Pose(tempWorldPositions, tempWorldVelocities);
            sampleTime += timeStep;
        }

        return basePoseArray;
    }

    public void PreProcess()
    {
        Debug.Log("Creating Asset");
        string path = "Assets/Resources/MotionData";

        if (!AssetDatabase.IsValidFolder(path))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateFolder("Assets/Resources", "MotionData");
        }

        //AnimationScriptableObjectContainer w = new AnimationScriptableObjectContainer();
        MMMotionData motionAsset = CreateInstance<MMMotionData>();
        motionAsset = CreateOrReplaceAsset(motionAsset, path + "/MotionData.asset");

        int clipsLength = 0;
        List<AnimLookup> lookupList = new List<AnimLookup>();
        List<MotionFrameInfo> frameList = new List<MotionFrameInfo>();
        foreach (var mmAnimationClip in mmAnimationClips)
        {
            // Create satellite data for animation lookup
            lookupList.Add(new AnimLookup(clipsLength, mmAnimationClip.animClip.name));
            clipsLength += mmAnimationClip.Length;
            //Create combined array I guess..
            for (int i = 0; i < mmAnimationClip.Length; i++)
            {
                Vector3[] positionPoints = new Vector3[config.trajectoryTimePoints.Count];
                Vector3[] forwardPoints = new Vector3[config.trajectoryTimePoints.Count];
                bool isInvalid = false;
                for (int j = 0; j < config.trajectoryTimePoints.Count; j++)
                {
                    
                    int projectedIndex = Mathf.RoundToInt(config.trajectoryTimePoints[j] * config.frameRate) + i;
                    if (projectedIndex < mmAnimationClip.Length && projectedIndex >= 0)
                    {
                        positionPoints[j] = mmAnimationClip
                            .baseTrajectory[i].rootWorldToLocalMatrix.MultiplyPoint3x4(mmAnimationClip
                                .baseTrajectory[
                                    Mathf.RoundToInt(config.trajectoryTimePoints[j] * config.frameRate) + i]
                                .position);
                        forwardPoints[j] = mmAnimationClip
                            .baseTrajectory[i].rootWorldToLocalMatrix.MultiplyVector(mmAnimationClip
                                .baseTrajectory[
                                    Mathf.RoundToInt(config.trajectoryTimePoints[j] * config.frameRate) + i]
                                .forward);
                    }
                    else
                    {
                        isInvalid = true;
                    }
                }
                
                if(isInvalid)
                {
                    clipsLength--;
                    continue;
                }
                
                List<Vector3> charSpacePositions = new List<Vector3>();
                List<Vector3> charSpaceVelocities = new List<Vector3>();
                for (int j = 0; j < mmAnimationClip.basePoses[i].jointPositions.Count; j++)
                {
                    Vector3 tempPos = mmAnimationClip.baseTrajectory[i].rootWorldToLocalMatrix
                        .MultiplyVector(mmAnimationClip.basePoses[i].jointPositions[j]);
                    Vector3 tempVel = mmAnimationClip.baseTrajectory[i].rootWorldToLocalMatrix
                        .MultiplyVector(mmAnimationClip.basePoses[i].jointVelocities[j]);

                    charSpacePositions.Add(tempPos);
                    charSpaceVelocities.Add(tempVel);
                }

                TrajectoryInfo tempTraj = new TrajectoryInfo(positionPoints, forwardPoints);
                Pose tempPose = new Pose(charSpacePositions, charSpaceVelocities);
                frameList.Add(new MotionFrameInfo(tempTraj, tempPose, 0));
                
            }
        }

        /*motionAsset.trajectoryInfo = trajectories;
        motionAsset.poseInfo = poses;*/
        motionAsset.config = config;
        motionAsset.Length = clipsLength;
        motionAsset.frameInfo = frameList.ToArray();
        motionAsset.satelliteData = lookupList.ToArray();
        AssetDatabase.SaveAssets();
        CreateAnimatorController();
    }

    public MMAnimationClip CreateNewMMAnimationClip(AnimationClip clip)
    {
        Debug.Log("Creating Asset");
        string path = "Assets/Resources/ScriptableClips";

        if (!AssetDatabase.IsValidFolder(path))
        {
            if (!AssetDatabase.IsValidFolder("Assets/Resources"))
            {
                AssetDatabase.CreateFolder("Assets", "Resources");
            }

            AssetDatabase.CreateFolder("Assets/Resources", "ScriptableClips");
        }

        MMAnimationClip animationAsset = CreateInstance<MMAnimationClip>();
        animationAsset = CreateOrReplaceAsset(animationAsset, path + "/" + clip.name + ".asset");
        animationAsset.frameRate = config.frameRate;
        animationAsset.Length = (int) clip.length * config.frameRate;
        animationAsset.animClip = clip;
        animationAsset.baseTrajectory = CreateBaseTrajectoryArray(clip);
        animationAsset.basePoses = CreateBasePoseArray(clip);
        animationAsset.PreformatData();
        //AssetDatabase.CreateAsset(w, path + "/asset12321.asset");
        AssetDatabase.SaveAssets();
        return animationAsset;
    }

    T CreateOrReplaceAsset<T>(T newAsset, string newPath) where T : Object
    {
        T existingAsset = AssetDatabase.LoadAssetAtPath<T>(newPath);

        if (existingAsset == null)
        {
            AssetDatabase.CreateAsset(newAsset, newPath);
            existingAsset = newAsset;
        }
        else
        {
            EditorUtility.CopySerialized(newAsset, existingAsset);
        }

        return existingAsset;
    }

    private void CreateAnimatorController()
    {
        var controller =
            UnityEditor.Animations.AnimatorController.CreateAnimatorControllerAtPath(
                "Assets/AnimatorControllers/MMStates.controller");
        // Add StateMachines
        var rootStateMachine = controller.layers[0].stateMachine;
        rootStateMachine.AddState("Empty State");

        foreach (var mmClip in mmAnimationClips)
        {
            controller.AddMotion(mmClip.animClip);
        }
    }
}
#endif