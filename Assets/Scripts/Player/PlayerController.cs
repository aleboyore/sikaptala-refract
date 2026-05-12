using System;
using System.Collections;
using System.Reflection;
using UnityEngine;

public class PlayerController : MonoBehaviour
{
        // Protected state for gameplay logic
        protected Rigidbody2D _rb;
        protected CapsuleCollider2D _col;
        protected Vector2 _baseColliderSize;
        protected Vector2 _baseColliderOffset;
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
        protected int _airJumpsAllowed;
        protected int _airJumpsRemaining;
        protected int _dashesAllowed;
        protected int _dashesRemaining;
        protected float _dashSpeed = 20f;
        protected float _jumpHeightMultiplier = 1f;
        protected float _speedMultiplier = 1f;
        protected float _fallMultiplier = 1f;
        protected bool _hasShield = false;
        protected bool _isInvulnerable = false;
        protected bool _gravityFlipped = false;
        protected Transform _groundSupport;

        [SerializeField] protected ScriptableStats _stats;
        CharacterState _characterState;

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
            _baseColliderSize = _col.size;
            _baseColliderOffset = _col.offset;
            _characterState = GetComponent<CharacterState>();
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
            if (State != PlayerState.Normal)
                return;

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

            bool supportedByBox = _groundSupport != null;

            // Scale the cast size and offset to match the root transform scale
            float   scale      = Mathf.Abs(transform.localScale.y); // use Y; X may be negative (facing)
            Vector2 scaledSize = _baseColliderSize   * scale;
            Vector2 scaledOffset = _baseColliderOffset * scale;
            Vector2 castCenter = (Vector2)transform.position + scaledOffset;

            bool groundHit = Physics2D.CapsuleCast(
                castCenter, scaledSize, _col.direction, 0,
                _gravityFlipped ? Vector2.up : Vector2.down,
                _stats.GrounderDistance, _stats.GroundLayer);

            groundHit |= supportedByBox;

            if (groundHit)
            {
                if (!_grounded)
                {
                    _grounded = true;
                    _coyoteUsable = true;
                    _bufferedJumpUsable = true;
                    _endedJumpEarly = false;
                    _airJumpsRemaining = _airJumpsAllowed;
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

                float maxSpeed = _frameInput.Move.x * _stats.MaxSpeed * _speedMultiplier + apexBonus;
                _frameVelocity.x = Mathf.MoveTowards(
                    _frameVelocity.x,
                    maxSpeed,
                    _stats.Acceleration * Time.fixedDeltaTime);
            }
        }

        protected virtual void HandleJump()
        {
            if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y > 0)
                _endedJumpEarly = true;

            if (!_jumpToConsume && !HasBufferedJump) return;
            if (_grounded || CanUseCoyote)
            {
                ExecuteJump();
            }
            else if (ConsumeAirJump())
            {
                ExecuteJump();
            }
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
            _frameVelocity.y = _stats.JumpPower * _jumpHeightMultiplier; // P1 jumps positive Y (up)
            if (_gravityFlipped)
                _frameVelocity.y *= -1f;
            _characterState?.SetAnimBool("isJump", true);
            _characterState?.SetAnimBool("isFalling", false);
            RaiseJumped();
        }

        protected bool ConsumeAirJump()
        {
            if (_airJumpsRemaining <= 0) return false;

            _airJumpsRemaining--;
            _airJumpsAllowed--;   // consumable: spent charges do not replenish on landing
            return true;
        }

        protected virtual void HandleGravity()
        {
            if (_grounded)
            {
                _frameVelocity.y = _gravityFlipped ? -_stats.GroundingForce : _stats.GroundingForce;
            }
            else
            {
                var inAirGravity = _stats.FallAcceleration * _fallMultiplier;
                if (_endedJumpEarly && _frameVelocity.y > 0)
                    inAirGravity *= _stats.JumpEndEarlyGravityModifier;

                float targetFallSpeed = _gravityFlipped ? _stats.MaxFallSpeed : -_stats.MaxFallSpeed;
                _frameVelocity.y = Mathf.MoveTowards(
                    _frameVelocity.y, targetFallSpeed, inAirGravity * Time.fixedDeltaTime);
            }
            _characterState?.SetAnimBool("isFalling", !_grounded && _frameVelocity.y < 0f);
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
            _characterState?.SetAnimBool("isRunning", Mathf.Abs(_frameInput.Move.x) > 0.01f);
            HandleSpriteDirection();
        }

        protected virtual void HandleSpriteDirection()
        {
            Vector3 scale = transform.localScale;
            float uniformScale = Mathf.Max(Mathf.Abs(scale.x), Mathf.Abs(scale.y));

            if (_frameInput.Move.x > 0.01f)
            {
                // Moving right - no flip
                scale.x = uniformScale;
                scale.y = uniformScale;
                transform.localScale = scale;
            }
            else if (_frameInput.Move.x < -0.01f)
            {
                // Moving left - flip
                scale.x = -uniformScale;
                scale.y = uniformScale;
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
        public bool IsGrounded => _grounded;

        public void SetFallMultiplier(float multiplier)
        {
            _fallMultiplier = Mathf.Max(0.01f, multiplier);
        }

        public void EnterFrozenState(float duration)
        {
            if (State == PlayerState.LockedIn) return;
            State = PlayerState.Frozen;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeAll;
            
            // Animation idle gating: if grounded, show idle; else keep current animation
            if (_grounded)
            {
                _characterState?.SetAnimBool("isRunning", false);
                _characterState?.SetAnimBool("isJump", false);
                _characterState?.SetAnimBool("isFalling", false);
            }
            
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

            _fallMultiplier = 1f;
            transform.position = position;
            _airJumpsRemaining = _airJumpsAllowed;
            _jumpHeightMultiplier = 1f;
            _speedMultiplier = 1f;
            _hasShield = false;
            _isInvulnerable = false;
        }

        public void SetAirJumpCount(int count)
        {
            _airJumpsAllowed = Mathf.Max(0, count);
            _airJumpsRemaining = _airJumpsAllowed;
        }

        public int GetAirJumpsAllowed()
        {
            return _airJumpsAllowed;
        }

        public int GetAirJumpsRemaining()
        {
            return _airJumpsRemaining;
        }

        public void SetDashCount(int count, float speed)
        {
            _dashesAllowed = Mathf.Max(0, count);
            _dashesRemaining = _dashesAllowed;
            _dashSpeed = speed;
        }

        public int GetDashesAllowed()
        {
            return _dashesAllowed;
        }

        public int GetDashesRemaining()
        {
            return _dashesRemaining;
        }

        public void ResetPowerupState()
        {
            _airJumpsAllowed = 0;
            _airJumpsRemaining = 0;
            _dashesAllowed = 0;
            _dashesRemaining = 0;
            _dashSpeed = 20f;
            _jumpHeightMultiplier = 1f;
            _speedMultiplier = 1f;
            _hasShield = false;
            _isInvulnerable = false;
            _groundSupport = null;
            _grounded = false;
            _coyoteUsable = false;
            _bufferedJumpUsable = false;
            _endedJumpEarly = false;
            _frameVelocity = Vector2.zero;

            if (_gravityFlipped)
                FlipGravity();
        }

        public bool ConsumeDash(out float dashSpeed)
        {
            dashSpeed = _dashSpeed;
            if (_dashesRemaining <= 0) return false;

            _dashesRemaining--;
            return true;
        }

        public void SetJumpHeightMultiplier(float multiplier)
        {
            _jumpHeightMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void SetSpeedMultiplier(float multiplier)
        {
            _speedMultiplier = Mathf.Max(0.1f, multiplier);
        }

        public void ForceGrounded()
        {
            if (_grounded) return;

            _grounded = true;
            _frameLeftGrounded = _time;
            _coyoteUsable = true;
            _bufferedJumpUsable = true;
            _endedJumpEarly = false;
            _frameVelocity.y = 0f;

            _characterState?.SetAnimBool("isJump", false);
            _characterState?.SetAnimBool("isFalling", false);
            RaiseGroundedChanged(true, 0f);
        }

        public void SetGroundSupport(Transform support)
        {
            _groundSupport = support;
            ForceGrounded();
        }

        public void ClearGroundSupport(Transform support)
        {
            if (_groundSupport == support)
                _groundSupport = null;
        }

        public void GrantShield()
        {
            _hasShield = true;
        }

        public void GrantInvulnerability(float duration)
        {
            _isInvulnerable = true;
            PowerupEffectRunner.Run(gameObject, null, InvulnerabilityRoutine(duration), duration);
        }

        private System.Collections.IEnumerator InvulnerabilityRoutine(float duration)
        {
            yield return new WaitForSeconds(duration);
            _isInvulnerable = false;
        }

        public bool CanDie()
        {
            // Check invulnerability first
            if (_isInvulnerable) return false;

            // Check shield, consume it
            if (_hasShield)
            {
                _hasShield = false;
                return false;
            }

            return true;
        }

        public void FlipGravity()
        {
            _gravityFlipped = !_gravityFlipped;
            _grounded = false;
            _coyoteUsable = false;
            _bufferedJumpUsable = false;
            _endedJumpEarly = false;

            if (_rb != null)
                _rb.linearVelocity = new Vector2(_rb.linearVelocity.x, 0f);

            _frameVelocity.y = 0f;
        }

        public void SnapToX(float worldX)
        {
            _frameVelocity.x = 0f;
            if (_rb != null) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            transform.position = new Vector3(worldX, transform.position.y, transform.position.z);
        }

        public void LerpToX(float worldX, float duration, Action onComplete = null)
        {
            StartCoroutine(LerpToXRoutine(worldX, duration, onComplete));
        }

        private IEnumerator LerpToXRoutine(float worldX, float duration, Action onComplete)
        {
            State = PlayerState.Syncing;
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezePositionY | RigidbodyConstraints2D.FreezeRotation;
            if (_rb) _rb.linearVelocity = Vector2.zero;
            _frameVelocity = Vector2.zero;

            float startX = transform.position.x;
            float elapsed = 0f;

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
                float currentX = Mathf.Lerp(startX, worldX, EaseInOutCubic(t));
                transform.position = new Vector3(currentX, transform.position.y, transform.position.z);
                yield return null;
            }

            transform.position = new Vector3(worldX, transform.position.y, transform.position.z);
            _frameVelocity.x = 0f;
            if (_rb) _rb.linearVelocity = new Vector2(0f, _rb.linearVelocity.y);
            if (_rb) _rb.constraints = RigidbodyConstraints2D.FreezeRotation;
            State = PlayerState.Normal;
            onComplete?.Invoke();
        }

        private float EaseInOutCubic(float t)
        {
            return t < 0.5f
                ? 4f * t * t * t
                : 1f - Mathf.Pow(-2f * t + 2f, 3f) / 2f;
        }

        private void OnGroundedChanged(bool grounded, float yVel)
        {
            if (_characterState == null) return;
            if (grounded)
            {
                _characterState.SetAnimBool("isJump", false);
                _characterState.SetAnimBool("isFalling", false);
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
