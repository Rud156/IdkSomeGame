using System;
using System.Collections.Generic;
using Global;
using Global.GameObjectMarkers;
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
        [SerializeField] private float _accelerationRate;
        [SerializeField] private float _decelerationRate;
        [SerializeField] private float _gravityMultiplier;
        [SerializeField] private float _jumpLaunchSpeed;

        [Header("Secondary Movement")]
        [Header("Slide")]
        [SerializeField] private float _slideSpeed;
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
        [SerializeField] private float _wallRunDebugDuration;
        [Header("Rail Grind")]
        [SerializeField] private Transform _railGrindCastLocation;
        [SerializeField] private float _railGrindSpeed;
        [SerializeField] private float _railGrindDistanceCheck;
        [SerializeField] private LayerMask _railGrindLayerMask;
        [SerializeField] private float _railGrindFowardCheckDistance;
        [SerializeField] private int _railGrindSplineResolution;
        [SerializeField] private int _railGrindSplineIterations;

        [Header("Mesh Controls")]
        [SerializeField] private float _rotationSpeed;

        // Additional Components
        private Transform _cameraObject;

        // Inputs
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        // Input Data
        private float _currentMoveSpeed;
        private Vector2 _moveInput;
        private Vector2 _lastMoveInput;

        // Player State
        private Stack<PlayerState> _playerStateStack;
        private bool _isGrounded;
        private Vector3 _moveVelocity;
        private Vector3 _cameraPosition;
        private RaycastHit[] _raycastHit; // This is shared by Raycasts used in this class

        // Slide Data
        private Vector2 _slideDirectionInput; // The player is not allowed to change directions when sliding...
        private float _slideCurrentTime;

        // Wall Run Data
        public bool IsLeftWallRun { get; private set; }
        private float _wallRunCurrentTime;

        // Rail Grind Data
        private SplineContainer _railGrindSplineContainer;
        private float _railGrindSplineLength;
        private float _railGrindCurrentDistance;
        private bool _railGrindPositive;

        // Delegates
        public delegate void OnJumped();
        public delegate void OnGroundedStateChanged(bool currentState);
        public delegate void OnStateChanged(PlayerState currentState, PlayerState previousState);
        public delegate void OnStatePushed(PlayerState pushedState);
        public delegate void OnStatePopped(PlayerState poppedState);

        public OnJumped onJumped;
        public OnGroundedStateChanged onGroundedStateChanged;
        public OnStateChanged onStateChanged;
        public OnStatePushed onStatePushed;
        public OnStatePopped onStatePopped;

        private void Start()
        {
            CursorController.Instance.EnableCursor(false);
            _cameraObject = GameObject.FindGameObjectWithTag(GameTags.MainCamera).transform;

            _raycastHit = new RaycastHit[1];

            _currentMoveSpeed = 0;
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
                default:
                    throw new ArgumentOutOfRangeException();
            }

            ApplyPlayerMovement();
        }

        private void CheckForJump()
        {
            if (!_characterController.isGrounded)
            {
                return;
            }

            if (_jumpAction.WasPressedThisFrame())
            {
                _moveVelocity.y = _jumpLaunchSpeed;
                onJumped?.Invoke();
            }
        }

        private void CheckGroundedState()
        {
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
                if (!_characterController.isGrounded && _isGrounded && !IsSpecialMovementStateActive())
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
            // But the basic logic is this
            // Keep decreasing MoveSpeed till we hit 0
            _currentMoveSpeed -= _decelerationRate * Time.deltaTime;
            _currentMoveSpeed = Mathf.Clamp(_currentMoveSpeed, 0, _moveSpeed);
            var deltaMoveSpeed = _currentMoveSpeed * Time.deltaTime;

            // Calculate Movement Direction
            var movement = _orientation.forward * _lastMoveInput.y + _orientation.right * _lastMoveInput.x;
            movement.Normalize();
            movement *= deltaMoveSpeed;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;

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

            // Calculate the speed at which the player will be moving...
            _currentMoveSpeed += _accelerationRate * Time.deltaTime;
            _currentMoveSpeed = Mathf.Clamp(_currentMoveSpeed, 0, _moveSpeed);
            var deltaMoveSpeed = _currentMoveSpeed * Time.deltaTime;

            // Calculate Movement Direction
            var movement = _orientation.forward * _lastMoveInput.y + _orientation.right * _lastMoveInput.x;
            movement.Normalize();
            movement *= deltaMoveSpeed;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;

            // If we are moving, and we press the Slide Action only then we can perform
            // One of the 3 actions. The action performed depends on where the character is...
            if (_sprintAction.WasPressedThisFrame())
            {
                if (CanActivateWallRunSaveWallRunDirection())
                {
                    _wallRunCurrentTime = _wallRunDuration;
                    PushState(PlayerState.WallRun);
                }
                else if (CanActivateRailGrindSaveSplineContainer())
                {
                    PushState(PlayerState.RailGrind);
                }
                // Since slide does not need any conditions per-say to activate. Check it last...
                else if (CanActivateSlide())
                {
                    // Basically, we save the direction when we start the slide and then use that for the
                    // entire duration...
                    _slideDirectionInput = _lastMoveInput;
                    _slideCurrentTime = _slideDuration;
                    PushState(PlayerState.Slide);
                }
            }
        }

        private void UpdateFallingState()
        {
            if (_isGrounded)
            {
                PopState();
            }
        }

        private bool CanActivateSlide() => !IsZeroMoveInput() && _playerStateStack.Peek() == PlayerState.Moving;

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
            }
        }

        private bool CanActivateWallRunSaveWallRunDirection()
        {
            var isValidMovement = !IsZeroMoveInput() && _playerStateStack.Peek() == PlayerState.Moving;
            if (!isValidMovement)
            {
                return false;
            }

            // Mark Left Side by default...
            IsLeftWallRun = true;

            // Check Left Side
            var hitCount = Physics.RaycastNonAlloc(
                _wallRunLeftSide.position,
                -_characterMesh.right, // We depend on the character mesh for direction
                _raycastHit,
                _wallRunDistanceCheck,
                _wallRunLayerMask
            );
            if (hitCount > 0)
            {
                return _raycastHit[0].collider.TryGetComponent<IsWallRunnable>(out _);
            }

            hitCount = Physics.RaycastNonAlloc(
                _wallRunRightSide.position,
                _characterMesh.right, // We depend on the character mesh for direction
                _raycastHit,
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
            return _raycastHit[0].collider.TryGetComponent<IsWallRunnable>(out _);
        }

        private void UpdateWallRunState()
        {
            int hitCount;
            if (IsLeftWallRun)
            {
                hitCount = Physics.RaycastNonAlloc(
                    _wallRunLeftSide.position,
                    -_characterMesh.right,
                    _raycastHit,
                    _wallRunDistanceCheck,
                    _wallRunLayerMask
                );
            }
            else
            {
                hitCount = Physics.RaycastNonAlloc(
                    _wallRunRightSide.position,
                    _characterMesh.right,
                    _raycastHit,
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

            var raycastHit = _raycastHit[0];

            // Debug Draw
            Debug.DrawLine(
                IsLeftWallRun ? _wallRunLeftSide.position : _wallRunRightSide.position,
                raycastHit.point,
                Color.red,
                _wallRunDebugDuration
            );

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

            var distanceFromWall = Vector3.Distance(transform.position, raycastHit.point);
            var correctedDistance = distanceFromWall - _wallRunAttachedDistanceCheck;

            // Now make a vector that goes into the wall
            var correctionDirection = -raycastHit.normal * (correctedDistance * _wallRunCorrectionSpeed);
            var movement = (wallParallel * _wallRunSpeed) + correctionDirection;
            movement *= Time.deltaTime;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;

            // Reduce the timer...
            _wallRunCurrentTime -= Time.deltaTime;

            // This means the wall run is over we exit the state...
            if (_wallRunCurrentTime <= 0)
            {
                PopState();
            }
        }

        private bool CanActivateRailGrindSaveSplineContainer()
        {
            var isValidMovement = !IsZeroMoveInput() && _playerStateStack.Peek() == PlayerState.Moving;
            if (!isValidMovement)
            {
                return false;
            }

            var hitCount = Physics.RaycastNonAlloc(
                _railGrindCastLocation.position,
                Vector3.down,
                _raycastHit,
                _railGrindDistanceCheck,
                _railGrindLayerMask
            );
            if (hitCount <= 0)
            {
                return false;
            }

            var isRailGrindable = _raycastHit[0].collider.TryGetComponent<IsRailGrindable>(out var railGrindComponent);
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

        private void UpdateRailGrindState()
        {
            if (_railGrindCurrentDistance > _railGrindSplineLength || _railGrindCurrentDistance < 0)
            {
                PopState();
                return;
            }

            _railGrindCurrentDistance += (_railGrindPositive ? 1 : -1) * _railGrindSpeed * Time.deltaTime;
            var railGrindRatio = _railGrindCurrentDistance / _railGrindSplineLength;
            Vector3 targetPosition = _railGrindSplineContainer.EvaluatePosition(railGrindRatio);

            // No need to normalize this since the speed is factored into it from _railGrindCurrentDistance
            var movement = targetPosition - transform.position;

            // Setup Movement...
            _moveVelocity.x = movement.x;
            _moveVelocity.z = movement.z;
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
                _lastMoveInput = _moveInput;
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
    }
}