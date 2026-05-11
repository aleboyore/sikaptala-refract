using System.Collections;
using UnityEngine;

public class SyncChainVFX : MonoBehaviour
{
    [Header("Chain Line")]
    [SerializeField] private LineRenderer _lineRenderer;
    [SerializeField] private float _lineWidth = 0.08f;
    [SerializeField] private Color _chainColor = new Color(0.4f, 0.9f, 1f, 1f);

    [Header("Chain Links")]
    [SerializeField] private GameObject _linkPrefab;
    [SerializeField] private int _linkCount = 6;
    [SerializeField] private float _linkScale = 0.18f;

    [Header("Arrival Burst")]
    [SerializeField] private ParticleSystem _burstParticles;

    [Header("Pulse")]
    [SerializeField] private float _pulseSpeed = 4f;
    [SerializeField] private float _pulseAmplitude = 0.02f;

    private Transform _p1Transform;
    private Transform _p2Transform;
    private GameObject[] _links;
    private Coroutine _chainRoutine;
    private bool _active;

    private void Awake()
    {
        _links = new GameObject[_linkCount];
        for (int i = 0; i < _linkCount; i++)
        {
            if (_linkPrefab == null) break;
            _links[i] = Instantiate(_linkPrefab, transform);
            _links[i].SetActive(false);
        }

        if (_lineRenderer != null)
        {
            _lineRenderer.positionCount = 2;
            _lineRenderer.startWidth = _lineWidth;
            _lineRenderer.endWidth = _lineWidth;
            _lineRenderer.enabled = false;
        }
    }

    public void PlayChain(Transform p1, Transform p2, float duration)
    {
        if (_chainRoutine != null) StopCoroutine(_chainRoutine);
        _p1Transform = p1;
        _p2Transform = p2;
        _chainRoutine = StartCoroutine(ChainRoutine(duration));
    }

    public void PlayArrivalBurst()
    {
        _active = false;
        HideChain();
        if (_burstParticles != null) _burstParticles.Play();
    }

    private IEnumerator ChainRoutine(float duration)
    {
        _active = true;

        if (_lineRenderer != null) _lineRenderer.enabled = true;
        if (_links != null)
        {
            foreach (var link in _links)
            {
                if (link != null) link.SetActive(true);
            }
        }

        float elapsed = 0f;
        while (_active && elapsed < duration + 0.1f)
        {
            elapsed += Time.deltaTime;
            UpdateChainVisuals(elapsed, duration);
            yield return null;
        }

        HideChain();
    }

    private void UpdateChainVisuals(float elapsed, float duration)
    {
        if (_p1Transform == null || _p2Transform == null || _lineRenderer == null) return;

        Vector3 p1Pos = _p1Transform.position;
        Vector3 p2Projected = new Vector3(-_p2Transform.position.x, p1Pos.y, p1Pos.z);

        float t = duration <= 0f ? 1f : Mathf.Clamp01(elapsed / duration);
        float alpha = Mathf.Lerp(1f, 0f, t);
        Color color = _chainColor;
        color.a = alpha;
        _lineRenderer.startColor = color;
        _lineRenderer.endColor = color;
        _lineRenderer.SetPosition(0, p1Pos);
        _lineRenderer.SetPosition(1, p2Projected);

        float pulse = 1f + Mathf.Sin(Time.time * _pulseSpeed) * _pulseAmplitude;
        _lineRenderer.startWidth = _lineWidth * pulse;
        _lineRenderer.endWidth = _lineWidth * pulse;

        if (_links == null) return;
        for (int i = 0; i < _links.Length; i++)
        {
            if (_links[i] == null) continue;
            float frac = (i + 1f) / (_links.Length + 1f);
            Vector3 pos = Vector3.Lerp(p1Pos, p2Projected, frac);
            pos.y += Mathf.Sin(Time.time * _pulseSpeed + i * 0.8f) * 0.05f;
            _links[i].transform.position = pos;
            float scl = Mathf.Lerp(1f, 0.1f, t);
            _links[i].transform.localScale = Vector3.one * (_linkScale * scl);
        }
    }

    private void HideChain()
    {
        if (_lineRenderer != null) _lineRenderer.enabled = false;
        if (_links == null) return;

        foreach (var link in _links)
        {
            if (link != null) link.SetActive(false);
        }
    }
}