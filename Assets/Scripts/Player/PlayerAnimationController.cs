using System;
using UnityEngine;

namespace Player
{
    public class PlayerAnimationController : MonoBehaviour
    {
        // Basic Movement
        private static readonly int Moving = Animator.StringToHash("Moving");
        private static readonly int Jump = Animator.StringToHash("Jump");
        private static readonly int Falling = Animator.StringToHash("Falling");

        // Additional Single Button Movement
        private static readonly int Sliding = Animator.StringToHash("Sliding");
        private static readonly int WallRun = Animator.StringToHash("WallRunning");
        private static readonly int RailGrind = Animator.StringToHash("RailGrinding");

        [Header("Components")]
        [SerializeField] private Animator _animator;
        [SerializeField] private PlayerController _playerController;

        private void Start()
        {
            _playerController.onJumped += HandlePlayerJumped;
            _playerController.onStatePushed += HandleStatePushed;
            _playerController.onStatePopped += HandleStatePopped;
        }

        private void OnDestroy()
        {
            _playerController.onJumped -= HandlePlayerJumped;
            _playerController.onStatePushed -= HandleStatePushed;
            _playerController.onStatePopped -= HandleStatePopped;
        }

        #region State Functions

        private void HandlePlayerJumped()
        {
            _animator.SetTrigger(Jump);
        }

        private void HandleStatePushed(PlayerState pushedState)
        {
            switch (pushedState)
            {
                case PlayerState.Idle:
                    break;

                case PlayerState.Moving:
                    _animator.SetBool(Moving, true);
                    break;

                case PlayerState.Falling:
                    _animator.SetBool(Falling, true);
                    break;

                case PlayerState.Slide:
                    _animator.SetBool(Sliding, true);
                    break;

                case PlayerState.WallRun:
                    _animator.SetBool(WallRun, true);
                    break;

                case PlayerState.RailGrind:
                    _animator.SetBool(RailGrind, true);
                    break;

                case PlayerState.Ability1:
                    break;
                case PlayerState.Ability2:
                    break;
                case PlayerState.Ability3:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(pushedState), pushedState, null);
            }
        }

        private void HandleStatePopped(PlayerState poppedState)
        {
            switch (poppedState)
            {
                case PlayerState.Idle:
                    break;

                case PlayerState.Moving:
                    _animator.SetBool(Moving, false);
                    break;

                case PlayerState.Falling:
                    _animator.SetBool(Falling, false);
                    break;

                case PlayerState.Slide:
                    _animator.SetBool(Sliding, false);
                    break;

                case PlayerState.WallRun:
                    _animator.SetBool(WallRun, false);
                    break;

                case PlayerState.RailGrind:
                    _animator.SetBool(RailGrind, false);
                    break;

                case PlayerState.Ability1:
                    break;
                case PlayerState.Ability2:
                    break;
                case PlayerState.Ability3:
                    break;
                default:
                    throw new ArgumentOutOfRangeException(nameof(poppedState), poppedState, null);
            }
        }

        #endregion
    }
}