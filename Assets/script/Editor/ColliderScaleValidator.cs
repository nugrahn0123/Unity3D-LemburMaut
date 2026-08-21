using System.Collections.Generic;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

public static class ColliderScaleValidator
{
    [MenuItem("Tools/Validation/Find Negative Scale Colliders")]
    public static void FindNegativeScaleColliders()
    {
        Collider[] colliders = Object.FindObjectsByType<Collider>(FindObjectsSortMode.None);
        List<GameObject> invalidObjects = new List<GameObject>();

        foreach (Collider col in colliders)
        {
            if (col == null)
                continue;

            Vector3 scale = col.transform.lossyScale;
            if (scale.x < 0f || scale.y < 0f || scale.z < 0f)
            {
                invalidObjects.Add(col.gameObject);
            }
        }

        if (invalidObjects.Count == 0)
        {
            Debug.Log("Validation: Tidak ada collider dengan negative scale di scene aktif.");
            Selection.objects = new Object[0];
            return;
        }

        Object[] selected = new Object[invalidObjects.Count];
        for (int i = 0; i < invalidObjects.Count; i++)
        {
            GameObject go = invalidObjects[i];
            selected[i] = go;
            Debug.LogWarning($"Negative Scale Collider: {GetHierarchyPath(go.transform)} | scale={go.transform.lossyScale}", go);
        }

        Selection.objects = selected;
        Debug.LogWarning($"Validation: Ditemukan {invalidObjects.Count} object collider dengan negative scale. Object sudah diseleksi di Hierarchy.");
    }

    [MenuItem("Tools/Validation/Disable Negative Scale BoxColliders")]
    public static void DisableNegativeScaleBoxColliders()
    {
        BoxCollider[] boxColliders = Object.FindObjectsByType<BoxCollider>(FindObjectsSortMode.None);
        int disabledCount = 0;

        foreach (BoxCollider box in boxColliders)
        {
            if (box == null || !box.enabled)
                continue;

            Vector3 scale = box.transform.lossyScale;
            if (scale.x >= 0f && scale.y >= 0f && scale.z >= 0f)
                continue;

            Undo.RecordObject(box, "Disable Negative Scale BoxCollider");
            box.enabled = false;
            disabledCount++;
            Debug.LogWarning($"Disabled BoxCollider (negative scale): {GetHierarchyPath(box.transform)} | scale={scale}", box);
            EditorUtility.SetDirty(box);
        }

        if (disabledCount > 0)
        {
            EditorSceneManager.MarkSceneDirty(SceneManager.GetActiveScene());
            Debug.LogWarning($"Validation: Menonaktifkan {disabledCount} BoxCollider dengan negative scale di scene aktif.");
        }
        else
        {
            Debug.Log("Validation: Tidak ada BoxCollider aktif dengan negative scale untuk dinonaktifkan.");
        }
    }

    private static string GetHierarchyPath(Transform current)
    {
        if (current == null)
            return "<null>";

        string path = current.name;
        while (current.parent != null)
        {
            current = current.parent;
            path = current.name + "/" + path;
        }

        return path + " (Scene: " + SceneManager.GetActiveScene().name + ")";
    }
}
