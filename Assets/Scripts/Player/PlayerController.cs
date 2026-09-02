using System;
using System.Collections.Generic;
using Global;
using UnityEngine;
using UnityEngine.InputSystem;
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
        [SerializeField] private float _wallRunSpeed;
        [SerializeField] private float _wallRunDuration;
        [SerializeField] private float _wallRunDistanceCheck;
        [Header("Rail Grind")]
        [SerializeField] private float _railGrindSpeed;

        [Header("Mesh Controls")]
        [SerializeField] private float _rotationSpeed;

        // Additional Components
        private Transform _cameraObject;

        // Inputs
        private InputAction _moveAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        // Input Data
        [SerializeField] private float _currentMoveSpeed;
        [SerializeField] private Vector2 _moveInput;
        private Vector2 _lastMoveInput;
        private bool _sprintPressed;

        // Player State
        private Stack<PlayerState> _playerStateStack;
        private bool _isGrounded;
        [SerializeField] private Vector3 _moveVelocity;
        private Vector3 _cameraPosition;

        // Delegates
        public delegate void OnJumped();
        public delegate void OnGroundedStateChanged(bool currentState);
        public delegate void OnStateChanged(PlayerState currentState, PlayerState previousState);

        public OnJumped onJumped;
        public OnGroundedStateChanged onGroundedStateChanged;
        public OnStateChanged onStateChanged;

        private void Start()
        {
            CursorController.EnableCursor(false);
            _cameraObject = GameObject.FindGameObjectWithTag(GameTags.MainCamera).transform;

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
            // If we just jumped this frame give some time for the game to register a jump
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
                if (CanActivateSlide())
                {
                    PushState(PlayerState.Slide);
                }
                else if (CanActivateWallRun())
                {
                    PushState(PlayerState.WallRun);
                }
                else if (CanActivateRailGrind())
                {
                    PushState(PlayerState.RailGrind);
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
        }

        private bool CanActivateWallRun()
        {
            return false;
        }

        private void UpdateWallRunState()
        {
        }

        private bool CanActivateRailGrind()
        {
            return false;
        }

        private void UpdateRailGrindState()
        {
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

        private void UpdateMeshRotation()
        {
            _cameraPosition.y = transform.position.y;
            _cameraPosition.x = _cameraObject.transform.position.x;
            _cameraPosition.z = _cameraObject.transform.position.z;

            var viewDirection = transform.position - _cameraPosition;
            _orientation.forward = viewDirection.normalized;

            var inputDirection = _orientation.forward * _moveInput.y + _orientation.right * _moveInput.x;
            if (!IsZeroMoveInput())
            {
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

            _sprintPressed = _sprintAction.IsPressed();
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
            _playerStateStack.Push(playerState);
        }

        public void PopState()
        {
            var playerState = _playerStateStack.Pop();
            onStateChanged?.Invoke(_playerStateStack.Peek(), playerState);
        }

        #endregion
    }
}