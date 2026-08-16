using System;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.InputSystem;

namespace Player
{
    public class PlayerController : MonoBehaviour
    {
        [Header("Components")]
        [SerializeField] private CharacterController _characterController;

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
            _moveAction = InputSystem.actions.FindAction("Move");
            _lookAction = InputSystem.actions.FindAction("Look");
            _jumpAction = InputSystem.actions.FindAction("Jump");
            _sprintAction = InputSystem.actions.FindAction("Sprint");

            _moveVelocity = Vector3.zero;

            _playerStateStack = new Stack<PlayerState>();
            PushState(PlayerState.Idle);
        }

        private void Update()
        {
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

            _moveVelocity.y = _jumpLaunchSpeed;
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