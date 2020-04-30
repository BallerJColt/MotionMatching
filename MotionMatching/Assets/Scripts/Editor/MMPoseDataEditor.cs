using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

//[CustomEditor(typeof(MMMotionData))]
public class MMPoseDataEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MMMotionData motionData = (MMMotionData) target;
        GUILayout.Label("Framerate: " + motionData.config.frameRate);
        GUILayout.Space(10f);
        GUILayout.Label("Trajectory ponts: " + motionData.config.trajectoryTimePoints.Count);
        foreach (var point in motionData.config.trajectoryTimePoints)
        {
            GUILayout.Label(point + "s");
        }
        GUILayout.Space(10f);
        GUILayout.Label("Tracked joints: " + motionData.config.trackedBones.Count);
        foreach (var joint in motionData.config.trackedBones)
        {
            GUILayout.Label(joint.ToString());
        }
        
        GUILayout.Space(10f);
        GUILayout.Label("Animation clips: " + motionData.satelliteData.Length);
        foreach (var data in motionData.satelliteData)
        {
            GUILayout.Label("Animation name: " + data.Value + ". Start frame: " + data.Key);
        }
        
        GUILayout.Space(10f);
        GUILayout.Label("Poses: " + motionData.Length);
    }
}
