using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MMAnimationClip))]
public class MMAnimClipEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MMAnimationClip mmClip = (MMAnimationClip) target;

        GUILayout.Label("Clip name: " + mmClip.animClip.name);
        GUILayout.Label("Animation Length: " + mmClip.animClip.length + "s");
        GUILayout.Label("Processed frames: " + mmClip.Length);
    }
}