using UnityEditor;
using UnityEngine;

[CustomEditor(typeof(PrintPaperDropController))]
public class PrintPaperDropControllerEditor : Editor
{
    public override void OnInspectorGUI()
    {
        DrawDefaultInspector();

        var controller = (PrintPaperDropController)target;

        EditorGUILayout.Space();
        EditorGUILayout.LabelField("Inspector Test", EditorStyles.boldLabel);

        using (new EditorGUI.DisabledScope(!Application.isPlaying))
        {
            if (GUILayout.Button("Open Paper Output"))
            {
                controller.OpenPaperOutput();
            }

            if (GUILayout.Button("Close Paper Output"))
            {
                controller.ClosePaperOutput();
            }

            if (GUILayout.Button("Spawn One Paper"))
            {
                controller.SpawnOnePaper();
            }
        }

        if (!Application.isPlaying)
        {
            EditorGUILayout.HelpBox("Enter Play Mode to test paper output buttons.", MessageType.Info);
        }
    }
}
