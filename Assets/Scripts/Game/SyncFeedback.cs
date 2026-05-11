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

    public void PlayFailure()
    {
        StopAllCoroutines();
        StartCoroutine(FlashSprites(_failureColor, _flashCount));
        StartCoroutine(ShakeCamera());
        if (_failureClip && _audioSource) _audioSource.PlayOneShot(_failureClip);
    }

    public void PlaySuccess()
    {
        StopAllCoroutines();
        StartCoroutine(FlashSprites(_successColor, 1));
        if (_successClip && _audioSource) _audioSource.PlayOneShot(_successClip);
    }

    private IEnumerator FlashSprites(Color flashColor, int count)
    {
        if (_p1Sprite == null || _p2Sprite == null) yield break;

        Color p1Original = _p1Sprite.color;
        Color p2Original = _p2Sprite.color;

        for (int i = 0; i < count; i++)
        {
            _p1Sprite.color = flashColor;
            _p2Sprite.color = flashColor;
            yield return new WaitForSeconds(_flashDuration);
            _p1Sprite.color = p1Original;
            _p2Sprite.color = p2Original;
            yield return new WaitForSeconds(_flashDuration);
        }
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
}
