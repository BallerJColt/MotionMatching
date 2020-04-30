using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using KinematicCharacterController;
using System;
using KinematicCharacterController.Examples;
using MiniGame2.Events;

namespace KinematicTest.controller
{
    public enum PlayerStates
    {
        Running,
        Idling,
        Sliding,
        LedgeGrabbing,
        Tired,
        Falling,
        NoInput,
        CinematicIdle,
        RunningOffLedge
    }

    public enum WorldForward
    {
        Right,
        Forward,
        Left,
        Back,
    }

    public struct PlayerCharacterInputs
    {
        //public string inputString;
        public bool jumpDown;
        public bool slideDown;
        public bool ledgeGrabHold;
        public bool changeDirection;
        public bool crouchDown;
        public bool crouchUp;
        public bool stopDown;
        public bool worldMoveDown;
    }

    public class KinematicTestController : MonoBehaviour, ICharacterController
    {
        [Space(10)] public bool updateSettingsLive = true;

        [Space(10)] public KinematicCharacterMotor Motor;
        //public GameObject scarf;

        // Gravity
        private Vector3 baseGravity = new Vector3(0, -10f, 0);
        private float hangGravity;
        private float riseGravity = 3f;
        private float fallGravity;
        private float dropGravity;

        // Movement tweaks
        private float curveStep;
        private float rampUpTime = 2f;
        private float rampDownTime = 0.5f;
        private bool stopped;
        private bool rampingDown;
        private bool isRunningRight = true;
        private bool canChangedirection = true;
        public static int runningRight = 1;
        private AnimationCurve rampUpCurve; //speed curve
        private AnimationCurve rampDownCurve;

        // Running
        private Vector3 worldMoveDirection = Vector3.right;
        private float MaxStableMoveSpeed;
        private float StableMovementSharpness;
        private float OrientationSharpness = 10;
        private Vector3 lastVelocityBeforeJump;
        private float turningToIdleGrace = 0.05f;
        private float lastTurn = 0f;

        private bool _hitWallThisFrame;

        // Jumping
        private bool AllowDoubleJump;
        private bool AllowJumpingWhenSliding;
        private bool _doubleJumpConsumed;
        private Vector3 _moveInputVector;
        private Vector3 _lookInputVector;
        private string _inputString;
        private float _timeSinceLastAbleToJump = 0f;
        private bool jumpInitiated;
        private bool _jumpedThisFrame;
        private float hangTimeVelocityThreshold;
        private bool _jumpRequested;
        private bool _jumpConsumed;
        private float _timeSinceJumpRequested;
        private float JumpSpeed;
        private float desiredJumpHeight;
        private float JumpPreGroundingGraceTime = 0f;
        private float JumpPostGroundingGraceTime = 0f;
        private bool playedJumpParticleThisJump = false;

        // Air movement
        private float MaxAirMoveSpeed;
        private float AirAccelerationSpeed;
        private float Drag = 0.1f;
        private bool _JustLanded;

        // Sliding
        private float _timeSinceStartedSliding;
        private float _slideCurveStep;
        private float _slideEndSpeed;
        private bool _isStoppedSliding;
        private Collider[] _probedColliders = new Collider[8];
        private bool _shouldBeCrouching;
        private bool _wasSlidingLastFrame;
        private bool _slidingThisFrame;
        private bool _isCrouching;

        // Hanging
        private float timeBeforeFallFromLedge;
        private float graceTimeBeforeHangAgain = 1f;
        private float timeAtLastLedgeGrab = 0f;
        private bool jumpFromWallRequested = false;
        private float ledgeGrabJumpHeight = 2;
        private float ledgeGrabAirMoveSpeed = 5;
        [SerializeField] private bool forward;
        [HideInInspector] public static GameObject ledgeGrabbed = null;
        private bool canFallFromLedgeAfterDelay;
        private float timeAtLastGrab;
        private bool teleporting;
        private bool _ledgeGrabbedThisFrame;
        public Vec3Variable ledgeGrabAnimationOffset;

        //World changes
        private float _timeSinceTransitioning;
        private float transitionTime = 2f;

        // Settings
        public PlayerControllerSettings settings;

        //Particles
        public ParticleSystem jumpParticle;
        public ParticleSystem runParticle;

        // Debug stuff
        public PlayerStates CurrentCharacterState;
        public WorldForward CurrentWorldForward;
        public Vector3 Gravity = new Vector3(0, -10f, 0);
        private float ledgeGrabGravityMultiplier = 0f;

        //This will later be scriptable object
        [Header("Sound settings")] public AK.Wwise.Event jumpSound;

        public AK.Wwise.Event landSound;
        bool canChangeMidAir = true;

        //Collision event because KCC doesn't like unity's collisions
        public IntEvent SpikeDamageEvent;
        public IntEvent CopDamageEvent;
        private bool _justTookDamage;
        private float _timeSinceDamageTaken;
        private bool canTakeDamage = true;
        private float damageResetTimer;


        void Init()
        {
            canChangeMidAir = settings.canChangeDirectionsMidair;
            rampUpTime = settings.rampUpTime;
            rampDownTime = settings.rampDownTime;
            rampUpCurve = settings.rampUpCurve;
            rampDownCurve = settings.rampDownCurve;
            baseGravity = new Vector3(0f, -settings.baseGravity, 0f);
            riseGravity = settings.riseGravityMultiplier;
            hangGravity = settings.hangGravityMultiplier;
            fallGravity = settings.fallGravityMultiplier;
            dropGravity = settings.dropGravityMultiplier;
            hangTimeVelocityThreshold = settings.hangTimeVelocityThreshold;
            ledgeGrabJumpHeight = settings.ledgeGrabForwardJumpHeight;
            ledgeGrabAirMoveSpeed = settings.ledgeGrabForwardAirMoveSpeed;
            //hangTime = settings.hangTime;
            graceTimeBeforeHangAgain = settings.graceTimeBeforeHangAgain;
            StableMovementSharpness = settings.StableMovementSharpness;
            OrientationSharpness = settings.OrientationSharpness;
            MaxStableMoveSpeed = settings.maxMoveSpeed;
            StableMovementSharpness = settings.StableMovementSharpness;

            desiredJumpHeight = settings.jumpHeight * Motor.Capsule.height;

            JumpSpeed = Mathf.Sqrt(2 * riseGravity * desiredJumpHeight * settings.baseGravity);

            JumpPreGroundingGraceTime = settings.JumpPreGroundingGraceTime;
            JumpPostGroundingGraceTime = settings.JumpPostGroundingGraceTime;

            MaxAirMoveSpeed = settings.maxAirMoveSpeed;
            Drag = 0.1f;
            canFallFromLedgeAfterDelay = settings.canFallFromLedgeAfterDelay;
            timeBeforeFallFromLedge = settings.timeBeforeFallFromLedge;
            AllowDoubleJump = settings.AllowDoubleJump;
            AllowJumpingWhenSliding = settings.AllowJumpingWhenSliding;

            damageResetTimer = settings.invincibilityTime;
        }

        private void Start()
        {
            // Assign to motor
            Motor.CharacterController = this;
            Init();
        }


        public void TransitionToState(PlayerStates newState)
        {
            PlayerStates tmpInitialState = CurrentCharacterState;
            OnStateExit(tmpInitialState, newState);
            CurrentCharacterState = newState;
            OnStateEnter(newState, tmpInitialState);
        }

        /// <summary>
        /// Event when entering a state
        /// </summary>
        public void OnStateEnter(PlayerStates state, PlayerStates fromState)
        {
            switch (state)
            {
                case PlayerStates.RunningOffLedge:
                {
                    timeAtLastLedgeGrab = Time.time;
                    MaxAirMoveSpeed = settings.maxAirMoveSpeed;
                    MaxStableMoveSpeed = settings.maxMoveSpeed;
                    JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.jumpHeight * settings.baseGravity *
                                           Motor.Capsule.height);
                    break;
                }
                case PlayerStates.Running:
                {
                    runParticle.Play();
                    MaxAirMoveSpeed = settings.maxAirMoveSpeed;
                    MaxStableMoveSpeed = settings.maxMoveSpeed;
                    JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.jumpHeight * settings.baseGravity *
                                           Motor.Capsule.height);
                    break;
                }
                case PlayerStates.Idling:
                {
                    stopped = true;
                    rampingDown = false;

                    MaxAirMoveSpeed = settings.idleAirMoveSpeed;
                    MaxStableMoveSpeed = 0f;
                    curveStep = 0f;
                    JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.idleJumpHeight * settings.baseGravity *
                                           Motor.Capsule.height);
                    break;
                }
                case PlayerStates.CinematicIdle:
                {
                    stopped = true;
                    rampingDown = false;
                    MaxStableMoveSpeed = 0f;
                    curveStep = 0f;
                    break;
                }
                case PlayerStates.Sliding:
                {
                    runParticle.Stop();
                    _slidingThisFrame = true;
                    canChangedirection = false;
                    _isStoppedSliding = false;
                    MaxAirMoveSpeed = settings.slideMoveSpeed;
                    MaxStableMoveSpeed = settings.slideMoveSpeed;
                    curveStep = 0f;
                    JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.slideJumpHeight * settings.baseGravity *
                                           Motor.Capsule.height);
                    _shouldBeCrouching = true;

                    riseGravity = settings.slideRiseGravityMultiplier;
                    hangGravity = settings.slideHangGravityMultiplier;
                    dropGravity = settings.slideDropGravityMultiplier;
                    fallGravity = settings.slideFallGravityMultiplier;
                    hangTimeVelocityThreshold = settings.slideHangTimeVelocityThreshold;

                    if (!_isCrouching)
                    {
                        _isCrouching = true;
                        Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                    }

                    break;
                }
                case PlayerStates.LedgeGrabbing:
                {
                    _ledgeGrabbedThisFrame = true;
                    stopped = false;
                    timeAtLastGrab = Time.time;
                    MaxAirMoveSpeed = 0;
                    MaxStableMoveSpeed = 0;
                    Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                    break;
                }
                case PlayerStates.Tired:
                {
                    stopped = false;
                    rampingDown = false;
                    MaxStableMoveSpeed = 0f;
                    curveStep = 0f;
                    if (forward)
                    {
                        MaxAirMoveSpeed = settings.ledgeGrabForwardAirMoveSpeed;
                        JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.ledgeGrabForwardJumpHeight *
                                               settings.baseGravity *
                                               Motor.Capsule.height);
                    }
                    else
                    {
                        MaxAirMoveSpeed = settings.ledgeGrabBackwardsAirMoveSpeed;
                        JumpSpeed = Mathf.Sqrt(2 * riseGravity * settings.ledgeGrabBackwardsJumpHeight *
                                               settings.baseGravity *
                                               Motor.Capsule.height);
                    }


                    break;
                }
                case PlayerStates.Falling:
                {
                    stopped = false;
                    rampingDown = false;
                    MaxAirMoveSpeed = 1f;
                    MaxStableMoveSpeed = 1f;
                    curveStep = 1f;
                    break;
                }
            }
        }

        /// <summary>
        /// Event when exiting a state
        /// </summary>
        public void OnStateExit(PlayerStates state, PlayerStates toState)
        {
            switch (state)
            {
                case PlayerStates.Running:
                {
                    break;
                }
                case PlayerStates.Idling:
                {
                    break;
                }
                case PlayerStates.Sliding:
                {
                    riseGravity = settings.riseGravityMultiplier;
                    hangGravity = settings.hangGravityMultiplier;
                    dropGravity = settings.dropGravityMultiplier;
                    fallGravity = settings.fallGravityMultiplier;
                    hangTimeVelocityThreshold = settings.hangTimeVelocityThreshold;

                    Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                    _isCrouching = false;
                    _isStoppedSliding = true;
                    _timeSinceStartedSliding = 0f;
                    canChangedirection = true;
                    break;
                }
                case PlayerStates.LedgeGrabbing:
                {
                    timeAtLastLedgeGrab = Time.time;
                    Motor.ZoeAttachedRigidbody = null;
                    _doubleJumpConsumed = false;
                    Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                    break;
                }
                case PlayerStates.NoInput:
                {
                    //runningRight = 1;
                    curveStep = 0; //for now
                    _timeSinceTransitioning = 0f;
                    break;
                }
                case PlayerStates.CinematicIdle:
                {
                    if (stopped)
                    {
                        stopped = false;
                        rampingDown = false;
                        curveStep = 0;
                    }

                    break;
                }
            }
        }

        /// <summary>
        /// This is called every frame by MyPlayer in order to tell the character what its inputs are
        /// </summary>
        public void SetInputs(ref PlayerCharacterInputs inputs)
        {
            if (CurrentCharacterState == PlayerStates.NoInput)
                return;

            if (CurrentCharacterState == PlayerStates.CinematicIdle)
            {
                if (inputs.changeDirection || inputs.slideDown || inputs.jumpDown)
                {
                    TransitionToState(PlayerStates.Running);
                }
            }

            if (inputs.slideDown && CurrentCharacterState == PlayerStates.Running &&
                Motor.GroundingStatus.FoundAnyGround)
            {
                TransitionToState(PlayerStates.Sliding);
            }

            if (inputs.slideDown && CurrentCharacterState == PlayerStates.LedgeGrabbing)
            {
                TransitionToState(PlayerStates.Falling);
            }


            if (canChangedirection && inputs.changeDirection)
            {
                if (CurrentCharacterState == PlayerStates.Idling)
                    TransitionToState(PlayerStates.Running);
                if (stopped)
                {
                    stopped = false;
                    rampingDown = false;
                    curveStep = 0;
                    runningRight *= -1;
                    //scarf.transform.Rotate(Vector3.up, 180);
                }
                else
                {
                    curveStep = 1 - curveStep;
                    rampingDown = true;
                }

                lastTurn = Time.time;

                //isRunningRight = !isRunningRight;
            }

            if (CurrentCharacterState == PlayerStates.LedgeGrabbing && inputs.changeDirection)
            {
                forward = false;
                TransitionToState(PlayerStates.Tired);
                rampingDown = false;
                curveStep = 0;
                runningRight = runningRight * -1;
                jumpFromWallRequested = true;
                _timeSinceJumpRequested = 0f;
                _jumpRequested = true;
            }

            //runningRight = isRunningRight ? 1 : -1 ;
            // Clamp input
            //Vector3 moveInputVector = Vector3.ClampMagnitude(new Vector3(inputs.MoveAxisRight, 0f, inputs.MoveAxisForward), 1f);


            switch (CurrentWorldForward)
            {
                case WorldForward.Right:
                {
                    worldMoveDirection = Vector3.right;
                    break;
                }
                case WorldForward.Forward:
                {
                    worldMoveDirection = Vector3.forward;
                    break;
                }
                case WorldForward.Left:
                {
                    worldMoveDirection = Vector3.left;
                    break;
                }
                case WorldForward.Back:
                {
                    worldMoveDirection = Vector3.back;
                    break;
                }
            }

            Vector3 moveInputVector = worldMoveDirection * runningRight;
            // Move and look inputs
            _moveInputVector = moveInputVector;

            //Capsule rotation, delete this to unfuck old camerafollow

            _lookInputVector = _moveInputVector.normalized;
            if (inputs.jumpDown)
            {
                _timeSinceJumpRequested = 0f;
                _jumpRequested = true;
                if (CurrentCharacterState == PlayerStates.LedgeGrabbing)
                {
                    forward = true;
                    TransitionToState(PlayerStates.Tired);
                    jumpFromWallRequested = true;
                }

                
            }

            if (inputs.stopDown)
            {
                TransitionToState(PlayerStates.Idling);
            }

            //Debug.LogWarning("Post input time is " + Time.time);
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called before the character begins its movement update
        /// </summary>
        public void BeforeCharacterUpdate(float deltaTime)
        {
            _hitWallThisFrame = false;
            Collider[] colliders = new Collider[8];
            var alma = Motor.CharacterCollisionsOverlap(transform.position, transform.rotation, colliders);
            if (alma > 0 &&
                GameManager.GetGameState() == GameStateScriptableObject.GameState.mainGameplayLoop)
            {
                foreach (var results in colliders)
                {
                    CheckVariousDMGThings(results);
                }
            }

            switch (CurrentCharacterState)
            {
                case PlayerStates.NoInput:
                {
                    _timeSinceTransitioning += deltaTime;
                    break;
                }
                case PlayerStates.Sliding:
                {
                    if (_wasSlidingLastFrame)
                    {
                        _slidingThisFrame = false;
                        _wasSlidingLastFrame = false;
                    }

                    if (_slidingThisFrame && !_wasSlidingLastFrame)
                    {
                        _wasSlidingLastFrame = true;
                    }


                    _timeSinceStartedSliding += deltaTime;
                    if (settings.decelerateWhileSliding)
                    {
                        _slideCurveStep = _timeSinceStartedSliding / settings.slideDuration;
                    }

                    break;
                }
                case PlayerStates.LedgeGrabbing:
                {
                    _ledgeGrabbedThisFrame = false;
                    break;
                }
                case PlayerStates.Idling:
                {
                    _hitWallThisFrame = false;
                    break;
                }
            }

            if (_justTookDamage)
            {
                canTakeDamage = false;
            }

            if (!canTakeDamage)
            {
                _justTookDamage = false;
                _timeSinceDamageTaken += deltaTime;
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its rotation should be right now. 
        /// This is the ONLY place where you should set the character's rotation
        /// </summary>
        public void UpdateRotation(ref Quaternion currentRotation, float deltaTime)
        {
            if (updateSettingsLive)
            {
                Init();
            }

            if (_lookInputVector != Vector3.zero && OrientationSharpness > 0f)
            {
                // Smoothly interpolate from current to target look direction
                Vector3 smoothedLookInputDirection = Vector3.Slerp(Motor.CharacterForward, _lookInputVector,
                    1 - Mathf.Exp(-OrientationSharpness * deltaTime)).normalized;

                // Set the current rotation (which will be used by the KinematicCharacterMotor)
                currentRotation = Quaternion.LookRotation(smoothedLookInputDirection, Motor.CharacterUp);
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is where you tell your character what its velocity should be right now. 
        /// This is the ONLY place where you can set the character's velocity
        /// </summary>
        public void UpdateVelocity(ref Vector3 currentVelocity, float deltaTime)
        {
            Vector3 targetMovementVelocity = Vector3.zero;
            if (Motor.GroundingStatus.IsStableOnGround)
            {
                if (!runParticle.isPlaying)
                {
                    runParticle.Play();
                }

                Gravity = riseGravity * baseGravity;
                // Reorient source velocity on current ground slope (this is because we don't want our smoothing to cause any velocity losses in slope changes)
                currentVelocity =
                    Motor.GetDirectionTangentToSurface(currentVelocity, Motor.GroundingStatus.GroundNormal) *
                    currentVelocity.magnitude;

                // Calculate target velocity
                Vector3 inputRight = Vector3.Cross(_moveInputVector, Motor.CharacterUp);
                Vector3 reorientedInput = Vector3.Cross(Motor.GroundingStatus.GroundNormal, inputRight).normalized *
                                          _moveInputVector.magnitude;
                float velocity = 0f;

                switch (CurrentCharacterState)
                {
                    case PlayerStates.RunningOffLedge:
                    case PlayerStates.Running:
                    {
                        if (rampingDown)
                        {
                            if (curveStep < 1)
                            {
                                curveStep += (1 / rampDownTime * Time.deltaTime);
                            }

                            if (curveStep >= 1)
                            {
                                curveStep = 0;
                                rampingDown = false;
                                lastTurn = Time.time;
                                runningRight = runningRight * -1;
                                //scarf.transform.Rotate(Vector3.up, 180);
                            }
                        }
                        else
                        {
                            if (curveStep < 1)
                            {
                                curveStep += (1 / rampUpTime * Time.deltaTime);
                            }

                            if (curveStep > 1)
                            {
                                curveStep = 1;
                            }
                        }

                        if (!stopped)
                        {
                            if (rampingDown)
                            {
                                velocity = MaxStableMoveSpeed * rampDownCurve.Evaluate(curveStep);
                            }
                            else
                            {
                                velocity = MaxStableMoveSpeed * rampUpCurve.Evaluate(curveStep);
                            }
                        }

                        break;
                    }
                    case PlayerStates.Sliding:
                    {
                        if (settings.decelerateWhileSliding)
                        {
                            velocity = settings.slideMoveSpeed * settings.slideCurve.Evaluate(_slideCurveStep);
                        }
                        else
                        {
                            velocity = Mathf.Min(settings.slideMoveSpeed, currentVelocity.magnitude);
                        }

                        break;
                    }
                }


                targetMovementVelocity = reorientedInput * velocity;

                // Smooth movement Velocity
                currentVelocity = Vector3.Lerp(currentVelocity, targetMovementVelocity,
                    1 - Mathf.Exp(-StableMovementSharpness * deltaTime));
            }
            else
            {
                // Add move input
                if (_moveInputVector.sqrMagnitude > 0f)
                {
                    targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;

                    // Prevent climbing on un-stable slopes with air movement
                    if (Motor.GroundingStatus.FoundAnyGround)
                    {
                        Vector3 perpenticularObstructionNormal = Vector3
                            .Cross(Vector3.Cross(Motor.CharacterUp, Motor.GroundingStatus.GroundNormal),
                                Motor.CharacterUp).normalized;
                        targetMovementVelocity =
                            Vector3.ProjectOnPlane(targetMovementVelocity, perpenticularObstructionNormal);
                    }

                    switch (CurrentCharacterState)
                    {
                        case PlayerStates.RunningOffLedge:
                        case PlayerStates.Running:
                        {
                            if (canChangeMidAir)
                            {
                                if (rampingDown)
                                {
                                    if (curveStep < 1)
                                    {
                                        curveStep += (1 / rampDownTime * Time.deltaTime);
                                    }

                                    if (curveStep >= 1)
                                    {
                                        curveStep = 0;
                                        rampingDown = false;
                                        runningRight = runningRight * -1;
                                        //scarf.transform.Rotate(Vector3.up, 180);
                                    }
                                }
                                else
                                {
                                    if (curveStep < 1)
                                    {
                                        curveStep += (1 / rampUpTime * Time.deltaTime);
                                    }

                                    if (curveStep > 1)
                                    {
                                        curveStep = 1;
                                    }
                                }

                                if (!stopped)
                                {
                                    if (rampingDown)
                                    {
                                        AirAccelerationSpeed = MaxAirMoveSpeed * rampDownCurve.Evaluate(curveStep);
                                    }
                                    else
                                    {
                                        AirAccelerationSpeed = MaxAirMoveSpeed * rampUpCurve.Evaluate(curveStep);
                                    }
                                }
                            }
                            else
                            {
                                targetMovementVelocity = lastVelocityBeforeJump;
                            }

                            break;
                        }
                        case PlayerStates.Idling:
                        {
                            targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;
                            AirAccelerationSpeed = MaxAirMoveSpeed;
                            break;
                        }
                        case PlayerStates.Falling:
                        {
                            AirAccelerationSpeed = MaxAirMoveSpeed;
                            break;
                        }
                        case PlayerStates.LedgeGrabbing:
                        {
                            AirAccelerationSpeed = 0;
                            break;
                        }
                        case PlayerStates.Tired:
                        {
                            targetMovementVelocity = _moveInputVector * MaxAirMoveSpeed;
                            AirAccelerationSpeed = MaxAirMoveSpeed;
                            if (canChangeMidAir)
                            {
                                if (rampingDown)
                                {
                                    if (curveStep < 1)
                                    {
                                        curveStep += (1 / rampDownTime * Time.deltaTime);
                                    }

                                    if (curveStep >= 1)
                                    {
                                        curveStep = 0;
                                        rampingDown = false;
                                        runningRight = runningRight * -1;
                                        //scarf.transform.Rotate(Vector3.up, 180);
                                    }
                                }
                                else
                                {
                                    if (curveStep < 1)
                                    {
                                        curveStep += (1 / rampUpTime * Time.deltaTime);
                                    }

                                    if (curveStep > 1)
                                    {
                                        curveStep = 1;
                                    }
                                }

                                if (!stopped)
                                {
                                    if (rampingDown)
                                    {
                                        AirAccelerationSpeed = MaxAirMoveSpeed * rampDownCurve.Evaluate(curveStep);
                                    }
                                    else
                                    {
                                        AirAccelerationSpeed = MaxAirMoveSpeed * rampUpCurve.Evaluate(curveStep);
                                    }
                                }
                            }

                            break;
                        }

                        case PlayerStates.Sliding:
                        {
                            targetMovementVelocity = lastVelocityBeforeJump;
                            break;
                        }
                    }


                    Vector3 velocityDiff = Vector3.ProjectOnPlane(targetMovementVelocity - currentVelocity, Gravity);
                    currentVelocity += velocityDiff * AirAccelerationSpeed * deltaTime;
                }

                //Calculate variable gravity
                Gravity = dropGravity * baseGravity;
                if (jumpInitiated)
                {
                    Gravity = riseGravity * baseGravity; // LUL
                    if (currentVelocity.y < 0f)
                        Gravity = hangGravity * baseGravity;
                    if (currentVelocity.y < -hangTimeVelocityThreshold)
                        Gravity = fallGravity * baseGravity;
                }


                else if (CurrentCharacterState == PlayerStates.Running)
                {
                    TransitionToState(PlayerStates.RunningOffLedge);
                }

                if (CurrentCharacterState == PlayerStates.LedgeGrabbing)
                {
                    Gravity = baseGravity * ledgeGrabGravityMultiplier;
                    currentVelocity = Vector3.zero;
                }

                // Gravity
                currentVelocity += Gravity * deltaTime;

                // Drag
                currentVelocity *= (1f / (1f + (Drag * deltaTime)));
            }

            {
                _jumpedThisFrame = false;
                _timeSinceJumpRequested += deltaTime;
                if (_jumpRequested)
                {
                    // Handle double jump
                    if (AllowDoubleJump)
                    {
                        if (_jumpConsumed && !_doubleJumpConsumed && (AllowJumpingWhenSliding
                                ? !Motor.GroundingStatus.FoundAnyGround
                                : !Motor.GroundingStatus.IsStableOnGround))
                        {
                            if (CurrentCharacterState == PlayerStates.Idling)
                            {
                                TransitionToState(PlayerStates.Running);
                            }

                            Motor.ForceUnground(0.1f);

                            // Add to the return velocity and reset jump state
                            currentVelocity += (Motor.CharacterUp * JumpSpeed) -
                                               Vector3.Project(currentVelocity, Motor.CharacterUp);
                            _jumpRequested = false;
                            _doubleJumpConsumed = true;
                            _jumpedThisFrame = true;
                        }
                    }

                    if (jumpFromWallRequested)
                    {
                        jumpFromWallRequested = false;
                        Motor.ForceUnground(0.1f);
                        currentVelocity += (Motor.CharacterUp * JumpSpeed) -
                                           Vector3.Project(currentVelocity, Motor.CharacterUp);
                        _jumpRequested = false;
                        _jumpConsumed = true;
                        _jumpedThisFrame = true;
                    }

                    // See if we actually are allowed to jump
                    if (
                        (!_jumpConsumed &&
                         ((AllowJumpingWhenSliding
                              ? Motor.GroundingStatus.FoundAnyGround
                              : Motor.GroundingStatus.IsStableOnGround) ||
                          _timeSinceLastAbleToJump <= JumpPostGroundingGraceTime)))
                    {
                        jumpInitiated = true;
                        if (CurrentCharacterState == PlayerStates.Running || CurrentCharacterState == PlayerStates.Sliding || CurrentCharacterState == PlayerStates.RunningOffLedge)
                        {
                            Debug.Log("Playing Jump Particle");
                            if (!playedJumpParticleThisJump)
                            {
                                jumpParticle.Play();
                                playedJumpParticleThisJump = true;
                            }

                            runParticle.Stop();
                            //runParticle.Clear();
                            settings.jumpShakeEvent.Raise();
                        }
                        jumpSound.Post(gameObject);
                        // Calculate jump direction before ungrounding
                        Vector3 jumpDirection = Motor.CharacterUp;
                        if (Motor.GroundingStatus.FoundAnyGround && !Motor.GroundingStatus.IsStableOnGround)
                        {
                            jumpDirection = Motor.GroundingStatus.GroundNormal;
                        }

                        // Makes the character skip ground probing/snapping on its next update. 
                        // If this line weren't here, the character would remain snapped to the ground when trying to jump. Try commenting this line out and see.
                        Motor.ForceUnground(0.1f);

                        // Add to the return velocity and reset jump state
                        currentVelocity += (jumpDirection * JumpSpeed) -
                                           Vector3.Project(currentVelocity, Motor.CharacterUp);
                        _jumpRequested = false;
                        _jumpConsumed = true;
                        _jumpedThisFrame = true;
                    }
                }
            }
        }

        /// <summary>
        /// (Called by KinematicCharacterMotor during its update cycle)
        /// This is called after the character has finished its movement update
        /// </summary>
        public void AfterCharacterUpdate(float deltaTime)
        {
            // Handle jump-related values
            {
                // Handle jumping pre-ground grace period
                if (_jumpRequested && _timeSinceJumpRequested > JumpPreGroundingGraceTime)
                {
                    _jumpRequested = false;
                }

                if (AllowJumpingWhenSliding
                    ? Motor.GroundingStatus.FoundAnyGround
                    : Motor.GroundingStatus.IsStableOnGround)
                {
                    // If we're on a ground surface, reset jumping values
                    if (!_jumpedThisFrame)
                    {
                        _doubleJumpConsumed = false;
                        _jumpConsumed = false;
                    }

                    _timeSinceLastAbleToJump = 0f;
                }
                else
                {
                    // Keep track of time since we were last able to jump (for grace period)
                    _timeSinceLastAbleToJump += deltaTime;
                }

                switch (CurrentCharacterState)
                {
                    case PlayerStates.Idling:
                    {
                        break;
                    }
                    case PlayerStates.NoInput:
                    {
                        if (_timeSinceTransitioning > transitionTime)
                        {
                            TransitionToState(PlayerStates.Running);
                        }

                        break;
                    }
                    case PlayerStates.Sliding:
                    {
                        if (!_isStoppedSliding && _timeSinceStartedSliding > settings.slideDuration)
                        {
                            _shouldBeCrouching = false;
                        }

                        if (_isCrouching && !_shouldBeCrouching && Motor.GroundingStatus.IsStableOnGround)
                        {
                            // Do an overlap test with the character's standing height to see if there are any obstructions
                            Motor.SetCapsuleDimensions(0.5f, 2f, 1f);
                            if (Motor.CharacterOverlap(
                                    Motor.TransientPosition,
                                    Motor.TransientRotation,
                                    _probedColliders,
                                    Motor.CollidableLayers,
                                    QueryTriggerInteraction.Ignore) > 0)
                            {
                                // If obstructions, just stick to crouching dimensions
                                Motor.SetCapsuleDimensions(0.5f, 1f, 0.5f);
                            }
                            else
                            {
                                // If no obstructions, uncrouch
                                TransitionToState(PlayerStates.Running);
                                _isStoppedSliding = true;
                            }
                        }


                        break;
                    }
                    case PlayerStates.LedgeGrabbing:
                    {
                        if (canFallFromLedgeAfterDelay && timeAtLastGrab + timeBeforeFallFromLedge <= Time.time)
                        {
                            timeAtLastGrab = Time.time;
                            TransitionToState(PlayerStates.Tired);
                        }

                        break;
                    }
                }

                if (!canTakeDamage && _timeSinceDamageTaken > damageResetTimer)
                {
                    canTakeDamage = true;
                    _timeSinceDamageTaken = 0f;
                }
            }
            if (teleporting)
            {
                teleporting = false;
                if (runningRight != (int) ledgeGrabbed.gameObject.GetComponent<LedgeGrabPoint>().zoeShouldBeFacing)
                {
                    runningRight *= -1;
                    curveStep = 0;
                    forward = false;
                }

                //Debug.Log("POS : " + (ledgeGrabbed.gameObject.GetComponent<LedgeGrabPoint>().offset +
                //                      ledgeGrabbed.gameObject.GetComponent<LedgeGrabPoint>().transform.position)
                //          .ToString());
                Motor.SetPosition(ledgeGrabbed.gameObject.GetComponent<LedgeGrabPoint>().offset +
                                  ledgeGrabbed.gameObject.GetComponent<LedgeGrabPoint>().transform.position /*+
                                  ledgeGrabAnimationOffset.GetVector3()*/);
            }

            _JustLanded = false;
        }

        public bool IsColliderValidForCollisions(Collider coll)
        {
            return true;
        }


        private void CheckVariousDMGThings(Collider hitCollider)
        {
            if (hitCollider == null || !canTakeDamage || hitCollider.CompareTag("Wall") ||
                hitCollider.CompareTag("Ledge") || hitCollider.CompareTag("Player"))
                return;

            // Debug.Log("overlap tag: " + hitCollider.tag);
            if (hitCollider.CompareTag("Spike"))
            {
                int damage = hitCollider.GetComponent<DamageOnImpact>().damage.myInt;
                SpikeDamageEvent.Raise(damage);
                _justTookDamage = true;
            }
            else if (hitCollider.CompareTag("Cop"))
            {
                int damage = hitCollider.GetComponent<DamageOnImpact>().damage.myInt;
                CopDamageEvent.Raise(damage);
                _justTookDamage = true;
            }
        }

        public void OnGroundHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            CheckVariousDMGThings(hitCollider);

            if (CurrentCharacterState == PlayerStates.Idling)
            {
                bool foundWall = false;
                Collider[] hitColliders = Physics.OverlapBox(gameObject.transform.position + Vector3.up * 0.75f,
                    new Vector3(1.2f, 0.5f, 0.5f));

                foreach (Collider item in hitColliders)
                {
                    if (item.CompareTag("Wall"))
                    {
                        foundWall = true;
                    }
                }

                if (foundWall == false)
                {
                    TransitionToState(PlayerStates.Running);
                    stopped = false;
                }
            }
        }

        public void OnMovementHit(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            ref HitStabilityReport hitStabilityReport)
        {
            Debug.Log("HIT " + hitCollider.tag);
            if (hitCollider.CompareTag("Ledge") && Time.time > (timeAtLastLedgeGrab + graceTimeBeforeHangAgain)
            ) // && hitNormal.y == 0 && Mathf.Sign(hitNormal.x) == -Mathf.Sign(runningRight))
            {
                if (CurrentCharacterState == PlayerStates.Sliding || CurrentCharacterState == PlayerStates.Tired || CurrentCharacterState == PlayerStates.RunningOffLedge)
                {
                    ledgeGrabbed = hitCollider.gameObject;
                    Motor.ZoeAttachedRigidbody = hitCollider.gameObject.GetComponentInParent<Rigidbody>();
                    timeAtLastLedgeGrab = Time.time;
                    TransitionToState(PlayerStates.LedgeGrabbing);
                    teleporting = true;
                }
                else if (hitNormal.y <= 0 || Mathf.Approximately(hitNormal.y, 1))
                {
                    ledgeGrabbed = hitCollider.gameObject;
                    Motor.ZoeAttachedRigidbody = hitCollider.gameObject.GetComponentInParent<Rigidbody>();
                    timeAtLastLedgeGrab = Time.time;
                    TransitionToState(PlayerStates.LedgeGrabbing);
                    teleporting = true;
                }
                else if (_jumpConsumed)
                {
                    ledgeGrabbed = hitCollider.gameObject;
                    Motor.ZoeAttachedRigidbody = hitCollider.gameObject.GetComponentInParent<Rigidbody>();
                    timeAtLastLedgeGrab = Time.time;
                    TransitionToState(PlayerStates.LedgeGrabbing);
                    teleporting = true;
                }
            }

            if (hitCollider.CompareTag("Wall") && CurrentCharacterState != PlayerStates.Idling &&
                Motor.GroundingStatus.IsStableOnGround && !rampingDown)
            {
                //if (rampingDown)
                //{
                //    curveStep = 0;
                //    rampingDown = false;
                //    //runningRight = runningRight * -1;
                //    //scarf.transform.Rotate(Vector3.up, 180);
                //}
                if (!rampingDown)
                {
                    if (Time.time > lastTurn + turningToIdleGrace)
                    {
                        TransitionToState(PlayerStates.Idling);
                        Debug.Log("Going To Idle");
                        _hitWallThisFrame = true;
                    }
                }
            }

            if (hitCollider.CompareTag("MovingPlatform"))
            {
                MovingPlatform movingPlatform = hitCollider.gameObject.GetComponent<MovingPlatform>();
                if (movingPlatform.activationType == MovingPlatform.ActivationType.player)
                {
                    movingPlatform.activatePlatform();
                }
            }

            if (hitCollider.CompareTag("FallingPlatform"))
            {
                FallingPlatforms fallingPlatform = hitCollider.gameObject.GetComponent<FallingPlatforms>();
                fallingPlatform.startFallingPlatform();
            }

            CheckVariousDMGThings(hitCollider);
        }

        public void PostGroundingUpdate(float deltaTime)
        {
            // Handle landing and leaving ground
            if (Motor.GroundingStatus.IsStableOnGround && !Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLanded();
            }
            else if (!Motor.GroundingStatus.IsStableOnGround && Motor.LastGroundingStatus.IsStableOnGround)
            {
                OnLeaveStableGround();
            }
        }

        protected void OnLanded()
        {
            playedJumpParticleThisJump = false;
            _JustLanded = true;
            landSound.Post(gameObject);
            canChangedirection = true;
            jumpInitiated = false;
            Debug.Log("Landed");
            settings.landingShakeEvent.Raise();
            if (CurrentCharacterState == PlayerStates.Idling || CurrentCharacterState == PlayerStates.Falling ||
                CurrentCharacterState == PlayerStates.Tired  || CurrentCharacterState == PlayerStates.RunningOffLedge)
            {
                stopped = false;
                TransitionToState(PlayerStates.Running);
            }

            if (CurrentCharacterState == PlayerStates.Sliding)
            {
                //??
                _slideEndSpeed =
                    (Motor.GetDirectionTangentToSurface(Motor.Velocity, Motor.GroundingStatus.GroundNormal) *
                     Motor.Velocity.magnitude).magnitude;
                Debug.Log(_slideEndSpeed);
            }
        }

        protected void OnLeaveStableGround()
        {
            if (canChangeMidAir && CurrentCharacterState != PlayerStates.Sliding)
            {
                canChangedirection = true;
            }
            else
            {
                lastVelocityBeforeJump = Motor.Velocity;
                canChangedirection = false;
            }

            if (runParticle.isPlaying)
            {
                runParticle.Stop();
            }

            Debug.Log("Left ground");
        }

        public void AddVelocity(Vector3 velocity)
        {
        }

        public void ProcessHitStabilityReport(Collider hitCollider, Vector3 hitNormal, Vector3 hitPoint,
            Vector3 atCharacterPosition, Quaternion atCharacterRotation, ref HitStabilityReport hitStabilityReport)
        {
        }

        public void OnDiscreteCollisionDetected(Collider hitCollider)
        {
        }

        public float GetJumpSpeedFromHeight(float grav, float jumpHeight)
        {
            return Mathf.Sqrt(2f * grav * jumpHeight);
        }

        public static bool GetIsRunningRight()
        {
            if (runningRight == 1)
            {
                return true;
            }
            else
            {
                return false;
            }
        }

        public bool GetIsJumpingOnPurpose()
        {
            return jumpInitiated;
        }

        public void MidLevelTransition(int dir)
        {
            Debug.Log(dir);
            TransitionToState(PlayerStates.NoInput);
            CurrentWorldForward = (WorldForward) (((int) CurrentWorldForward + dir) % 4);
        }

        public bool JumpingThisFrame()
        {
            return _jumpedThisFrame;
        }

        public bool GetCanTakeDamage()
        {
            return canTakeDamage;
        }

        public float GetIFrameMaxDuration()
        {
            return settings.invincibilityTime;
        }

        public bool GetJustLanded()
        {
            return _JustLanded;
        }

        public bool GetLedgeForward()
        {
            return forward;
        }

        public float GetJumpPower()
        {
            return JumpSpeed;
        }

        public void OnDeathStopMove()
        {
            TransitionToState(PlayerStates.NoInput);
        }

        public float GetSlideNormalizedTime()
        {
            return _slideCurveStep;
        }

        public bool GetSlidingThisFrame()
        {
            return _slidingThisFrame;
        }

        public bool GetHitWallThisFrame()
        {
            return _hitWallThisFrame;
        }

        public bool GetLedgingThisFrame()
        {
            return _ledgeGrabbedThisFrame;
        }

        public void MakeInvulnerableForever()
        {
            damageResetTimer = 600f;
            canTakeDamage = false;
        }
    }
}