#if UNITY_EDITOR
using UnityEditor;

// Prevents a sporadic NullReferenceException inside UnityEditor.GameObjectInspector.OnDisable
// when entering Play Mode by ensuring the Inspector is not inspecting a specific GameObject
// at the transition time. This is a harmless workaround for an Editor-only issue.
[InitializeOnLoad]
internal static class PlayModeSelectionGuard
{
 static PlayModeSelectionGuard()
 {
 EditorApplication.playModeStateChanged += OnPlayModeStateChanged;
 }

 private static void OnPlayModeStateChanged(PlayModeStateChange state)
 {
        // Right before exiting edit mode, clear selection so the Inspector isn't bound
        // to a (possibly changing) GameObject, which can trigger NREs in some editor versions.
        if (state == PlayModeStateChange.ExitingEditMode)
        {
            if (Selection.activeObject != null)
            {
                EditorApplication.delayCall += () => { try { Selection.activeObject = null; } catch { } };
            }
        }
    }
}
#endif
