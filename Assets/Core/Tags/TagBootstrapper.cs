using UnityEngine;

namespace DCC.Core.Tags
{
    /// <summary>
    /// Loads all TagDefinition assets and initializes the TagRegistry before any
    /// other system starts. Place this script on a GameObject in the bootstrap scene
    /// with Script Execution Order set to -1000 (before everything else).
    ///
    /// In production, replace Resources.LoadAll with Addressables for async loading
    /// and better memory management.
    /// </summary>
    [DefaultExecutionOrder(-1000)]
    public class TagBootstrapper : MonoBehaviour
    {
        [Tooltip("Override: if provided, only these tags are registered. " +
                 "Leave empty to auto-load all tags from Resources/DCC/Tags/.")]
        [SerializeField] private TagDefinition[] _explicitTags;

        private void Awake()
        {
            if (TagRegistry.Instance != null) return; // Already initialized.

            TagDefinition[] tags;

            if (_explicitTags != null && _explicitTags.Length > 0)
            {
                tags = _explicitTags;
            }
            else
            {
                tags = Resources.LoadAll<TagDefinition>("DCC/Tags");
                if (tags == null || tags.Length == 0)
                {
                    Debug.LogWarning(
                        "[TagBootstrapper] No TagDefinitions found in Resources/DCC/Tags/. " +
                        "Create tag assets there or assign them explicitly.");
                    tags = new TagDefinition[0];
                }
            }

            TagRegistry.Initialize(tags);
            Debug.Log($"[TagBootstrapper] Initialized TagRegistry with {tags.Length} tags.");
        }
    }
}
