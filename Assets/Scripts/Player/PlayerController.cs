using System;
using System.Collections.Generic;
using Global;
using Global.GameObjectMarkers;
using UnityEditor;
using UnityEngine;
using UnityEngine.InputSystem;
using UnityEngine.Splines;
using Utils;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CharacterController _characterController;
        [SerializeField] private Transform _characterMesh;
        [SerializeField] private Transform _orientation;

        [Header("Movement")]
        [SerializeField] private float _moveSpeed;
        [SerializeField] private AnimationCurve _moveSpeedAnimCurve;
        [SerializeField] private float _accelerationRate;
        [SerializeField] private float _decelerationRate;
        [SerializeField] private float _overshootDecelerationRate;

        [Header("Jump Controls")]
        [SerializeField] private float _jumpLaunchSpeed;
        [SerializeField] private float _gravityMultiplier;

        [Header("Mesh Controls")]
        [SerializeField] private float _rotationSpeed;

        [Header("Secondary Movement")]
        [Header("Slide")]
        [SerializeField] private float _slideSpeed;
        [SerializeField] private float _slideJumpVelocity;
        [SerializeField] private float _slideFallLaunchVelocity;
        [SerializeField] private float _slideDuration;
        [Header("Wall Run")]
        [SerializeField] private Transform _wallRunLeftSide;
        [SerializeField] private Transform _wallRunRightSide;
        [SerializeField] private float _wallRunSpeed;
        [SerializeField] private float _wallRunDuration;
        [SerializeField] private float _wallRunDistanceCheck;
        [SerializeField]
        [Tooltip(
            "This is primarily used to push the player towards the wall. So the distance must be less than _wallRunDistanceCheck"
        )]
        private float _wallRunAttachedDistanceCheck;
        [SerializeField] private float _wallRunCorrectionSpeed;
        [SerializeField] private LayerMask _wallRunLayerMask;
        [SerializeField] private float _wallRunUpCurveSpeed;
        [SerializeField] private float _wallRunDownCurveSpeed;
        [SerializeField] private float _wallRunUpCurveDuration;
        [SerializeField] private Vector3 _wallRunLaunchSpeed;
        [Header("Rail Grind")]
        [SerializeField] private Transform _railGrindCastLocation;
        [SerializeField] private float _railGrindSpeed;
        [SerializeField] private float _railGrindCheckerRadius;
        [SerializeField] private LayerMask _railGrindLayerMask;
        [SerializeField] private float _railGrindFowardCheckDistance;
        [SerializeField] private int _railGrindSplineResolution;
        [SerializeField] private int _railGrindSplineIterations;
        [SerializeField] private float _railGrindJumpVelocity;
        [SerializeField] private float _railGrindAcceleration;
        [SerializeField] private float _railGrindDeceleration;
        [SerializeField] private float _railGrindBoostDuration;

        // Additional Components
        private Transform _cameraObject;

        // Inputs
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        // Input Data
        private Vector2 _moveInput;
        private Vector2 _lastNonZeroMoveInput;

        // Player State
        private Stack<PlayerState> _playerStateStack;
        private bool _isGrounded;
        private Vector3 _moveVelocity;
        private Vector3 _cameraPosition;

        // Basic Movement Data
        public PlayerState CurrentPlayerState => _playerStateStack.Peek();
        public float CurrentMoveSpeed { get; private set; }
        public float MaxMoveSpeed => _moveSpeed;

        // Moving State
        private Vector2 _previousFrameInput;

        // Slide Data
        private Vector2 _slideDirectionInput; // The player is not allowed to change directions when sliding...
        private float _slideCurrentTime;

        // Wall Run Data
        private RaycastHit[] _wallRunRaycastHit;
        private Vector2 _wallRunDirectionInput; // TODO: Implement this...
        public bool IsLeftWallRun { get; private set; }
        private float _wallRunCurrentTime;

        // Rail Grind Data
        private Collider[] _railGrindOverlapColliders;
        private SplineContainer _railGrindSplineContainer;
        private float _railGrindSplineLength;
        private float _railGrindCurrentDistance;
        private bool _railGrindPositive;
        private float _railGrindCurrentSpeed;
        private float _railGrindBoostRemainingTime;

        // Delegates
        public delegate void OnJumped();
        public delegate void OnGroundedStateChanged(bool currentState);
        public delegate void OnStateChanged(PlayerState currentState, PlayerState previousState);
        public delegate void OnStatePushed(PlayerState pushedState);
        public delegate void OnStatePopped(PlayerState poppedState);
        public delegate void OnRailGrindBoosted();

        public OnJumped onJumped;
        public OnGroundedStateChanged onGroundedStateChanged;
        public OnStateChanged onStateChanged;
        public OnStatePushed onStatePushed;
        public OnStatePopped onStatePopped;
        public OnRailGrindBoosted onRailGrindBoosted;

        private void Start()
        {
            /*
             * TODO:
             * 
             * We need to figure out an easier way initiate Rail Grind, Wall Run
             * For Rail Grind we need to make a much bigger sphere cast so that it can be easily trigger
             * Ideally I want it to be triggered when the player is near but not exactly on it
             *
             * Same thing for Wall Run
             * I need to find trigger it from when the player is NOT perpendicular to a wall
             * Meaning they can trigger a wall run from some distance away from the wall and it'll
             * pull the player towards to wall
             *
             * Maybe tone down the speeds? The character feels good but is kind of hard to control
             */
            
            CursorController.Instance.EnableCursor(false);
            _cameraObject = GameObject.FindGameObjectWithTag(GameTags.MainCamera).transform;

            _wallRunRaycastHit = new RaycastHit[1];
            _railGrindOverlapColliders = new Collider[1];

            CurrentMoveSpeed = 0;
            _moveVelocity = Vector3.zero;
            _cameraPosition = Vector3.zero;

            _playerStateStack = new Stack<PlayerState>();
            PushState(PlayerState.Idle);

            SetupInput();
        }

        private void Update()
        {
            UpdateInput();
            UpdatePlayerMovement();
            UpdateMeshRotation();
        }

        #region Player Movement

        private void UpdatePlayerMovement()
        {
            CheckForJump();
            CheckGroundedState();

            switch (_playerStateStack.Peek())
            {
                case PlayerState.Idle:
                    UpdateIdleState();
                    break;
                case PlayerState.Moving:
                    UpdateMovingState();
                    break;
                case PlayerState.Falling:
                    UpdateFallingState();
                    break;
                case PlayerState.Slide:
                    UpdateSlideState();
                    break;
                case PlayerState.WallRun:
                    UpdateWallRunState();
                    break;
                case PlayerState.RailGrind:
                    UpdateRailGrindState();
                    break;
                case PlayerState.Ability1:
                    UpdateAbility1State();
                    break;
                case PlayerState.Ability2:
                    UpdateAbility2State();
                    break;
                case PlayerState.Ability3:
                    UpdateAbility3State();
                    break;

                case PlayerState.CUSTOM_MOVEMENT:
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ApplyPlayerMovement();
        }

        private void CheckForJump()
        {
            // If we are not running the Base Movement let the systems control
            // What happens when Jumping...
            if (IsSpecialMovementStateActive())
            {
                return;
            }

            if (!_characterController.isGrounded)
            {
                return;
            }

            if (_jumpAction.WasPressedThisFrame())
            {
                TriggerJump(_jumpLaunchSpeed);
            }
        }

        private void TriggerJump(float launchVelocity)
        {
            _moveVelocity.y = launchVelocity;
            onJumped?.Invoke();
        }

        private void CheckGroundedState()
        {
            // If we are not running the Base Movement let the systems control
            // What happens when Falling down...
            if (IsSpecialMovementStateActive())
            {
                return;
            }

            // If we just jumped this frame, give some time for the game to register a jump.
            // We can easily process this next frame...
            if (_jumpAction.WasPressedThisFrame() && _characterController.isGrounded)
            {
                return;
            }

            if (_isGrounded != _characterController.isGrounded)
            {
                onGroundedStateChanged?.Invoke(_characterController.isGrounded);

                // Activate Falling State if we are not doing anything special
                if (!_characterController.isGrounded && _isGrounded)
                {
                    PushState(PlayerState.Falling);
                }
            }

            _isGrounded = _characterController.isGrounded;
            if (!_isGrounded)
            {
                // When it is falling make sure to handle proper Gravity acceleration...
                _moveVelocity.y += Physics.gravity.y * _gravityMultiplier * Time.deltaTime;
            }
            else
            {
                // When the character is on the ground it does not matter since we are at a constant speed anyway...
                _moveVelocity.y = Physics.gravity.y;
            }
        }

        private void ApplyPlayerMovement()
        {
            // Apply the final velocity...
            _characterController.Move(_moveVelocity);
        }

        #endregion

        #region Player State Handling

        private void UpdateIdleState()
        {
            // Maybe this is a bad idea of handling deceleration when Idle
            // But, the basic logic is this
            // Keep decreasing MoveSpeed till we hit 0
            if (CurrentMoveSpeed > 0)
            {
                CurrentMoveSpeed -= _decelerationRate * Time.deltaTime;
                var deltaMoveSpeed = CurrentMoveSpeed * Time.deltaTime;

                // Calculate Movement Direction
                var movement = _orientation.forward * _lastNonZeroMoveInput.y +
                               _orientation.right * _lastNonZeroMoveInput.x;
                movement.Normalize();
                movement *= deltaMoveSpeed;

                // Setup Movement...
                _moveVelocity.x = movement.x;
                _moveVelocity.z = movement.z;
            }
            else
            {
                CurrentMoveSpeed = 0;
                _moveVelocity.x = 0;
                _moveVelocity.z = 0;
            }

            if (!IsZeroMoveInput())
            {
                PushState(PlayerState.Moving);
            }
        }

        private void UpdateMovingState()
        {
            if (IsZeroMoveInput())
            {
                PopState();
            }

            // Calculate Movement Direction
            var movement = _orientation.forward * _moveInput.y + _orientation.right * _moveInput.x;
            movement.Normalize();

            // Before we apply the speed, we need to compute the direction difference.
            // So, we can reduce/increase the based...
            var dotProduct = Vector3.Dot(_moveInput, _previousFrameInput);
            var mappedSpeedMultiplier = _moveSpeedAnimCurve.Evaluate(dotProduct);

            // Calculate the final Movement speed...
            if (CurrentMoveSpeed > _moveSpeed)
            {
                CurrentMoveSpeed -= _overshootDecelerationRate * Time.deltaTime;
            }
            else
            {
                CurrentMoveSpeed += _accelerationRate * Time.deltaTime;
            }

            CurrentMoveSpeed *= mappedSpeedMultiplier;

            // Apply the speed...
            var deltaMoveSpeed = CurrentMoveSpeed * Time.deltaTime;
            movement *= deltaMoveSpeed;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;

            // Save the Previous Frame Input...
            _previousFrameInput = _moveInput;

            // If we are moving, and we press the Slide Action only, then we can perform
            // One of the 3 actions. The action performed depends on where the character is...
            if (_sprintAction.WasPressedThisFrame())
            {
                if (CanActivateWallRunSaveWallRunDirection())
                {
                    ActivateWallRun();
                }
                else if (CanActivateRailGrindSaveSplineContainer())
                {
                    ActivateRailGrind();
                }
                // Since the slide does not need any conditions per-say to activate. Check it last...
                else if (CanActivateSlide())
                {
                    ActivateSlide();
                }
            }
        }

        private void UpdateFallingState()
        {
            if (_isGrounded)
            {
                // We need to pop the state prior to pushing a new state on since we don't care aobut
                // Falling after the new states have been added...
                PopState();

                // If we land on a Rail we can start a Rail Grind...
                if (CanActivateRailGrindSaveSplineContainer())
                {
                    ActivateRailGrind();
                }
                else if (CanActivateWallRunSaveWallRunDirection())
                {
                    ActivateWallRun();
                }
            }
        }

        private bool CanActivateSlide() => !IsZeroMoveInput() && _playerStateStack.Peek() == PlayerState.Moving;

        private void ActivateSlide()
        {
            // Basically, we save the direction when we start the slide and then use that for the
            // entire duration...
            _slideDirectionInput = _lastNonZeroMoveInput;
            _slideCurrentTime = _slideDuration;
            PushState(PlayerState.Slide);
        }

        private void UpdateSlideState()
        {
            // Calculate Movement Direction
            var movement = _orientation.forward * _slideDirectionInput.y + _orientation.right * _slideDirectionInput.x;
            movement.Normalize();
            movement *= (_slideSpeed * Time.deltaTime);

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;

            // Reduce the timer...
            _slideCurrentTime -= Time.deltaTime;

            // This means the slide is over we exit the state...
            if (_slideCurrentTime <= 0)
            {
                PopState();
                return;
            }

            // Launch the Player
            if (_jumpAction.WasPressedThisFrame())
            {
                // Jump with a Boosted velocity...
                TriggerJump(_slideJumpVelocity);
                CurrentMoveSpeed = _slideSpeed;

                PopState();
            }
            // If we start falling. Boost the player a little bit...
            else if (!_characterController.isGrounded)
            {
                // Get launched forward... // TODO: This needs to be tested when the other things are polished...
                movement = _orientation.forward * _slideDirectionInput.y + _orientation.right * _slideDirectionInput.x;
                movement.Normalize();
                movement *= (_slideFallLaunchVelocity * Time.deltaTime);

                // Setup Movement...
                _moveVelocity.x = movement.x;
                _moveVelocity.z = movement.z;

                PopState();
            }
        }

        private bool CanActivateWallRunSaveWallRunDirection()
        {
            // For Wall Run we need the user to have pressed the button before triggering it...
            if (!_sprintAction.IsPressed())
            {
                return false;
            }

            if (IsZeroMoveInput())
            {
                return false;
            }

            // Mark Left Side by default...
            IsLeftWallRun = true;

            // Check Left Side
            var hitCount = Physics.RaycastNonAlloc(
                _wallRunLeftSide.position,
                -_characterMesh.right, // We depend on the character mesh for direction
                _wallRunRaycastHit,
                _wallRunDistanceCheck,
                _wallRunLayerMask
            );
            if (hitCount > 0)
            {
                return _wallRunRaycastHit[0].collider.TryGetComponent<IsWallRunnable>(out _);
            }

            hitCount = Physics.RaycastNonAlloc(
                _wallRunRightSide.position,
                _characterMesh.right, // We depend on the character mesh for direction
                _wallRunRaycastHit,
                _wallRunDistanceCheck,
                _wallRunLayerMask
            );

            if (hitCount <= 0)
            {
                return false;
            }

            IsLeftWallRun = false;

            // If we reached here means we have one of the sides stored in _isLeftWallRun
            // The rest can be handled via the update loop...
            return _wallRunRaycastHit[0].collider.TryGetComponent<IsWallRunnable>(out _);
        }

        private void ActivateWallRun()
        {
            _wallRunCurrentTime = _wallRunDuration;
            PushState(PlayerState.WallRun);
        }

        private void UpdateWallRunState()
        {
            int hitCount;
            if (IsLeftWallRun)
            {
                hitCount = Physics.RaycastNonAlloc(
                    _wallRunLeftSide.position,
                    -_characterMesh.right,
                    _wallRunRaycastHit,
                    _wallRunDistanceCheck,
                    _wallRunLayerMask
                );
            }
            else
            {
                hitCount = Physics.RaycastNonAlloc(
                    _wallRunRightSide.position,
                    _characterMesh.right,
                    _wallRunRaycastHit,
                    _wallRunDistanceCheck,
                    _wallRunLayerMask
                );
            }

            // This means we are no longer on a wall, so skip WallRunning
            if (hitCount == 0)
            {
                PopState();
                return;
            }

            var raycastHit = _wallRunRaycastHit[0];

            // This means we are no longer on a wall that is runnable, so skip WallRunning
            var isWallRunnable = raycastHit.collider.TryGetComponent<IsWallRunnable>(out _);
            if (!isWallRunnable)
            {
                PopState();
                return;
            }

            // Get the Parallel Vector
            var wallParallel = Vector3.Cross(raycastHit.normal, Vector3.up);

            // Rotate the Vector if the player is facing the opposite direction...
            if (Vector3.Dot(wallParallel, _characterMesh.forward) < 0)
            {
                wallParallel = -wallParallel;
            }

            // Apply the actual Wall Run speed..
            wallParallel *= _wallRunSpeed;

            // Now make a vector that goes into the wall
            var distanceFromWall = Vector3.Distance(transform.position, raycastHit.point);
            var correctedDistance = distanceFromWall - _wallRunAttachedDistanceCheck;
            var correctionDirection = -raycastHit.normal * (correctedDistance * _wallRunCorrectionSpeed);

            // Make a Vector that goes up and then slowly goes down...
            var upTimeLeft = _wallRunDuration - _wallRunUpCurveDuration;
            // This means that we are still in the window to keep going up slightly...
            Vector3 verticalMotionVector;
            if (_wallRunCurrentTime > upTimeLeft)
            {
                verticalMotionVector = Vector3.up * _wallRunUpCurveSpeed;
            }
            // This means the player needs to go down slowly
            else
            {
                verticalMotionVector = Vector3.down * _wallRunDownCurveSpeed;
            }

            // Final Movement
            var movement = wallParallel + correctionDirection + verticalMotionVector;
            movement *= Time.deltaTime;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.y = movement.y;
            _moveVelocity.z = movement.z;

            // Reduce the timer...
            _wallRunCurrentTime -= Time.deltaTime;

            // This means the wall run is over we exit the state...
            if (_wallRunCurrentTime <= 0)
            {
                PopState();
            }
            // If we press Jump when on a Wall Run. Find which direction is empty and launch the player
            else if (_jumpAction.WasPressedThisFrame())
            {
                // Get a Vector opposite to the Wall
                var outLaunchVector = raycastHit.normal.normalized;

                // Create a forward Vector
                wallParallel.Normalize();

                // Launch the Player...
                movement = outLaunchVector + wallParallel + Vector3.up;

                // Apply the final movement
                _moveVelocity.x = movement.x * _wallRunLaunchSpeed.x;
                _moveVelocity.z = movement.z * _wallRunLaunchSpeed.z;
                TriggerJump(movement.y * _wallRunLaunchSpeed.y);
                PopState();
            }
        }

        private bool CanActivateRailGrindSaveSplineContainer()
        {
            // We need the user to perform an action before we activate.
            // Otherwise we'll be activating it unintentionally...
            if (!_sprintAction.IsPressed())
            {
                return false;
            }

            var hitCount = Physics.OverlapSphereNonAlloc(
                _railGrindCastLocation.position,
                _railGrindCheckerRadius,
                _railGrindOverlapColliders,
                _railGrindLayerMask
            );
            if (hitCount <= 0)
            {
                return false;
            }

            var isRailGrindable = _railGrindOverlapColliders[0]
                .TryGetComponent<IsRailGrindable>(out var railGrindComponent);
            if (!isRailGrindable)
            {
                return false;
            }

            _railGrindSplineContainer = railGrindComponent.SplineContainer;
            _railGrindSplineLength = _railGrindSplineContainer.CalculateLength();

            var forwardPoint = transform.position + _characterMesh.forward * _railGrindFowardCheckDistance;
            var localCurrentPoint = _railGrindSplineContainer.transform.InverseTransformPoint(transform.position);
            var localForwardPoint = _railGrindSplineContainer.transform.InverseTransformPoint(forwardPoint);

            SplineUtility.GetNearestPoint(
                _railGrindSplineContainer.Spline,
                localCurrentPoint,
                out _,
                out var currentRatio,
                _railGrindSplineResolution,
                _railGrindSplineIterations
            );

            SplineUtility.GetNearestPoint(
                _railGrindSplineContainer.Spline,
                localForwardPoint,
                out _,
                out var nextRatio,
                _railGrindSplineResolution,
                _railGrindSplineIterations
            );

            // If true means we need to increase distance else reduce distance
            _railGrindPositive = nextRatio > currentRatio;
            _railGrindCurrentDistance = currentRatio * _railGrindSplineLength;

            return true;
        }

        private void ActivateRailGrind()
        {
            _railGrindCurrentSpeed = _railGrindSpeed;
            _railGrindBoostRemainingTime = 0;

            PushState(PlayerState.RailGrind);
        }

        private void UpdateRailGrindState()
        {
            if (_railGrindCurrentDistance > _railGrindSplineLength || _railGrindCurrentDistance < 0)
            {
                PopState();
                return;
            }

            // Basically when we are Boosting we increase the velocity
            // And then slowly go back to the original speed...
            if (_railGrindBoostRemainingTime > 0)
            {
                _railGrindBoostRemainingTime -= Time.deltaTime;
                _railGrindCurrentSpeed += _railGrindAcceleration * Time.deltaTime;
            }
            else
            {
                if (_railGrindCurrentSpeed > _railGrindSpeed)
                {
                    _railGrindCurrentSpeed -= _railGrindDeceleration * Time.deltaTime;
                }
            }

            _railGrindCurrentDistance += (_railGrindPositive ? 1 : -1) * _railGrindCurrentSpeed * Time.deltaTime;
            var railGrindRatio = _railGrindCurrentDistance / _railGrindSplineLength;
            Vector3 targetPosition = _railGrindSplineContainer.EvaluatePosition(railGrindRatio);

            // No need to normalize this since the speed is factored into it from _railGrindCurrentDistance
            var movement = targetPosition - transform.position;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.y = 0;
            _moveVelocity.z = movement.z;

            // Launch the Player
            if (_jumpAction.WasPressedThisFrame())
            {
                // Jump with a Boosted velocity
                TriggerJump(_railGrindJumpVelocity);
                PopState();
            }
            // This means the player wants a boost in movement so we should push them ahead a little...
            else if (_sprintAction.WasPressedThisFrame())
            {
                _railGrindBoostRemainingTime = _railGrindBoostDuration;
                onRailGrindBoosted?.Invoke();
            }
        }

        private void UpdateAbility1State()
        {
        }

        private void UpdateAbility2State()
        {
        }

        private void UpdateAbility3State()
        {
        }

        #endregion

        #region Update Look and Mesh

        private bool CanAdjustMeshRotation()
        {
            switch (_playerStateStack.Peek())
            {
                case PlayerState.Idle:
                case PlayerState.Moving:
                case PlayerState.Falling:
                    return true;

                case PlayerState.CUSTOM_MOVEMENT:
                case PlayerState.Slide:
                case PlayerState.WallRun:
                case PlayerState.RailGrind:
                    return false;

                case PlayerState.Ability1:
                case PlayerState.Ability2:
                case PlayerState.Ability3:
                    return true;

                default:
                    throw new ArgumentOutOfRangeException();
            }
        }

        private void UpdateMeshRotation()
        {
            _cameraPosition.y = transform.position.y;
            _cameraPosition.x = _cameraObject.transform.position.x;
            _cameraPosition.z = _cameraObject.transform.position.z;

            if (!IsZeroMoveInput() && CanAdjustMeshRotation())
            {
                var viewDirection = transform.position - _cameraPosition;
                _orientation.forward = viewDirection.normalized;

                var inputDirection = _orientation.forward * _moveInput.y + _orientation.right * _moveInput.x;
                _characterMesh.forward = Vector3.Slerp(
                    _characterMesh.forward,
                    inputDirection.normalized,
                    Time.deltaTime * _rotationSpeed
                );
            }
        }

        #endregion

        #region Input Handling

        private void SetupInput()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _moveAction.Enable();

            _jumpAction = InputSystem.actions.FindAction("Jump");
            _jumpAction.Enable();

            _sprintAction = InputSystem.actions.FindAction("Sprint");
            _sprintAction.Enable();
        }

        private void UpdateInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            if (!IsZeroMoveInput())
            {
                _lastNonZeroMoveInput = _moveInput;
            }

            _sprintAction.IsPressed();
        }

        private bool IsZeroMoveInput() =>
            ExtensionFunctions.IsNearlyZero(_moveInput.x)
            &&
            ExtensionFunctions.IsNearlyZero(_moveInput.y);

        #endregion

        #region State Functions

        private bool IsSpecialMovementStateActive() => _playerStateStack.Peek() > PlayerState.CUSTOM_MOVEMENT;

        public void PushState(PlayerState playerState)
        {
            onStateChanged?.Invoke(
                playerState,
                _playerStateStack.Count > 0 ? _playerStateStack.Peek() : PlayerState.Idle
            );
            onStatePushed?.Invoke(playerState);

            _playerStateStack.Push(playerState);
        }

        public void PopState()
        {
            var playerState = _playerStateStack.Pop();
            onStateChanged?.Invoke(_playerStateStack.Peek(), playerState);
            onStatePopped?.Invoke(playerState);
        }

        #endregion

        #region Custom Editor Display

        [CustomEditor(typeof(PlayerController))]
        public class CustomPlayerControllerEditor : Editor
        {
            public override void OnInspectorGUI()
            {
                DrawDefaultInspector();
                EditorGUILayout.Space();

                var ts = (PlayerController)target;
                var stack = ts._playerStateStack;

                if (stack != null)
                {
                    foreach (var item in stack)
                    {
                        GUILayout.Label(item.ToString());
                    }
                }
            }
        }

        #endregion
    }
}