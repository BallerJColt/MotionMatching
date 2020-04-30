using System.Collections;
using System.Collections.Generic;
using UnityEngine;
//using UnityEditor.Animations;
//using UnityEditor;

[System.Serializable]
public class AnimatorControllerInspetor : MonoBehaviour
{
    //[SerializeField]public AnimatorControllerParameter parameter; cant see in inspector
    //public RuntimeAnimatorController runtimeController;
    //public AnimatorController controller;
    public List<AnimControllerParamToChange> parametersToChange;

    private AnimatorControllerParameter[] animControllerParams;

    public struct AnimControllerParamToChange
    {
        public string nameOfParam;
        public AnimatorControllerParameterType paramType;
        public float desiredFloatValue;
        public int desiredIntValue;
        public bool desiredBoolValue;
        public float transitionTime;
    }
    private struct ParamIndex
    {
        public string _name;
        public int index;
    }
    /*public void flipTheBools()
    {
        if (controller == null)
        {
            Debug.LogWarning("No AnimtorController on " + gameObject.name);
            return;
        }
        ParamIndex[] paramsLocation = getControllerParamsNames();
        for (int i = 0; i < parametersToChange.Count; i++)
        {
            for (int j = 0; j < paramsLocation.Length; j++)
            {
                if(parametersToChange[i] == paramsLocation[j]._name)
                {
                    if(controller.parameters[j].type == AnimatorControllerParameterType.Bool)
                    {
                        controller.parameters[j].defaultBool = !controller.parameters[j].defaultBool;
                    }
                    break;
                }
            }
        }
    }*/

    /*private ParamIndex[] getControllerParamsNames()
    {
        ParamIndex[] parameterNames = new ParamIndex[controller.parameters.Length];
        for (int i = 0; i < parameterNames.Length; i++)
        {
            parameterNames[i]._name = controller.parameters[i].name;
            parameterNames[i].index = i;
        }
        return parameterNames;
    }*/

}
