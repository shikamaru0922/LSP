using System;
using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

public class StatuePuzzleManager : MonoBehaviour
{
    [System.Serializable]
    public class StatueData
    {
        [Header("配置")]
        public string name;             // 给自己看的备注，比如 "左边雕像"
        public Transform statueObj;     // 雕像的 Transform
        
        [Range(0, 360)]
        public float targetAngleY;      // 目标 Y 轴角度 (0 ~ 360)
        
        [Tooltip("允许的误差范围 (度)。比如填 5，代表目标角度正负 5 度内都算对")]
        public float tolerance = 5f;

        // 内部状态：当前是否对齐
        [HideInInspector] public bool isAligned = false;
        // 【新增】引用同物体上的交互脚本
        
    }

    [Header("雕像列表 (在这里配置你的两个雕像)")]
    public List<StatueData> statues = new List<StatueData>();

    [Header("事件")]
    [Tooltip("当所有雕像都旋转正确时触发")]
    public UnityEvent onPuzzleSolved;

    [Header("调试")]
    public bool showDebugLogs = true;

    [SerializeField]
    private bool _hasSolved = false;
    private InteractableSetFlag _interactFlagScript;

    private void Start()
    {
        // 【新增】自动获取身上的 InteractableSetFlag 脚本
        _interactFlagScript = GetComponent<InteractableSetFlag>();
    }

    private void Update()
    {
        // 如果已经解开了，就不再检测，节省性能
        if (_hasSolved) return;

        CheckAllStatues();
    }

    private void CheckAllStatues()
    {
        bool allCorrect = true;

        foreach (var statue in statues)
        {
            if (statue.statueObj == null) continue;

            // 1. 获取当前 Y 轴角度
            float currentY = statue.statueObj.localEulerAngles.y;

            // 2. 计算角度差 (关键点：使用 DeltaAngle 处理 0度和360度的衔接问题)
            // Mathf.DeltaAngle(350, 10) 会返回 20，而不是 340，这对旋转解谜非常重要
            float angleDifference = Mathf.Abs(Mathf.DeltaAngle(currentY, statue.targetAngleY));

            // 3. 判断是否在误差范围内
            bool isMatch = angleDifference <= statue.tolerance;
            
            statue.isAligned = isMatch;

            if (!isMatch)
            {
                allCorrect = false;
                // 如果你想看实时调试，可以打开下面这行
                // if(showDebugLogs) Debug.Log($"{statue.name} 角度不对: 当前 {currentY:F0}, 目标 {statue.targetAngleY}, 差值 {angleDifference:F0}");
            }
        }

        // 4. 如果全部正确，并且之前没触发过
        if (allCorrect && !_hasSolved)
        {
            _hasSolved = true;
            Debug.Log("<color=green>【解谜成功】所有雕像归位！</color>");
            onPuzzleSolved?.Invoke();
            
            if (_interactFlagScript != null)
            {
                // 调用它的公共方法，传入来源字符串方便调试
                // 这会自动处理：SetFlag、播放成功音效、显示关联物体(objectToShow) 等
                _interactFlagScript.ExecuteLogic("视线移开消失");
            
                // 注意：InteractableSetFlag 内部可能会隐藏物体(hideOnInteract=true)
                // 但为了双重保险，我们检查一下，如果它没隐藏，我们这里强制隐藏
               
            }
            else
            {
                // 如果没有挂那个交互脚本，就直接消失
                gameObject.SetActive(false);
            }
        }
    }
    
    // 供外部调用（比如你重置关卡时）
    public void ResetPuzzle()
    {
        _hasSolved = false;
    }
}