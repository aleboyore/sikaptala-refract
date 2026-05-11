using System.Collections;
using UnityEngine;

public class SyncFeedback : MonoBehaviour
{
    [Header("Failure — Red Flash")]
    [SerializeField] private SpriteRenderer _p1Sprite;
    [SerializeField] private SpriteRenderer _p2Sprite;
    [SerializeField] private Color _failureColor = new Color(1f, 0.2f, 0.2f, 1f);
    [SerializeField] private float _flashDuration = 0.15f;
    [SerializeField] private int _flashCount = 2;

    [Header("Failure — Screen Shake")]
    [SerializeField] private Transform _cameraTransform;
    [SerializeField] private float _shakeMagnitude = 0.12f;
    [SerializeField] private float _shakeDuration = 0.25f;

    [Header("Success — White Flash")]
    [SerializeField] private Color _successColor = new Color(1f, 1f, 1f, 1f);

    [Header("Audio")]
    [SerializeField] private AudioSource _audioSource;
    [SerializeField] private AudioClip _failureClip;
    [SerializeField] private AudioClip _successClip;

    private Color _p1OriginalColor;
    private Color _p2OriginalColor;
    private Coroutine _flashRoutine;
    private Coroutine _shakeRoutine;

    private void Awake()
    {
        if (_p1Sprite != null) _p1OriginalColor = _p1Sprite.color;
        if (_p2Sprite != null) _p2OriginalColor = _p2Sprite.color;
    }

    public void PlayFailure()
    {
        StopFeedbackRoutines();
        _flashRoutine = StartCoroutine(FlashSprites(_failureColor, _flashCount));
        _shakeRoutine = StartCoroutine(ShakeCamera());
        if (_failureClip && _audioSource) _audioSource.PlayOneShot(_failureClip);
    }

    public void PlaySuccess()
    {
        StopFeedbackRoutines();
        _flashRoutine = StartCoroutine(FlashSprites(_successColor, 1));
        if (_successClip && _audioSource) _audioSource.PlayOneShot(_successClip);
    }

    private void StopFeedbackRoutines()
    {
        if (_flashRoutine != null)
        {
            StopCoroutine(_flashRoutine);
            _flashRoutine = null;
        }

        if (_shakeRoutine != null)
        {
            StopCoroutine(_shakeRoutine);
            _shakeRoutine = null;
        }

        RestoreOriginalColors();
    }

    private IEnumerator FlashSprites(Color flashColor, int count)
    {
        if (_p1Sprite == null || _p2Sprite == null) yield break;

        for (int i = 0; i < count; i++)
        {
            _p1Sprite.color = flashColor;
            _p2Sprite.color = flashColor;
            yield return new WaitForSeconds(_flashDuration);
            RestoreOriginalColors();
            yield return new WaitForSeconds(_flashDuration);
        }

        RestoreOriginalColors();
    }

    private IEnumerator ShakeCamera()
    {
        if (_cameraTransform == null) yield break;

        Vector3 origin = _cameraTransform.localPosition;
        float elapsed = 0f;

        while (elapsed < _shakeDuration)
        {
            float x = Random.Range(-1f, 1f) * _shakeMagnitude;
            float y = Random.Range(-1f, 1f) * _shakeMagnitude;
            _cameraTransform.localPosition = origin + new Vector3(x, y, 0f);
            elapsed += Time.deltaTime;
            yield return null;
        }

        _cameraTransform.localPosition = origin;
    }

    private void RestoreOriginalColors()
    {
        if (_p1Sprite != null) _p1Sprite.color = _p1OriginalColor;
        if (_p2Sprite != null) _p2Sprite.color = _p2OriginalColor;
    }
}
