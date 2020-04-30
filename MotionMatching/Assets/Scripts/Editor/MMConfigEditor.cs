using System.Collections;
using System.Collections.Generic;
using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(MMConfig))]
public class MMConfigEditor : Editor
{
    public override void OnInspectorGUI()
    {
        MMConfig config = (MMConfig) target;
        base.OnInspectorGUI();
        if (GUILayout.Button("Lol"))
        {
            config.CreateEnum("IgnoreTags", config.ignoreTags);
            config.CreateEnum("FavourTags", config.favourTags);
        }
    }
}