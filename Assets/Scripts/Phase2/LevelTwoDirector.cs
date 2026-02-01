using UnityEngine;
using UnityEngine.Events;
using System.Collections.Generic;

[System.Serializable]
public class GameFlag
{
    public string flagName;       // 布尔值的名字 (比如 "HasArm")
    public bool currentValue;     // 当前的值 (True/False)
    
    [Header("当变成 True 时触发")]
    public UnityEvent onTrue;     // 比如: 播放获得音效

    [Header("当变成 False 时触发")]
    public UnityEvent onFalse;    // (一般用得少，备用)
}

public class LevelTwoDirector : MonoBehaviour
{
    public static LevelTwoDirector Instance;

    [Header("===== 所有的游戏开关 (Flags) =====")]
    [Tooltip("在这里添加你所有的布尔值，比如 HasArm, HasKeyA...")]
    public List<GameFlag> flags = new List<GameFlag>();

    // 字典：用来快速查找，代码里查起来快
    private Dictionary<string, GameFlag> flagMap = new Dictionary<string, GameFlag>();

    private void Awake()
    {
        if (Instance == null) Instance = this;
        else Destroy(gameObject);

        // 初始化字典
        foreach (var flag in flags)
        {
            if (!flagMap.ContainsKey(flag.flagName))
            {
                flagMap.Add(flag.flagName, flag);
            }
        }
    }

    // =========================================================
    //  【核心功能 1】设置布尔值 (SetBool)
    //  比如: SetFlag("HasArm", true);
    // =========================================================
    public void SetFlag(string name, bool value)
    {
        if (flagMap.TryGetValue(name, out GameFlag flag))
        {
            // 如果值没变，就不重复触发 (防止每一帧都触发)
            if (flag.currentValue == value) return;

            flag.currentValue = value;
            Debug.Log($"<color=cyan>【导演】开关更新: {name} = {value}</color>");

            // 触发对应的事件
            if (value == true) flag.onTrue?.Invoke();
            else flag.onFalse?.Invoke();
        }
        else
        {
            Debug.LogError($"【错误】找不到名为 '{name}' 的开关！请在 Inspector 里添加。");
        }
    }

    // 为了让 UnityEvent (比如按钮) 能调用，提供一个只设为 True 的简便方法
    public void SetFlagTrue(string name)
    {
        SetFlag(name, true);
    }

    // =========================================================
    //  【核心功能 2】检查布尔值 (GetBool)
    //  比如: if (GetFlag("HasArm")) ...
    // =========================================================
    public bool GetFlag(string name)
    {
        if (flagMap.TryGetValue(name, out GameFlag flag))
        {
            return flag.currentValue;
        }
        
        // 如果找不到这个开关，默认返回 false，并报错提醒你
        Debug.LogWarning($"【警告】试图检查不存在的开关: {name}");
        return false;
    }
}