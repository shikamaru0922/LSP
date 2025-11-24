using System.Collections;
using UnityEngine;
using UnityEngine.Rendering;
using UnityEngine.UI;
using UnityEngine.SceneManagement; // 需要这个来切换场景

public class LoginUIController : MonoBehaviour
{
    [Header("UI 组件")]
    public InputField inputDeviceID; // 输入ID的地方
    public Button loginButton;       // 登录按钮
    public Text statusText;          // 显示状态：比如"正在连接..."
    public Text hrPreviewText;       // 显示心率预览

    public GameObject loginPanel;
    [Header("游戏场景名字")]
    //
    private bool isWaitingForConnection = false;

    void Start()
    {
        // 初始化 UI
        statusText.text = "equipment ID";
        hrPreviewText.text = "--";
        loginButton.onClick.AddListener(OnLoginClicked);
    }

    void OnLoginClicked()
    {
        string id = inputDeviceID.text;
        if (string.IsNullOrEmpty(id))
        {
            statusText.text = "ID can't be empty";
            return;
        }

        // 1. 调用全局管理器开始连接
        statusText.text = "Connecting...";
        statusText.color = Color.yellow;
        loginButton.interactable = false; // 禁用按钮防止重复点

        HyperateGlobal.Instance.Connect(id);
        
        // 2. 开始等待心率数据返回
        isWaitingForConnection = true;
    }

    void Update()
    {
        // 如果正在等待连接
        if (isWaitingForConnection)
        {
            // 检查全局管理器是否收到有效心率
            if (HyperateGlobal.Instance.IsConnected)
            {
                int hr = HyperateGlobal.Instance.CurrentHeartRate;
                
                // 显示心率预览
                hrPreviewText.text = hr.ToString() + " BPM";

                // 登录成功！
                statusText.text = "You Can Enter Game";
                statusText.color = Color.green;

                // 3. 延迟一小会儿后跳转场景
                //StartCoroutine(LoadGameSceneDelay());
                
                loginPanel.SetActive(false);
                isWaitingForConnection = false; // 停止检查
            }
            else
            {
                // 还可以做个超时检测，比如等待超过10秒提示失败
            }
        }
    }


}