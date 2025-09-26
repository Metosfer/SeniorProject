using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// Legacy placeholder kept to avoid missing-script errors in existing scenes.
/// New persistence logic lives directly on <see cref="WorldItem"/>.
/// </summary>
[DisallowMultipleComponent]
public class WorldItemIdentity : MonoBehaviour
{
    private const string DeprecationMessage = "WorldItemIdentity is deprecated. Please remove this component; WorldItem now handles persistence internally.";

    private void Awake()
    {
        if (Application.isPlaying)
        {
            Debug.LogWarning(DeprecationMessage, this);
            Destroy(this);
        }
    }

#if UNITY_EDITOR
    private void OnValidate()
    {
        if (!Application.isPlaying)
        {
            UnityEditor.EditorApplication.delayCall += () =>
            {
                if (this == null) return;
                Debug.LogWarning(DeprecationMessage, this);
            };
        }
    }
#endif
}
