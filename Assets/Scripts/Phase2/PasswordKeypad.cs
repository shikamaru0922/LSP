using UnityEngine;
using TMPro; 
using UnityEngine.Events; 

public class PasswordKeypad : MonoBehaviour
{
    [Header("UI 设置")]
    [Tooltip("整个密码锁UI的根物体，用于显示/隐藏")]
    public GameObject uiPanel; 
    
    [Tooltip("输入框组件")]
    public TMP_InputField inputField; 
    
    [Tooltip("提示文字 (可选)")]
    public TextMeshProUGUI feedbackText;

    [Header("密码逻辑")]
    [Tooltip("正确的密码")]
    public string correctPassword = "1234";

    [Header("按键设置 (新增)")]
    [Tooltip("用于关闭密码锁的按键")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("成功/失败事件 (Excel/Event)")]
    [Tooltip("密码正确时触发这里配置的事件")]
    public UnityEvent onPasswordCorrect;
    
    [Tooltip("密码错误时触发")]
    public UnityEvent onPasswordWrong;

    private bool isSolved = false;
    private bool isOpen = false; // 【新增】用于追踪当前UI是否处于打开状态

    private InteractableSetFlag _interactFlagScript;
    
    void Start()
    {
        _interactFlagScript = GetComponent<InteractableSetFlag>();
        
        // 游戏开始时隐藏密码锁
        if(uiPanel != null) uiPanel.SetActive(false);
        
        // 监听输入框的回车键 (Enter提交)
        inputField.onSubmit.AddListener(delegate { CheckPassword(); });
    }

    // 【新增】每帧检测玩家是否按下退出键
    void Update()
    {
        if (isOpen && Input.GetKeyDown(closeKey))
        {
            Debug.Log("玩家按下了退出键，关闭密码锁");
            CloseKeypad();
        }
    }

    /// <summary>
    /// 供外部调用：打开密码锁UI
    /// </summary>
    public void OpenKeypad()
    {
        if (isSolved) 
        {
            Debug.Log("密码锁已经解开了");
            return; 
        }

        isOpen = true; // 【新增】标记状态为打开
        uiPanel.SetActive(true);
        inputField.text = ""; // 清空上次输入的
        if(feedbackText) feedbackText.text = "";
        
        // 激活输入框焦点，不用鼠标点也能直接打字，打完直接按回车
        inputField.ActivateInputField(); 

        // 暂停游戏或锁定玩家视角 (根据你之前的 Interaction 代码逻辑来)
        // SetPlayerInput(false); 
    }

    /// <summary>
    /// 供外部调用：关闭密码锁UI
    /// </summary>
    public void CloseKeypad()
    {
        isOpen = false; // 【新增】标记状态为关闭
        uiPanel.SetActive(false);
        
        // 【重要】如果你有关闭玩家操作的代码，记得在这里恢复
        // SetPlayerInput(true);
    }

    /// <summary>
    /// 绑定在确认按钮上，也可以通过回车键(onSubmit)触发
    /// </summary>
    public void CheckPassword()
    {
        if (inputField.text == correctPassword)
        {
            // --- 密码正确 ---
            Debug.Log("密码正确！");
            isSolved = true;
            if(feedbackText) feedbackText.text = "ACCESS GRANTED";
            feedbackText.color = Color.green;

            // 触发事件
            onPasswordCorrect.Invoke(); 

            // 延迟一点关闭UI，让玩家看到成功提示
            Invoke("CloseKeypad", 1.0f);
            
            if (_interactFlagScript != null)
            {
                _interactFlagScript.ExecuteLogic("密码锁解开触发");
            }
        }
        else
        {
            // --- 密码错误 ---
            Debug.Log("密码错误！");
            if(feedbackText) feedbackText.text = "INVALID PASSWORD";
            feedbackText.color = Color.red;
            inputField.text = ""; // 清空重输
            
            // 重新强制聚焦，确保玩家可以无缝继续敲键盘试下一个密码
            inputField.ActivateInputField(); 
            
            onPasswordWrong.Invoke();
        }
    }
}