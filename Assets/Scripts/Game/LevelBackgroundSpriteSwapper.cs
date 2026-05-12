using UnityEngine;

/// <summary>
/// Swaps a background SpriteRenderer between two sprites based on the player's skin state.
/// Attach this to a background GameObject, then assign the SpriteRenderer and the Shard/Veil sprites.
/// </summary>
[RequireComponent(typeof(SpriteRenderer))]
public class LevelBackgroundSpriteSwapper : MonoBehaviour
{
	[SerializeField] private SpriteRenderer _targetRenderer;
	[SerializeField] private CharacterState _characterState;
	[SerializeField] private Sprite _shardSprite;
	[SerializeField] private Sprite _veilSprite;

	private Coroutine _bindRetryCoroutine;

	private void Reset()
	{
		_targetRenderer = GetComponent<SpriteRenderer>();
	}

	private void Awake()
	{
		if (_targetRenderer == null)
		{
			_targetRenderer = GetComponent<SpriteRenderer>();
		}
	}

	private void OnEnable()
	{
		BindCharacterState();
		ApplyCurrentState();
	}

	private void OnDisable()
	{
		UnbindCharacterState();
	}

	private void OnValidate()
	{
		if (_targetRenderer == null)
		{
			_targetRenderer = GetComponent<SpriteRenderer>();
		}
	}

	/// <summary>
	/// Assign a new state source at runtime if needed.
	/// </summary>
	public void SetCharacterState(CharacterState characterState)
	{
		if (_characterState == characterState)
		{
			return;
		}

		UnbindCharacterState();
		_characterState = characterState;
		BindCharacterState();
		ApplyCurrentState();
	}

	private void BindCharacterState()
	{
		if (_characterState == null)
		{
			_characterState = FindAnyObjectByType<CharacterState>();
		}

		if (_characterState != null)
		{
			_characterState.onStateChanged.AddListener(HandleStateChanged);
			Debug.Log($"[LevelBackgroundSpriteSwapper] Bound to CharacterState on '{_characterState.gameObject.name}'");
			if (_bindRetryCoroutine != null)
			{
				StopCoroutine(_bindRetryCoroutine);
				_bindRetryCoroutine = null;
			}
		}
		else
		{
			// start a short retry loop in case CharacterState is created shortly after scene load
			if (_bindRetryCoroutine == null)
			{
				_bindRetryCoroutine = StartCoroutine(BindRetryRoutine());
			}
		}
	}

	private System.Collections.IEnumerator BindRetryRoutine()
	{
		const int maxAttempts = 20; // ~10s with 0.5s delay
		int attempts = 0;
		while (attempts < maxAttempts && _characterState == null)
		{
			yield return new WaitForSeconds(0.5f);
			_characterState = FindAnyObjectByType<CharacterState>();
			if (_characterState != null)
			{
				_characterState.onStateChanged.AddListener(HandleStateChanged);
				Debug.Log($"[LevelBackgroundSpriteSwapper] Bound to CharacterState on '{_characterState.gameObject.name}' (delayed)");
				ApplyCurrentState();
				_bindRetryCoroutine = null;
				yield break;
			}
			attempts++;
		}
		_bindRetryCoroutine = null;
		Debug.LogWarning("[LevelBackgroundSpriteSwapper] Failed to bind to CharacterState after retries; background will not auto-swap.");
	}

	private void UnbindCharacterState()
	{
		if (_characterState != null)
		{
			_characterState.onStateChanged.RemoveListener(HandleStateChanged);
		}

		if (_bindRetryCoroutine != null)
		{
			StopCoroutine(_bindRetryCoroutine);
			_bindRetryCoroutine = null;
		}
	}

	private void HandleStateChanged(PlayerSkinState state)
	{
		ApplyState(state);
	}

	private void ApplyCurrentState()
	{
		if (_characterState == null)
		{
			return;
		}

		ApplyState(_characterState.current);
	}

	private void ApplyState(PlayerSkinState state)
	{
		if (_targetRenderer == null)
		{
			return;
		}

		Sprite nextSprite = state == PlayerSkinState.Shard ? _shardSprite : _veilSprite;
		if (nextSprite == null)
		{
			Debug.LogWarning($"[LevelBackgroundSpriteSwapper] Missing sprite for state '{state}' on '{gameObject.name}'");
			return;
		}

		_targetRenderer.sprite = nextSprite;
		Debug.Log($"[LevelBackgroundSpriteSwapper] Applied '{state}' background sprite on '{gameObject.name}'");
	}
}
