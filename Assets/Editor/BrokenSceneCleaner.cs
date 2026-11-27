using System.Collections.Generic;
using UnityEngine;
using UnityEngine.SceneManagement;
using UnityEditor;
using UnityEditor.SceneManagement;

public static class BrokenSceneCleaner
{
    [MenuItem("Tools/Cleanup/Scan Broken Objects")]
    public static void Scan()
    {
        ProcessScene(false);
    }

    [MenuItem("Tools/Cleanup/Remove Broken Objects")]
    public static void Clean()
    {
        ProcessScene(true);
    }

    private static void ProcessScene(bool remove)
    {
        if (EditorApplication.isPlayingOrWillChangePlaymode)
            return;

        if (PrefabStageUtility.GetCurrentPrefabStage() != null)
        {
            Debug.LogError("Exit Prefab Mode first.");
            return;
        }

        Scene scene = SceneManager.GetActiveScene();

        if (!scene.IsValid() || !scene.isLoaded)
            return;

        var brokenPrefabs = new List<GameObject>();
        var brokenScripts = new List<GameObject>();

        int missingScriptsCount = 0;

        foreach (GameObject root in scene.GetRootGameObjects())
        {
            Collect(
                root,
                brokenPrefabs,
                brokenScripts,
                ref missingScriptsCount
            );
        }

        foreach (GameObject obj in brokenPrefabs)
        {
            Debug.LogWarning(
                $"Missing prefab: {obj.name} | " +
                $"Position: {obj.transform.position}",
                obj
            );
        }

        foreach (GameObject obj in brokenScripts)
        {
            Debug.LogWarning(
                $"Missing scripts: {obj.name}",
                obj
            );
        }

        string report =
            $"Scene: {scene.name}\n" +
            $"Missing prefabs: {brokenPrefabs.Count}\n" +
            $"Missing scripts: {missingScriptsCount}";

        Debug.Log(report);

        if (!remove)
            return;

        if (!EditorUtility.DisplayDialog(
            "Clean broken scene",
            report + "\n\nRemove broken objects?",
            "Remove",
            "Cancel"))
        {
            return;
        }

        Undo.IncrementCurrentGroup();
        int undoGroup = Undo.GetCurrentGroup();

        Undo.SetCurrentGroupName("Clean Broken Scene");

        int removedPrefabs = 0;
        int removedScripts = 0;

        foreach (GameObject obj in brokenPrefabs)
        {
            if (obj == null)
                continue;

            Undo.DestroyObjectImmediate(obj);
            removedPrefabs++;
        }

        foreach (GameObject obj in brokenScripts)
        {
            if (obj == null)
                continue;

            Undo.RegisterCompleteObjectUndo(
                obj,
                "Remove Missing Scripts"
            );

            removedScripts +=
                GameObjectUtility
                    .RemoveMonoBehavioursWithMissingScript(obj);
        }

        Undo.CollapseUndoOperations(undoGroup);

        EditorSceneManager.MarkSceneDirty(scene);

        Debug.Log(
            $"Cleanup complete!\n" +
            $"Removed prefabs: {removedPrefabs}\n" +
            $"Removed scripts: {removedScripts}"
        );
    }

    private static void Collect(
        GameObject obj,
        List<GameObject> brokenPrefabs,
        List<GameObject> brokenScripts,
        ref int missingScriptsCount)
    {
        // Remove the entire broken prefab hierarchy.
        if (PrefabUtility.IsPrefabAssetMissing(obj))
        {
            brokenPrefabs.Add(obj);
            return;
        }

        // Remove missing script components.
        int count = GameObjectUtility
            .GetMonoBehavioursWithMissingScriptCount(obj);

        if (count > 0)
        {
            brokenScripts.Add(obj);
            missingScriptsCount += count;
        }

        // Include inactive child objects.
        foreach (Transform child in obj.transform)
        {
            Collect(
                child.gameObject,
                brokenPrefabs,
                brokenScripts,
                ref missingScriptsCount
            );
        }
    }
}