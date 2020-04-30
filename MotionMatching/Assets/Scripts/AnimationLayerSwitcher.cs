using System;
using System.Collections;
using System.Collections.Generic;
using KinematicTest.controller;
using UnityEngine;
using Random = UnityEngine.Random;

public class AnimationLayerSwitcher : MonoBehaviour, IOnSceneReset
{
    public KinematicTestController characterController;

    public Animator animator;
    private float slideTime;
    private float fallTime;

    [Tooltip("Ground prediction time in seconds")]
    public float predictionTime;

    public Transform zoeRoot;

    private bool _isChangingWeight;
    private bool _isRightFootInFront;

    [Tooltip("Time it takes to fade into/out of MM in frames")]
    public int fadeTimeInFrames;

    [Header("Jump type percentages")] public int normalJumpRatio;
    public int backflipRatio;
    public int cheatGainerRatio;

    private void Awake()
    {
        if (fadeTimeInFrames <= 0) fadeTimeInFrames = 1;
        if (predictionTime <= 0f) predictionTime = 0.1f;
    }

    private void Update()
    {
        //General in air stuff
        if (!characterController.Motor.GroundingStatus.FoundAnyGround && characterController.Motor.BaseVelocity.y > 0)
        {
            fallTime = 0f;
            animator.SetFloat("fallBlend", fallTime);
            
        }

            if (!characterController.Motor.GroundingStatus.FoundAnyGround && characterController.Motor.BaseVelocity.y < 0)
        {
            fallTime += Time.deltaTime;
            animator.SetFloat("fallBlend", fallTime);
            //Brace for impact
        }

        if (characterController.CurrentCharacterState != PlayerStates.Sliding)
        {
            animator.SetBool("isSliding", false);
        }

        //General running stuff
        if (characterController.Motor.GroundingStatus.FoundAnyGround)
        {
            _isRightFootInFront = IsRightFootInFront();
            animator.SetBool("rightFootInFront", _isRightFootInFront);

            if (characterController.GetSlidingThisFrame())
            {
                Debug.Log("sliding");
                animator.SetTrigger("slideInitiated");
            }
            else
            {
                animator.ResetTrigger("slideInitiated");
            }

            if (characterController.GetHitWallThisFrame())
            {
                Debug.Log("hitwall");
                animator.SetTrigger("hitWall");
            }
            else
            {
                animator.ResetTrigger("hitWall");
            }
        }

        //ledge can be grabbed from slide or air so we have it here
        if (characterController.GetLedgingThisFrame())
        {
            Debug.Log("ledge");
            animator.SetTrigger("ledgingThisFrame");
        }
        else
        {
            animator.ResetTrigger("ledgingThisFrame");
        }

        //On Landing
        if (GameManager.GetGameState() != GameStateScriptableObject.GameState.levelStart)
        {
            if ((characterController.Motor.GroundingStatus.IsStableOnGround || characterController.Motor.GroundingStatus.FoundAnyGround )&&
             !characterController.Motor.LastGroundingStatus.IsStableOnGround)
            {
                fallTime = 1.0f;
                animator.SetFloat("fallBlend", fallTime);
                animator.SetBool("inAir", false);
                animator.SetBool("isFalling", false);
                animator.SetTrigger("FallingGroundDetected");
                animator.ResetTrigger("ledgingThisFrame");
            }       
            else
            {
                animator.ResetTrigger("FallingGroundDetected");
            }
        }
        if (characterController.CurrentCharacterState != PlayerStates.Idling)
        {
            animator.SetBool("isStanding", false);
            animator.SetFloat("slideBlend", 0f);
        }

        if (characterController.CurrentCharacterState != PlayerStates.CinematicIdle)

        {
            animator.SetBool("isCinematicStanding", false);
        }

        if (characterController.CurrentCharacterState != PlayerStates.NoInput)
        {
            animator.SetBool("isNoInput", false);
        }

        //handle interaction states
        switch (characterController.CurrentCharacterState)
        {
            case PlayerStates.Idling:
            {
                if (characterController.JumpingThisFrame())
                {
                    Debug.Log("idleJump");
                    animator.SetTrigger("idleJump");
                    animator.SetBool("inAir", true);

                }
                else
                {
                    animator.ResetTrigger("idleJump");
                }

                //set idle loop
                animator.SetBool("isStanding", true);
                    //animator.SetTrigger("cinematicMoment");
                break;
            }
            case PlayerStates.NoInput:
            {
                //set idle loop
                animator.SetBool("isNoInput", true);
                break;
            }
            case PlayerStates.CinematicIdle:
            {
                //set idle loop
                animator.SetBool("isCinematicStanding", true);
                break;
            }
            case PlayerStates.Sliding:
            {
                //set roll animation
                slideTime = Mathf.Clamp(characterController.GetSlideNormalizedTime(),0f,1f);
                animator.SetFloat("slideBlend", slideTime);
                animator.SetBool("isSliding", true);
                if (characterController.JumpingThisFrame())
                {
                    // set jump anim
                    Debug.Log("slideJump");
                    animator.SetTrigger("slideJump");
                        animator.SetBool("inAir", true);
                    }
                else
                {
                    animator.ResetTrigger("slideJump");
                }

                break;
            }
            case PlayerStates.RunningOffLedge:
            case PlayerStates.Running:
            {
                if (characterController.JumpingThisFrame())
                {
                    int jumpType = SelectJumpType(); // 0 = normal, 1 = backflip, 2 = C H E A T G A I N E R
                    animator.SetInteger("jumpType", jumpType);
                    Debug.Log("jump");
                    animator.SetTrigger("jump");
                    animator.SetBool("inAir", true);
                }
                else
                {
                    animator.ResetTrigger("jump");
                }

                //animator.SetBool("isStanding", false);
                animator.SetBool("onLedge?", false);


                break;
            }
            case PlayerStates.LedgeGrabbing:
            {
                //set ledge grab
                animator.SetBool("onLedge?", true);
                animator.SetBool("inAir", false);
                fallTime = 1.0f;
                animator.SetFloat("fallBlend", fallTime);
                animator.ResetTrigger("hitWall");
                break;
            }
            case PlayerStates.Tired:
            {
                if (characterController.JumpingThisFrame())
                {
                    Debug.Log("ledgejump");
                    animator.SetTrigger("ledgeJump");
                }
                else
                {
                    animator.ResetTrigger("ledgeJump");
                }

                animator.SetBool("inAir", true);
                animator.SetBool("onLedge?", false);
                animator.ResetTrigger("hitWall");

                break;
            }
            case PlayerStates.Falling:
            {
                animator.SetBool("inAir", true);
                animator.SetBool("onLedge?", false);
                animator.ResetTrigger("hitWall");
                break;
            }
        }
    }


    //now only projects down and with a smaller radius than Zoe's capsule, meaning it should mostly only collide with floor. Some edge cases still exist
    private bool PredictAboutToLand(float deltaTime, out Vector3 predictedPosition)
    {
        Vector3 grav = characterController.Gravity;
        Vector3 velocity = characterController.Motor.BaseVelocity;
        //velocity = veloctiy + grav * deltaTime;
        velocity = grav * deltaTime;


        Vector3 position = transform.parent.position;
        predictedPosition = position + velocity * deltaTime;
        Vector3 dir = velocity * deltaTime;
        return Physics.SphereCast(position, characterController.Motor.Capsule.radius - 0.1f, dir.normalized,
            out var sphereCastHitInfo, dir.magnitude);
    }


    public void StartWeightChange(int desiredWeight)
    {
        if (_isChangingWeight)
        {
            StopCoroutine(nameof(SetLayerWeights));
        }

        StartCoroutine(SetLayerWeights(desiredWeight));
    }

    private IEnumerator SetLayerWeights(int desiredWeight)
    {
        _isChangingWeight = true;
        int interactionLayerIndex = animator.GetLayerIndex("Interactions");
        float startWeight = animator.GetLayerWeight(interactionLayerIndex);
        int step = 1;
        do
        {
            float weight = Mathf.Lerp(startWeight, desiredWeight, step / (float) fadeTimeInFrames);
            animator.SetLayerWeight(interactionLayerIndex, weight);
            yield return new WaitForSeconds(0f);
            step++;
        } while (step < fadeTimeInFrames + 1);

        _isChangingWeight = false;
    }

    private bool IsRightFootInFront()
    {
        Matrix4x4 rootMatrix = zoeRoot.worldToLocalMatrix;
        Vector3 leftFootLocal =
            rootMatrix.MultiplyPoint3x4(animator.GetBoneTransform(HumanBodyBones.LeftFoot).position);
        Vector3 rightFootLocal =
            rootMatrix.MultiplyPoint3x4(animator.GetBoneTransform(HumanBodyBones.RightFoot).position);
        return (rightFootLocal.z > leftFootLocal.z);
    }

    private int SelectJumpType()
    {
        int r = Random.Range(1, normalJumpRatio + cheatGainerRatio + backflipRatio + 1);
        if (r > normalJumpRatio + backflipRatio)
        {
            return 2;
        }

        if (r > normalJumpRatio)
            return 1;
        return 0;
    }

    public void OnResetLevel()
    {
        foreach (var item in animator.parameters)
        {
            switch (item.type)
            {
                case AnimatorControllerParameterType.Float:
                    animator.SetFloat(item.ToString(),0);
                    break;
                case AnimatorControllerParameterType.Int:
                    animator.SetInteger(item.ToString(), 0);
                    break;
                case AnimatorControllerParameterType.Bool:
                    animator.SetBool(item.ToString(), false);
                    break;
                case AnimatorControllerParameterType.Trigger:
                    animator.ResetTrigger(item.ToString());
                    break;
                default:
                    break;
            }
        }
    }
}