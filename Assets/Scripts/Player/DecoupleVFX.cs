using System.Collections;
using UnityEngine;

namespace TarodevController
{
    public class DecoupleVFX : MonoBehaviour
    {
        [Header("Freeze Burst — Shards")]
        [SerializeField] private ParticleSystem _shardBurst;

        [Header("Freeze Sustain — Frost Mist")]
        [SerializeField] private ParticleSystem _frostMist;

        [Header("Thaw Pop — Shimmer")]
        [SerializeField] private ParticleSystem _thawPop;

        [Header("Tint")]
        [SerializeField] private Color _shardColor = new Color(0.5f, 0.95f, 1f, 1f);
        [SerializeField] private Color _mistColor = new Color(0.6f, 0.8f, 1f, 0.6f);

        private Coroutine _thawRoutine;

        private void Awake()
        {
            ApplyColor(_shardBurst, _shardColor);
            ApplyColor(_frostMist, _mistColor);
        }

        public void PlayFreeze(float duration)
        {
            if (_thawRoutine != null) StopCoroutine(_thawRoutine);

            _shardBurst?.Play();
            _frostMist?.Play();

            _thawRoutine = StartCoroutine(ThawAfter(duration));
        }

        public void PlayThaw()
        {
            _frostMist?.Stop(true, ParticleSystemStopBehavior.StopEmitting);
            _thawPop?.Play();
        }

        public void StopAll()
        {
            if (_thawRoutine != null)
            {
                StopCoroutine(_thawRoutine);
                _thawRoutine = null;
            }

            _shardBurst?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _frostMist?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
            _thawPop?.Stop(true, ParticleSystemStopBehavior.StopEmittingAndClear);
        }

        private IEnumerator ThawAfter(float duration)
        {
            yield return new WaitForSeconds(duration);
            PlayThaw();
        }

        private void ApplyColor(ParticleSystem ps, Color color)
        {
            if (ps == null) return;
            var main = ps.main;
            main.startColor = new ParticleSystem.MinMaxGradient(color);
        }
    }
}