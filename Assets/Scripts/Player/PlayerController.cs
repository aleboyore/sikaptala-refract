using System.Collections;
using System.Reflection;
using UnityEngine;

namespace TarodevController
{
    public class PlayerController : MonoBehaviour
    {
        // Protected state (made protected for MirrorController overrides)
        protected Rigidbody2D _rb;
        protected CapsuleCollider2D _col;
        protected FrameInput _frameInput;
        protected Vector2 _frameVelocity;
        protected bool _grounded;
        protected float _frameLeftGrounded;
        protected bool _jumpToConsume;
        protected bool _bufferedJumpUsable;
        protected bool _endedJumpEarly;
        protected bool _coyoteUsable;
        protected float _timeJumpWasPressed;
        protected float _time;
        protected bool _cachedQueryStartInColliders;
        protected bool _isOnDoor;

        [SerializeField] protected ScriptableStats _stats;
        [SerializeField] public Animator _animator;

        [SerializeField] private float _apexThreshold = 2f;
        [SerializeField] private float _apexBonus = 2f;

        public PlayerState State { get; private set; } = PlayerState.Normal;

        public delegate void GroundChanged(bool grounded, float yVel);
        public event GroundChanged GroundedChanged;
        public delegate void JumpedEvent();
        public event JumpedEvent Jumped;

        // Protected event raisers so derived classes can raise them safely
        protected void RaiseGroundedChanged(bool grounded, float yVel)
        {
            GroundedChanged?.Invoke(grounded, yVel);
        }

        protected void RaiseJumped()
        {
            Jumped?.Invoke();
        }

        protected virtual void Awake()
        {
            _rb = GetComponent<Rigidbody2D>();
            _col = GetComponent<CapsuleCollider2D>();
            if (_animator == null) _animator = GetComponent<Animator>();
            if (_rb) _rb.gravityScale = 0;
            _cachedQueryStartInColliders = Physics2D.queriesStartInColliders;
            GroundedChanged += OnGroundedChanged;
        }

        private void OnDestroy()
        {
            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
            GroundedChanged -= OnGroundedChanged;
        }

        protected virtual void GatherInput()
        {
            if (InputHandler.Instance != null)
                _frameInput = InputHandler.Instance.P1Input;

            if (_stats != null && _stats.SnapInput)
            {
                _frameInput.Move.x = Mathf.Abs(_frameInput.Move.x) < _stats.HorizontalDeadZoneThreshold
                    ? 0 : Mathf.Sign(_frameInput.Move.x);
                _frameInput.Move.y = Mathf.Abs(_frameInput.Move.y) < _stats.VerticalDeadZoneThreshold
                    ? 0 : Mathf.Sign(_frameInput.Move.y);
            }

            if (_frameInput.JumpDown)
            {
                _jumpToConsume = true;
                _timeJumpWasPressed = _time;
            }
        }

        protected virtual void CheckCollisions()
        {
            Physics2D.queriesStartInColliders = false;

            bool groundHit = Physics2D.CapsuleCast(
                _col.bounds.center, _col.size, _col.direction, 0,
                Vector2.down, _stats.GrounderDistance, _stats.GroundLayer);

            if (groundHit)
            {
                if (!_grounded)
                {
                    _grounded = true;
                    _coyoteUsable = true;
                    _bufferedJumpUsable = true;
                    _endedJumpEarly = false;
                    RaiseGroundedChanged(true, Mathf.Abs(_frameVelocity.y));
                }
            }
            else if (_grounded && !groundHit)
            {
                _grounded = false;
                _frameLeftGrounded = _time;
                RaiseGroundedChanged(false, 0);
            }

            Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
        }

        protected virtual void HandleDirection()
        {
            if (_frameInput.Move.x == 0)
            {
                var deceleration = _grounded ? _stats.GroundDeceleration : _stats.AirDeceleration;
                _frameVelocity.x = Mathf.MoveTowards(_frameVelocity.x, 0, deceleration * Time.fixedDeltaTime);
            }
            else
            {
                float apexBonus = 0f;
                if (!_grounded && Mathf.Abs(_rb.linearVelocity.y) < _apexThreshold)
                    apexBonus = _apexBonus * _frameInput.Move.x;

                _frameVelocity.x = Mathf.MoveTowards(
                    _frameVelocity.x,
                    _frameInput.Move.x * _stats.MaxSpeed + apexBonus,
                    _stats.Acceleration * Time.fixedDeltaTime);
            }
        }

        protected virtual void HandleJump()
        {
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y > 0)
                _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump) return;
            if (_grounded || CanUseCoyote) ExecuteJump();
            _jumpToConsume = false;
        }

        protected virtual void ExecuteJump()
        {
            _endedJumpEarly = false;
            _timeJumpWasPressed = 0;
            _bufferedJumpUsable = false;
            _coyoteUsable = false;
            // ensure we are considered airborne immediately so gravity logic doesn't cancel the jump
            _grounded = false;
            _frameVelocity.y = _stats.JumpPower; // P1 jumps positive Y (up)
            if (_animator)
            {
                _animator.SetBool("isJump", true);
                _animator.SetBool("isFalling", false);
            }
            RaiseJumped();
        }

        protected virtual void HandleGravity()
        {
            if (_grounded && _frameVelocity.y <= 0f)
            {
                _frameVelocity.y = _stats.GroundingForce;
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration;
                if (_endedJumpEarly && _frameVelocity.y > 0)
                    inAirGravity *= _stats.JumpEndEarlyGravityModifier;

                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, -_stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
            if (_animator) _animator.SetBool("isFalling", !_grounded && _frameVelocity.y < 0f);
        }

        private void Update()
        {
            _time += Time.deltaTime;
            GatherInput();

            // Lock-in detection uses raw actions to avoid movement conflicts
            if (_col != null && InputHandler.Instance != null && InputHandler.Instance.Actions != null)
            {
                if (_isOnDoor
                    && InputHandler.Instance.Actions.Gameplay.LockIn.WasPressedThisFrame()
                    && State == PlayerState.Normal)
                {
                    EnterLockedInState();
                    GameManager.Instance?.NotifyLockIn(this);
                }
            }
        }

        private void FixedUpdate()
        {
            if (State != PlayerState.Normal) return;

            CheckCollisions();
            HandleJump();
            HandleDirection();
            HandleGravity();
            ApplyMovement();
            if (_animator) _animator.SetBool("isRunning", Mathf.Abs(_frameInput.Move.x) > 0.01f);
            HandleSpriteDirection();
        }

        protected virtual void HandleSpriteDirection()
        {
            if (_frameInput.Move.x > 0.01f)
            {
                // Moving right - no flip
                var scale = transform.localScale;
                scale.x = 1f;
                transform.localScale = scale;
            }
            else if (_frameInput.Move.x < -0.01f)
            {
                // Moving left - flip
                var scale = transform.localScale;
                scale.x = -1f;
                transform.localScale = scale;
            }
        }

        protected void ApplyMovement()
        {
            // Apply the computed frame velocity to the Rigidbody2D
            if (_rb != null)
            {
                _rb.linearVelocity = _frameVelocity;
            }
        }

        public bool CanUseCoyote => _coyoteUsable && (_time - _frameLeftGrounded) <= _stats.CoyoteTime;
        public bool HasBufferedJump => _bufferedJumpUsable && (_time - _timeJumpWasPressed) <= _stats.JumpBuffer;

        public void EnterFrozenState(float duration)
        {
            if (State == PlayerState.LockedIn) return;
            State = PlayerState.Frozen;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            StartCoroutine(FreezeRoutine(duration));
        }

        private IEnumerator FreezeRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            State = PlayerState.Normal;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
        }

        public void EnterLockedInState()
        {
            State = PlayerState.LockedIn;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeAll;
        }

        public void ResetToCheckpoint(Vector3 position)
        {
            State = PlayerState.Normal;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            _frameVelocity = Vector2.zero;
            transform.position = position;
        }

        public void SnapToX(float worldX)
        {
            _frameVelocity.x = 0f;
            if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            transform.position = new Vector3(worldX, transform.position.y, transform.position.z);
        }

        private void OnGroundedChanged(bool grounded, float yVel)
        {
            if (! _animator) return;
            if (grounded)
            {
                _animator.SetBool("isJump", false);
                _animator.SetBool("isFalling", false);
            }
        }

        protected virtual void OnTriggerEnter2D(Collider2D other)
        {
            if (other.CompareTag("Door"))
                _isOnDoor = true;
        }

        protected virtual void OnTriggerExit2D(Collider2D other)
        {
            if (other.CompareTag("Door"))
                _isOnDoor = false;
        }
    }
}
