using System;
using System.Collections;
using System.Collections.Generic;
using KinematicCharacterController;
using KinematicTest.controller;
using UnityEngine;

public class KinematicTrajectoryPredictor : MotionMatchable
{
    private int MaxDiscreteCollisionIterations = 3;
    private Collider[] _probedColliders = new Collider[8];
    public KinematicCharacterMotor motor;
    public KinematicTestController controller;
    public Animator animator;
    public MMConfig config;
    public bool showGizmos;
    public Transform meshRoot;
    public Transform rootJoint;
    public Vector3[] trajectoryPoints;
    public Vector3[] trajectoryForwards;
    private Vector3[] tempPositions;
    private Vector3[] velocities;

    private void Awake()
    {
        trajectoryPoints = new Vector3[config.trajectoryTimePoints.Count];
        trajectoryForwards = new Vector3[config.trajectoryTimePoints.Count];
        velocities = new Vector3[config.trackedBones.Count];
        tempPositions = new Vector3[config.trackedBones.Count];
    }

    private void FixedUpdate()
    {
        Matrix4x4 worldToLocalMatrix = meshRoot.worldToLocalMatrix;
        for (int i = 0; i < config.trackedBones.Count; i++)
        {
            HumanBodyBones bone = config.trackedBones[i];

            velocities[i] = worldToLocalMatrix.inverse.MultiplyVector(
                                (worldToLocalMatrix.MultiplyPoint3x4(animator.GetBoneTransform(bone).position) -
                                 tempPositions[i])) / Time.deltaTime;

            tempPositions[i] = worldToLocalMatrix.MultiplyPoint3x4(animator.GetBoneTransform(bone).position);
        }

        PredictTrajectory();
        GetPose();
    }

    public override Pose GetPose()
    {
        List<Vector3> posList = new List<Vector3>();
        List<Vector3> velList = new List<Vector3>();
        for (int i = 0; i < velocities.Length; i++)
        {
            var bone = config.trackedBones[i];
            posList.Add(meshRoot.worldToLocalMatrix.MultiplyPoint3x4(animator.GetBoneTransform(bone).position));
            velList.Add(velocities[i]);
        }

        return new Pose(posList, velList);
    }

    public override TrajectoryInfo PredictTrajectory()
    {
        GenerateTrajectoryPoints();
        GenerateTrajectoryForwards();
        TransformTrajectoryToLocal();
        TransformTrajectoryForwardsToLocal();
        return new TrajectoryInfo(trajectoryPoints, trajectoryForwards);
    }

    private void GenerateTrajectoryPoints()
    {
        for (int i = 0; i < config.trajectoryTimePoints.Count; i++)
        {
            trajectoryPoints[i] = ExplicitEuler(config.trajectoryTimePoints[i]);
        }
    }

    public void TransformTrajectoryToLocal()
    {
        for (var i = 0; i < trajectoryPoints.Length; i++)
        {
            Vector3 point = trajectoryPoints[i];
            trajectoryPoints[i] = meshRoot.worldToLocalMatrix.MultiplyPoint3x4(point);
        }
    }

    public void TransformTrajectoryForwardsToLocal()
    {
        for (var i = 0; i < trajectoryForwards.Length; i++)
        {
            Vector3 fwd = trajectoryForwards[i];
            trajectoryForwards[i] = meshRoot.worldToLocalMatrix.MultiplyVector(fwd);
        }
    }

    private void GenerateTrajectoryForwards()
    {
        trajectoryForwards[0] = (trajectoryPoints[0] - meshRoot.position).normalized;
        for (int i = 1; i < trajectoryPoints.Length; i++)
        {
            trajectoryForwards[i] = (trajectoryPoints[i] - trajectoryPoints[i - 1]).normalized;
        }
    }

    
    private Vector3 ExplicitEuler(float deltaTime)
    {
        Vector3 grav = controller.Gravity;
        Vector3 velocity = motor.BaseVelocity;
        if (!motor.GroundingStatus.IsStableOnGround && !motor.GroundingStatus.FoundAnyGround ||
            controller.JumpingThisFrame())
            velocity = velocity + grav * deltaTime;


        Vector3 position = meshRoot.position;
        Vector3 predictedPosition = position + velocity * deltaTime;
        ProjectPredictedPosition(predictedPosition);
        return predictedPosition;
    }

    private Vector3 ProjectPredictedPosition(Vector3 predictedPosition)
    {
        var alma = motor.CharacterOverlap(predictedPosition, Quaternion.identity, _probedColliders,
            motor.CollidableLayers,
            QueryTriggerInteraction.Ignore);
        if (alma > 0)
        {
            Gizmos.color = Color.red;
        }
        else
        {
            Gizmos.color = Color.white;
        }

        return Vector3.zero;
    }

    private void OnDrawGizmos()
    {
        if (showGizmos && trajectoryPoints != null)
        {
            for (var i = 0; i < trajectoryPoints.Length; i++)
            {
                var v = trajectoryPoints[i];
                var fwd = trajectoryForwards[i];
                Gizmos.DrawWireSphere(v, 0.2f);
                DrawArrow(v, fwd, 1);
            }


            if (animator == null || velocities == null)
                return;

            for (int i = 0; i < config.trackedBones.Count; i++)
            {
                var bone = config.trackedBones[i];
                Gizmos.color = Color.magenta;
                Gizmos.DrawRay(animator.GetBoneTransform(bone).position, velocities[i]);
                Gizmos.color = Color.white;
                Gizmos.DrawLine(Vector3.zero,
                    rootJoint.worldToLocalMatrix.MultiplyPoint3x4(animator.GetBoneTransform(bone).position));
            }
        }
    }

    private void DrawArrow(Vector3 pos, Vector3 fwd, float scale)
    {
        pos.y += 1f / 10;
        Gizmos.DrawRay(pos, Vector3.up * 1f / 10);
        pos.y += 1f / 10;
        float arrowHeadScale = scale / 5;
        Gizmos.DrawRay(pos, fwd * scale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 160f + 1f, 0) * fwd * arrowHeadScale);
        Gizmos.DrawRay(pos + fwd * scale, Quaternion.Euler(0, 200f - 1f, 0) * fwd * arrowHeadScale);
    }
}