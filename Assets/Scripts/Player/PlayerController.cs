using System;
using System.Collections.Generic;
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

        [Header("Movement")]
        [SerializeField] private float _moveSpeed;
        [SerializeField] private float _accelerationRate;
        [SerializeField] private float _decelerationRate;
        [SerializeField] private float _jumpLaunchSpeed;

        // Inputs
        private InputAction _moveAction;
        private InputAction _lookAction;
        private InputAction _jumpAction;
        private InputAction _sprintAction;
        // Input Data
        private Vector2 _moveInput;
        private Vector2 _lookInput;
        private bool _sprintPressed;

        // Player State
        private Stack<PlayerState> _playerStateStack;
        private bool _isGrounded;
        private Vector3 _moveVelocity;

        // Delegates
        public delegate void OnJumped();
        public delegate void OnGroundedStateChanged(bool currentState);
        public delegate void OnStateChanged(PlayerState currentState, PlayerState previousState);

        public OnJumped onJumped;
        public OnGroundedStateChanged onGroundedStateChanged;
        public OnStateChanged onStateChanged;

        private void Start()
        {
            _moveVelocity = Vector3.zero;

            _playerStateStack = new Stack<PlayerState>();
            PushState(PlayerState.Idle);

            SetupInput();
        }

        private void Update()
        {
            UpdateInput();
            UpdatePlayerMovement();
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
            }
        }

        private void CheckGroundedState()
        {
            if (_isGrounded == _characterController.isGrounded)
            {
                return;
            }

            onGroundedStateChanged?.Invoke(_characterController.isGrounded);
            _isGrounded = _characterController.isGrounded;
        }

        private void ApplyPlayerMovement()
        {
            _characterController.Move(_moveVelocity * Time.deltaTime);
        }

        #endregion

        #region Player State Handling

        private void UpdateIdleState()
        {
            if (IsZeroMoveInput())
            {
                return;
            }
            
            PushState(PlayerState.Moving);
        }

        private void UpdateMovingState()
        {
        }

        private void UpdateFallingState()
        {
        }

        private void UpdateSlideState()
        {
        }

        private void UpdateWallRunState()
        {
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
        }

        #endregion

        #region Input Handling

        private void SetupInput()
        {
            _moveAction = InputSystem.actions.FindAction("Move");
            _moveAction.Enable();

            _lookAction = InputSystem.actions.FindAction("Look");
            _lookAction.Enable();

            _jumpAction = InputSystem.actions.FindAction("Jump");
            _jumpAction.Enable();

            _sprintAction = InputSystem.actions.FindAction("Sprint");
            _sprintAction.Enable();
        }

        private void UpdateInput()
        {
            _moveInput = _moveAction.ReadValue<Vector2>();
            _lookInput = _lookAction.ReadValue<Vector2>();
            _sprintPressed = _sprintAction.IsPressed();
        }

        private bool IsZeroMoveInput()
        {
            return ExtensionFunctions.IsNearlyZero(_moveInput.x) && ExtensionFunctions.IsNearlyZero(_moveInput.y);
        }

        #endregion

        #region State Functions

        public void PushState(PlayerState playerState)
        {
            onStateChanged?.Invoke(playerState, _playerStateStack.Peek());
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