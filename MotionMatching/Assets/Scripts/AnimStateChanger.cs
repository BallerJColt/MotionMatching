using System.Collections;
using System.Collections.Generic;
using MiniGame2.Events;
using UnityEngine;

public class AnimStateChanger : StateMachineBehaviour
{
    public IntEvent lerpEvent;
    
    public override void OnStateEnter(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (stateInfo.IsName("Motion Matching"))
        {
            lerpEvent.Raise(0);
        }
    }

    public override void OnStateExit(Animator animator, AnimatorStateInfo stateInfo, int layerIndex)
    {
        if (stateInfo.IsName("Motion Matching"))
        {
            lerpEvent.Raise(1);
        }
    }
}
