using Unity.Netcode;
using UnityEngine;
using UnityEngine.UI;
using DCC.Core.Entities;
using DCC.Core.Allegiance;

namespace DCC.UI
{
    /// <summary>
    /// World-space health bar that follows an entity.
    /// Reads NetworkHealth (replicated) so any client sees accurate values.
    ///
    /// Color coding:
    ///   Green  = allied entity
    ///   Red    = neutral/enemy entity
    ///   Yellow = self
    /// </summary>
    public class HealthBarUI : MonoBehaviour
    {
        [SerializeField] private Slider _slider;
        [SerializeField] private Image _fillImage;
        [SerializeField] private Color _selfColor = Color.yellow;
        [SerializeField] private Color _allyColor = Color.green;
        [SerializeField] private Color _enemyColor = Color.red;

        private EntityAttributes _target;
        private AllegianceComponent _targetAllegiance;
        private AllegianceComponent _localAllegiance;

        public void Initialize(EntityAttributes target)
        {
            _target = target;
            _targetAllegiance = target.GetComponent<AllegianceComponent>();

            // Find local player's allegiance component for color comparison.
            var localPlayer = NetworkManager.Singleton?.LocalClient?.PlayerObject;
            _localAllegiance = localPlayer?.GetComponent<AllegianceComponent>();

            UpdateColor();
        }

        private void LateUpdate()
        {
            if (_target == null) return;

            // Always face the camera.
            var cam = Camera.main;
            if (cam != null)
                transform.rotation = Quaternion.LookRotation(transform.position - cam.transform.position);

            // Update health bar fill.
            if (_target.NetworkMaxHealth > 0f)
                _slider.value = _target.NetworkHealth / _target.NetworkMaxHealth;
        }

        private void UpdateColor()
        {
            if (_fillImage == null || _targetAllegiance == null || _localAllegiance == null) return;

            if (_targetAllegiance.OwnerClientId == _localAllegiance.OwnerClientId)
                _fillImage.color = _selfColor;
            else if (_localAllegiance.IsAlliedWith(_targetAllegiance))
                _fillImage.color = _allyColor;
            else
                _fillImage.color = _enemyColor;
        }
    }
}
