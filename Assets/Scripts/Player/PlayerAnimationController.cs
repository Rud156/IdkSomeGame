using System;
using UnityEngine;

namespace Player
{
    public class PlayerAnimationController : MonoBehaviour
    {
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Falling = Animator.StringToHash("Falling");

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerController _playerController;

        private void Start()
        {
            _playerController.onJumped += HandlePlayerJumped;
            _playerController.onGroundedStateChanged += HandleGroundedStateChanged;
            _playerController.onStateChanged += HandlePlayerStateChanged;
        }

        private void OnDestroy()
        {
            _playerController.onJumped -= HandlePlayerJumped;
            _playerController.onGroundedStateChanged -= HandleGroundedStateChanged;
            _playerController.onStateChanged -= HandlePlayerStateChanged;
        }

        #region State Functions

        private void HandlePlayerJumped()
        {
            _animator.SetTrigger(Jump);
        }

        private void HandleGroundedStateChanged(bool currentState)
        {
            _animator.SetBool(Falling, !currentState);
        }

        private void HandlePlayerStateChanged(PlayerState currentState, PlayerState previousState)
        {
            switch (currentState)
            {
                case PlayerState.Idle:
                    _animator.SetBool(Moving, false);
                    break;

                case PlayerState.Moving:
                    _animator.SetBool(Moving, true);
                    break;

                case PlayerState.Falling:
                    _animator.SetBool(Falling, true);
                    break;

                case PlayerState.Slide:
                    break;
                case PlayerState.WallRun:
                    break;
                case PlayerState.RailGrind:
                    break;
                case PlayerState.Ability1:
                    break;
                case PlayerState.Ability2:
                    break;
                case PlayerState.Ability3:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(currentState), currentState, null);
            }
        }

        #endregion
    }
}