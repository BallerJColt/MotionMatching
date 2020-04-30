using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Text;
using UnityEngine;

[CreateAssetMenu(fileName = "NewMMConfig", menuName = "Motion Matching/Config", order = 10)]
public class MMConfig : ScriptableObject
{
    public int frameRate;
    public List<float> trajectoryTimePoints;
    public List<HumanBodyBones> trackedBones;
    public string[] ignoreTags = new string[32];
    public string[] favourTags = new string[32];

#if UNITY_EDITOR
    public void CreateEnum(string enumName, string[] enumArray)
    {
        string filePathAndName = "Assets/Scripts/Enums/" + enumName + ".cs";

        StringBuilder sb = new StringBuilder();
        sb.AppendFormat("public enum {0} \n {{ \t",enumName);
        for (int i = 0; i < enumArray.Length; i++)
        {
            if (enumArray[i] == "")
                continue;
            sb.AppendFormat("\n \t {0} = (1 << {1}),",enumArray[i],i);
        }

        sb.AppendFormat("\n }}");
        
        File.WriteAllText(filePathAndName,sb.ToString());
        UnityEditor.AssetDatabase.ImportAsset(filePathAndName);
    }
#endif
}