using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using TMPro;

namespace DCC.UI
{
    /// <summary>
    /// Client-side HUD component that listens to DiscoverySystem events and shows
    /// a toast notification when the player finds a new combination.
    ///
    /// Attach to a Canvas GameObject. Wire up the Text/Panel references in the inspector.
    ///
    /// The notification queue prevents overlapping toasts — each one fades in, stays,
    /// then fades out before the next appears.
    /// </summary>
    public class DiscoveryHUD : MonoBehaviour
    {
        [Header("Toast Panel")]
        [SerializeField] private GameObject _toastPanel;
        [SerializeField] private TextMeshProUGUI _headerText;
        [SerializeField] private TextMeshProUGUI _nameText;
        [SerializeField] private CanvasGroup _canvasGroup;

        [Header("Timing")]
        [SerializeField] private float _fadeInDuration = 0.3f;
        [SerializeField] private float _displayDuration = 3.0f;
        [SerializeField] private float _fadeOutDuration = 0.5f;

        [Header("Discovery Log Panel (optional)")]
        [SerializeField] private Transform _logContainer;
        [SerializeField] private GameObject _logEntryPrefab;
        [SerializeField] private int _maxLogEntries = 20;

        private readonly Queue<string> _pendingToasts = new();
        private bool _showing;
        private readonly List<GameObject> _logEntries = new();

        private void OnEnable()
        {
            DiscoverySystem.OnLocalDiscovery += HandleLocalDiscovery;
            DiscoverySystem.OnGlobalDiscovery += HandleGlobalDiscovery;
        }

        private void OnDisable()
        {
            DiscoverySystem.OnLocalDiscovery -= HandleLocalDiscovery;
            DiscoverySystem.OnGlobalDiscovery -= HandleGlobalDiscovery;
        }

        private void HandleLocalDiscovery(int sig, string name)
        {
            _pendingToasts.Enqueue(name);
            AddToLog($"[You] {name}");
            if (!_showing) StartCoroutine(ShowNextToast());
        }

        private void HandleGlobalDiscovery(ulong discoverer, string name)
        {
            AddToLog($"[World First] {name}");
        }

        // ── Toast coroutine ────────────────────────────────────────────────

        private IEnumerator ShowNextToast()
        {
            while (_pendingToasts.Count > 0)
            {
                _showing = true;
                string name = _pendingToasts.Dequeue();

                if (_toastPanel != null) _toastPanel.SetActive(true);
                if (_headerText != null) _headerText.text = "New Discovery!";
                if (_nameText != null) _nameText.text = name;

                yield return Fade(0f, 1f, _fadeInDuration);
                yield return new WaitForSeconds(_displayDuration);
                yield return Fade(1f, 0f, _fadeOutDuration);

                if (_toastPanel != null) _toastPanel.SetActive(false);
            }
            _showing = false;
        }

        private IEnumerator Fade(float from, float to, float duration)
        {
            if (_canvasGroup == null) yield break;
            float elapsed = 0f;
            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                _canvasGroup.alpha = Mathf.Lerp(from, to, elapsed / duration);
                yield return null;
            }
            _canvasGroup.alpha = to;
        }

        // ── Log panel ─────────────────────────────────────────────────────

        private void AddToLog(string text)
        {
            if (_logContainer == null || _logEntryPrefab == null) return;

            var entry = Instantiate(_logEntryPrefab, _logContainer);
            var tmp = entry.GetComponentInChildren<TextMeshProUGUI>();
            if (tmp != null) tmp.text = text;

            _logEntries.Add(entry);

            while (_logEntries.Count > _maxLogEntries)
            {
                Destroy(_logEntries[0]);
                _logEntries.RemoveAt(0);
            }
        }
    }
}
