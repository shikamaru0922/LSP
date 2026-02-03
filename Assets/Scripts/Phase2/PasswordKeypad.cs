using UnityEngine;
using TMPro; // 如果你用的是 TextMeshPro
using UnityEngine.Events; 
// using UnityEngine.UI; // 如果你用的是旧版 InputField，解注这一行

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

    [Header("成功/失败事件 (Excel/Event)")]
    [Tooltip("密码正确时触发这里配置的事件")]
    public UnityEvent onPasswordCorrect;
    
    [Tooltip("密码错误时触发")]
    public UnityEvent onPasswordWrong;

    private bool isSolved = false;

    void Start()
    {
        // 游戏开始时隐藏密码锁
        if(uiPanel != null) uiPanel.SetActive(false);
        
        // 监听输入框的回车键 (可选)
        inputField.onSubmit.AddListener(delegate { CheckPassword(); });
    }

    /// <summary>
    /// 供外部调用：打开密码锁UI
    /// </summary>
    public void OpenKeypad()
    {
        if (isSolved) 
        {
            // 如果已经解开了，可能不需要再输入，或者显示“已开启”
            Debug.Log("密码锁已经解开了");
            return; 
        }

        uiPanel.SetActive(true);
        inputField.text = ""; // 清空上次输入的
        if(feedbackText) feedbackText.text = "";
        
        // 激活输入框焦点，不用鼠标点也能直接打字
        inputField.ActivateInputField(); 

        // 暂停游戏或锁定玩家视角 (根据你之前的 Interaction 代码逻辑来)
        // SetPlayerInput(false); 
    }

    /// <summary>
    /// 供外部调用：关闭密码锁UI
    /// </summary>
    public void CloseKeypad()
    {
        uiPanel.SetActive(false);
        // 恢复玩家控制
        // SetPlayerInput(true);
    }

    /// <summary>
    /// 绑定在确认按钮上
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

            // !!! 这里就是你要求的“用一个事件写出来” !!!
            onPasswordCorrect.Invoke(); 

            // 延迟一点关闭UI，让玩家看到成功提示
            Invoke("CloseKeypad", 1.0f);
        }
        else
        {
            // --- 密码错误 ---
            Debug.Log("密码错误！");
            if(feedbackText) feedbackText.text = "INVALID PASSWORD";
            feedbackText.color = Color.red;
            inputField.text = ""; // 清空重输
            inputField.ActivateInputField(); // 重新聚焦
            
            onPasswordWrong.Invoke();
        }
    }
}