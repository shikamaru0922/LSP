#if UNITY_EDITOR
using System.Linq;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;
using UnityEngine.SceneManagement;

[CustomEditor(typeof(MaterialReplacer))]
public class MaterialReplacerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();
        var tool = (MaterialReplacer)target;

        EditorGUILayout.Space();
        using (new EditorGUI.DisabledScope(tool.newMaterial == null))
        {
            if (GUILayout.Button("一键替换：当前场景所有 MeshRenderer 材质"))
            {
                ReplaceAll(tool, includeSkinned: false);
            }
        }

        using (new EditorGUI.DisabledScope(tool.newMaterial == null || !tool.includeSkinnedMeshRenderer))
        {
            if (tool.includeSkinnedMeshRenderer && GUILayout.Button("一键替换（含 SkinnedMeshRenderer）"))
            {
                ReplaceAll(tool, includeSkinned: true);
            }
        }

        EditorGUILayout.HelpBox(
            "说明：该操作在【编辑器】中执行，会修改当前打开的场景，并支持撤销（Ctrl/Cmd + Z）。\n" +
            "实现方式：使用 sharedMaterials 替换，保持每个 Renderer 的材质槽数量不变。",
            MessageType.Info);
    }

    private void ReplaceAll(MaterialReplacer tool, bool includeSkinned)
    {
        if (tool.newMaterial == null)
        {
            EditorUtility.DisplayDialog("材质未设置", "请先把要替换成的材质拖到 newMaterial。", "好的");
            return;
        }

        var scene = SceneManager.GetActiveScene();
        var roots = scene.GetRootGameObjects();
        bool includeInactive = tool.includeInactive;

        int changed = 0;

        try
        {
            EditorUtility.DisplayProgressBar("替换材质", "扫描场景对象中…", 0f);

            // 处理 MeshRenderer
            var meshes = roots.SelectMany(r => r.GetComponentsInChildren<MeshRenderer>(includeInactive)).ToList();
            int total = meshes.Count;
            for (int i = 0; i < total; i++)
            {
                var r = meshes[i];
                EditorUtility.DisplayProgressBar("替换材质 (MeshRenderer)", r.gameObject.name, (i + 1f) / total);

                Undo.RecordObject(r, "Replace Materials (MeshRenderer)");
                int slots = r.sharedMaterials != null && r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1;
                r.sharedMaterials = Enumerable.Repeat(tool.newMaterial, slots).ToArray();
                EditorUtility.SetDirty(r);
                changed++;
            }

            // 可选：处理 SkinnedMeshRenderer
            if (includeSkinned)
            {
                var skinneds = roots.SelectMany(r => r.GetComponentsInChildren<SkinnedMeshRenderer>(includeInactive)).ToList();
                int totalS = skinneds.Count;
                for (int i = 0; i < totalS; i++)
                {
                    var r = skinneds[i];
                    EditorUtility.DisplayProgressBar("替换材质 (SkinnedMeshRenderer)", r.gameObject.name, (i + 1f) / totalS);

                    Undo.RecordObject(r, "Replace Materials (SkinnedMeshRenderer)");
                    int slots = r.sharedMaterials != null && r.sharedMaterials.Length > 0 ? r.sharedMaterials.Length : 1;
                    r.sharedMaterials = Enumerable.Repeat(tool.newMaterial, slots).ToArray();
                    EditorUtility.SetDirty(r);
                    changed++;
                }
            }

            // 标记场景已变更（提示保存）
            EditorSceneManager.MarkSceneDirty(scene);
        }
        finally
        {
            EditorUtility.ClearProgressBar();
        }

        Debug.Log($"[MaterialReplacer] 完成：修改了 {changed} 个 Renderer。");
    }
}
#endif
