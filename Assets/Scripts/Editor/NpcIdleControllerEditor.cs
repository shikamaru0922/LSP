#if UNITY_EDITOR
using UnityEngine;
using UnityEditor;
using UnityEditor.Animations; // 必须引用这个才能读取 Animator Controller 的内部信息
using System.Collections.Generic;

namespace LSP.Gameplay
{
    [CustomEditor(typeof(NpcIdleController))]
    public class NpcIdleControllerEditor : Editor
    {
        public override void OnInspectorGUI()
        {
            // 1. 获取目标脚本
            NpcIdleController script = (NpcIdleController)target;

            // 2. 获取该物体上的 Animator
            Animator animator = script.GetComponent<Animator>();

            if (animator == null)
            {
                EditorGUILayout.HelpBox("物体上缺少 Animator 组件！", MessageType.Error);
                return;
            }

            // 3. 获取 Animator Controller (状态机文件)
            AnimatorController controller = animator.runtimeAnimatorController as AnimatorController;

            if (controller == null)
            {
                EditorGUILayout.HelpBox("Animator 组件没有绑定 Controller 文件！", MessageType.Warning);
                // 如果没有控制器，就退化成普通的文本框让用户填
                base.OnInspectorGUI();
                return;
            }

            // ============================================
            // 核心逻辑：获取所有动画状态的名字
            // ============================================
            List<string> stateNames = new List<string>();
            
            // 我们默认只读取 Base Layer (第0层) 的状态
            // 如果你有多个层级，可以遍历 controller.layers
            if (controller.layers.Length > 0)
            {
                ChildAnimatorState[] states = controller.layers[0].stateMachine.states;
                foreach (ChildAnimatorState childState in states)
                {
                    stateNames.Add(childState.state.name);
                }
            }

            if (stateNames.Count == 0)
            {
                EditorGUILayout.HelpBox("Animator Controller 里没有任何 State (动画状态)！", MessageType.Warning);
                return;
            }

            // ============================================
            // 绘制下拉菜单 (Popup)
            // ============================================
            
            // 找到当前选中的动画在列表里的索引
            int currentIndex = stateNames.IndexOf(script.stateName);
            if (currentIndex == -1) currentIndex = 0; // 默认选第一个

            // 绘制标题
            EditorGUILayout.LabelField("Select Animation", EditorStyles.boldLabel);

            // 绘制下拉框，返回用户选中的新索引
            int newIndex = EditorGUILayout.Popup("Target State", currentIndex, stateNames.ToArray());

            // 将选中的名字保存回脚本
            string newStateName = stateNames[newIndex];
            if (script.stateName != newStateName)
            {
                Undo.RecordObject(script, "Change NPC Animation"); // 支持撤销 (Ctrl+Z)
                script.stateName = newStateName;
                EditorUtility.SetDirty(script); // 标记为已修改，确保能保存
            }

            // ============================================
            // 绘制脚本里的其他变量 (Random Offset, Loop 等)
            // ============================================
            EditorGUILayout.Space();
            EditorGUILayout.LabelField("Settings", EditorStyles.boldLabel);
            
            // 绘制 randomStartOffset
            bool newRandom = EditorGUILayout.Toggle(new GUIContent("Random Start Offset", "是否随机打乱起始时间？"), script.randomStartOffset);
            if (newRandom != script.randomStartOffset)
            {
                Undo.RecordObject(script, "Toggle Random");
                script.randomStartOffset = newRandom;
            }

            // 绘制 loopAnimation
            bool newLoop = EditorGUILayout.Toggle(new GUIContent("Loop Animation", "动画是否循环？"), script.loopAnimation);
            if (newLoop != script.loopAnimation)
            {
                Undo.RecordObject(script, "Toggle Loop");
                script.loopAnimation = newLoop;
            }
        }
    }
}
#endif