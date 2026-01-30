#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations; 
using System.Collections.Generic;

namespace LSP.Gameplay
{
    [CustomEditor(typeof(NpcIdleController))]
    public class NpcIdleControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            NpcIdleController script = (NpcIdleController)target;
            Animator animator = script.GetComponent<Animator>();

            // --- 1. 动画选择部分 (保持不变) ---
            if (animator == null)
            {
                EditorGUILayout.HelpBox("Missing Animator!", MessageType.Error);
                return;
            }

            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;
            if (controller != null)
            {
                List<string> stateNames = new List<string>();
                if (controller.layers.Length > 0)
                {
                    ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
                    foreach (ChildAnimatorState childState in states) stateNames.Add(childState.state.name);
                }

                if (stateNames.Count > 0)
                {
                    int currentIndex = stateNames.IndexOf(script.stateName);
                    if (currentIndex == -1) currentIndex = 0;
                    
                    EditorGUILayout.LabelField("Animation Setup", EditorStyles.boldLabel);
                    int newIndex = EditorGUILayout.Popup("Target State", currentIndex, stateNames.ToArray());
                    string newStateName = stateNames[newIndex];
                    if (script.stateName != newStateName)
                    {
                        Undo.RecordObject(script, "Change Animation");
                        script.stateName = newStateName;
                        EditorUtility.SetDirty(script);
                    }
                }
            }

            // --- 2. 基础设置 ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Base Settings", EditorStyles.boldLabel);
            script.randomStartOffset = EditorGUILayout.Toggle("Random Start", script.randomStartOffset);
            script.loopAnimation = EditorGUILayout.Toggle("Loop Animation", script.loopAnimation);

            // --- 3. 移动设置 (新增部分) ---
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Movement (Patrol)", EditorStyles.boldLabel);

            // 开关
            bool newEnable = EditorGUILayout.Toggle("Enable Patrol", script.enableMovement);
            if (newEnable != script.enableMovement)
            {
                Undo.RecordObject(script, "Toggle Movement");
                script.enableMovement = newEnable;
            }

            // 只有开启移动时，才显示下面的参数
            if (script.enableMovement)
            {
                EditorGUI.indentLevel++; // 缩进一下，更好看
                
                script.moveSpeed = EditorGUILayout.FloatField("Move Speed", script.moveSpeed);
                script.turnSpeed = EditorGUILayout.FloatField("Turn Speed", script.turnSpeed);
                script.waitTimeAtEnd = EditorGUILayout.FloatField("Wait Time (Sec)", script.waitTimeAtEnd);

                // 距离条，带滑块
                float newDistance = EditorGUILayout.Slider("Distance (m)", script.patrolDistance, 1f, 20f);
                if (Mathf.Abs(newDistance - script.patrolDistance) > 0.01f)
                {
                    Undo.RecordObject(script, "Change Distance");
                    script.patrolDistance = newDistance;
                    // 更新一下预览数据
                    script.UpdatePatrolPoints();
                }

                EditorGUI.indentLevel--;
                
                EditorGUILayout.HelpBox("在 Scene 窗口中可以看到红色的巡逻路径线。", MessageType.Info);
            }
            
            // 确保修改能保存
            if (GUI.changed) EditorUtility.SetDirty(script);
        }

        // === 新增：在场景里画线 ===
        private void OnSceneGUI()
        {
            NpcIdleController script = (NpcIdleController)target;

            // 只有开启了移动才画线
            if (script.enableMovement)
            {
                Vector3 start = script.GetStartPos();
                Vector3 end = script.GetEndPos();

                // 画一条线
                Handles.color = Color.red;
                Handles.DrawLine(start, end);

                // 画终点的小球
                Handles.SphereHandleCap(0, end, Quaternion.identity, 0.2f, EventType.Repaint);
                
                // 画起点的圆盘
                Handles.color = Color.yellow;
                Handles.DrawWireDisc(start, Vector3.up, 0.3f);

                // 文字标签
                Handles.Label(end + Vector3.up * 0.5f, $"Patrol End ({script.patrolDistance}m)");
            }
        }
    }
}
#endif