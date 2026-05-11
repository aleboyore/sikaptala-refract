using UnityEngine;

namespace TarodevController
{
	public class MirrorController : PlayerController
	{
		protected override void Awake()
		{
			base.Awake();
			// P2 falls toward +Y; gravity handled in HandleGravity override
		}

		protected override void GatherInput()
		{
			if (InputHandler.Instance != null)
				_frameInput = InputHandler.Instance.P2Input;

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

		protected override void CheckCollisions()
		{
			Physics2D.queriesStartInColliders = false;

			bool groundHit = Physics2D.CapsuleCast(
				_col.bounds.center, _col.size, _col.direction, 0,
				Vector2.up, _stats.GrounderDistance, _stats.GroundLayer);

			bool ceilingHit = Physics2D.CapsuleCast(
				_col.bounds.center, _col.size, _col.direction, 0,
				Vector2.down, _stats.GrounderDistance, _stats.GroundLayer);

			if (ceilingHit) _frameVelocity.y = Mathf.Max(0, _frameVelocity.y);

			if (!_grounded && groundHit)
			{
				_grounded = true;
				_coyoteUsable = true;
				_bufferedJumpUsable = true;
				_endedJumpEarly = false;
				RaiseGroundedChanged(true, Mathf.Abs(_frameVelocity.y));
			}
			else if (_grounded && !groundHit)
			{
				_grounded = false;
				_frameLeftGrounded = _time;
				RaiseGroundedChanged(false, 0);
			}

			Physics2D.queriesStartInColliders = _cachedQueryStartInColliders;
		}

		protected override void ExecuteJump()
		{
			_endedJumpEarly = false;
			_timeJumpWasPressed = 0;
			_bufferedJumpUsable = false;
			_coyoteUsable = false;
			// ensure airborne state so gravity logic doesn't cancel the jump
			_grounded = false;
			_frameVelocity.y = -_stats.JumpPower; // P2 jumps away from the ceiling
			if (_animator)
			{
				_animator.SetBool("isJump", true);
				_animator.SetBool("isFalling", false);
			}
			RaiseJumped();
		}

		protected override void HandleJump()
		{
			if (!_endedJumpEarly && !_grounded && !_frameInput.JumpHeld && _rb.linearVelocity.y < 0)
				_endedJumpEarly = true;

			if (!_jumpToConsume && !HasBufferedJump) return;
			if (_grounded || CanUseCoyote) ExecuteJump();
			_jumpToConsume = false;
		}

		protected override void HandleGravity()
		{
			if (_grounded && _frameVelocity.y >= 0f)
			{
				_frameVelocity.y = Mathf.Abs(_stats.GroundingForce);
			}
			else
			{
				var inAirGravity = _stats.FallAcceleration;
				if (_endedJumpEarly && _frameVelocity.y < 0)
					inAirGravity *= _stats.JumpEndEarlyGravityModifier;

				_frameVelocity.y = Mathf.MoveTowards(
					_frameVelocity.y, _stats.MaxFallSpeed, inAirGravity * Time.fixedDeltaTime);
			}

			if (_animator) _animator.SetBool("isFalling", !_grounded && _frameVelocity.y > 0f);
		}

		protected override void HandleSpriteDirection()
		{
			if (_frameInput.Move.x > 0.01f)
			{
				// Moving right - flip for P2
				var scale = transform.localScale;
				scale.x = -1f;
				transform.localScale = scale;
			}
			else if (_frameInput.Move.x < -0.01f)
			{
				// Moving left - no flip for P2
				var scale = transform.localScale;
				scale.x = 1f;
				transform.localScale = scale;
			}
		}
	}
}
