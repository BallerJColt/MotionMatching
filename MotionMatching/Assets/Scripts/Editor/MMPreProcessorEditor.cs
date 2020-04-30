using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEditor;
using System.Linq;

[CustomEditor(typeof(MMPreProcessor))]
public class CustomScriptableObjectEditor : Editor
{
    private MMPreProcessor mmPreProcessor;

    public override void OnInspectorGUI()
    {
        base.OnInspectorGUI();
        mmPreProcessor = (MMPreProcessor) target;

        AnimationClip myClip = mmPreProcessor.clip;
        if (GUILayout.Button("Preprocess"))
        {
            //Debug.Log("Amount of clips : " + mmPreProcessor.name);
            mmPreProcessor.PreProcess();
            //AssetDatabase.GetAssetPath();
        }


        if (GUI.changed)
        {
            if (mmPreProcessor.clip != null)
            {
//                mmPreProcessor.CreateBasePose(mmPreProcessor.clip);

                MMAnimationClip container = mmPreProcessor.CreateNewMMAnimationClip(mmPreProcessor.clip);
                if (mmPreProcessor.mmAnimationClips.Contains(container))
                {
                    int index = System.Array.IndexOf(mmPreProcessor.mmAnimationClips, container);
                    mmPreProcessor.mmAnimationClips[index] = container;
                    List<MMAnimationClip> tempList = mmPreProcessor.mmAnimationClips.ToList();
                    for (var i = tempList.Count - 1; i > -1; i--)
                    {
                        if (tempList[i] == null)
                            tempList.RemoveAt(i);
                    }
                    mmPreProcessor.mmAnimationClips = tempList.ToArray();
                }
                else
                {
                    List<MMAnimationClip> tempList = mmPreProcessor.mmAnimationClips.ToList();
                    tempList.Add(container);
                    for (var i = tempList.Count - 1; i > -1; i--)
                    {
                        if (tempList[i] == null)
                            tempList.RemoveAt(i);
                    }
                    mmPreProcessor.mmAnimationClips = tempList.ToArray();
                }

                mmPreProcessor.clip = null;
                EditorUtility.SetDirty(mmPreProcessor);
            }
        }
    }
}