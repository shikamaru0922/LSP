using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI;
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using System.IO;

public class HyperateGlobal : MonoBehaviour
{
    // 单例模式
    public static HyperateGlobal Instance { get; private set; }

    [Header("默认设置")]
    public string websocketToken = "在此填入你的Token"; 
    public string currentDeviceID = ""; 
    
    [Header("数据保存")]
    public string folderName = "HeartRateData"; 
    public string fileName = "GameSession_Log"; 

    // 公开变量，供 UI 读取
    public int CurrentHeartRate { get; private set; } = 0;
    public bool IsConnected { get; private set; } = false;

    // 公开文件路径，方便其他脚本读取
    public string FullFilePath => fullFilePath;

    private WebSocket websocket;
    private string fullFilePath;
    private bool isLogging = false;

    void Awake()
    {
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject);
            Application.runInBackground = true;
        }
        else
        {
            Destroy(gameObject);
        }
    }

    void Start()
    {
        // 等待 LoginUI 调用 Connect()
    }

    public async void Connect(string deviceID)
    {
        if (IsConnected) return;

        currentDeviceID = deviceID;
        Debug.Log($"准备连接设备: {currentDeviceID}");

        // 1. 准备文件
        SetupFile();

        // 2. 连接 WebSocket
        websocket = new WebSocket("wss://app.hyperate.io/socket/websocket?token=" + websocketToken);

        websocket.OnOpen += () =>
        {
            Debug.Log("Websocket 连接建立!");
            SendJoinMessage();
        };

        websocket.OnMessage += (bytes) =>
        {
            var message = System.Text.Encoding.UTF8.GetString(bytes);
            try 
            {
                var msg = JObject.Parse(message);
                
                if (msg["event"] != null && msg["event"].ToString() == "hr_update")
                {
                    string hrStr = (string)msg["payload"]["hr"];
                    int.TryParse(hrStr, out int hr);
                    
                    CurrentHeartRate = hr;
                    
                    if (!IsConnected && hr > 0)
                    {
                        IsConnected = true;
                        Debug.Log("收到有效心率，登录成功！");
                    }
                }
            }
            catch {}
        };

        websocket.OnError += (e) => Debug.Log("Error: " + e);
        websocket.OnClose += (e) => 
        {
            Debug.Log("连接断开");
            IsConnected = false;
        };

        // 3. 先启动数据记录协程（不要放在 await 后面！）
        if (!isLogging)
        {
            StartCoroutine(LogDataRoutine());
            InvokeRepeating("SendHeartbeat", 1.0f, 20.0f);
            isLogging = true;
            Debug.Log("<color=yellow>数据记录协程已启动</color>");
        }

        await websocket.Connect();
    }

    void SetupFile()
    {
        // ========== 保存到桌面 ==========
        string basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.Desktop);
        
        // 备选方案（取消注释即可使用）：
        // 保存到"我的文档"
        // string basePath = System.Environment.GetFolderPath(System.Environment.SpecialFolder.MyDocuments);
        
        // 保存到项目根目录（仅编辑器有效）
        // string basePath = Application.dataPath.Replace("/Assets", "");
        // ================================

        string directoryPath = Path.Combine(basePath, folderName);
        if (!Directory.Exists(directoryPath)) 
            Directory.CreateDirectory(directoryPath);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        fullFilePath = Path.Combine(directoryPath, $"{fileName}_{currentDeviceID}_{timestamp}.csv");

        string header = "SystemTime,GameTime,HeartRate,IBI_ms\n";
        try 
        {
            File.WriteAllText(fullFilePath, header);
            
            // 打印完整路径
            Debug.Log($"<color=green>✓ 数据文件已创建: {fullFilePath}</color>");
        }
        catch (Exception e) 
        {
            Debug.LogError("文件创建失败: " + e.Message);
        }
    }

    void Update()
    {
        #if !UNITY_WEBGL || UNITY_EDITOR
            if(websocket != null) websocket.DispatchMessageQueue();
        #endif
    }

    IEnumerator LogDataRoutine()
    {
        while (true)
        {
            yield return new WaitForSeconds(1.0f);
            WriteDataToFile();
        }
    }

    void WriteDataToFile()
    {
        // 调试：检查路径是否有效
        if (string.IsNullOrEmpty(fullFilePath)) 
        {
            Debug.LogWarning("文件路径为空，无法写入！");
            return;
        }

        string sysTime = DateTime.Now.ToString("HH:mm:ss.fff");
        float gameTime = Time.realtimeSinceStartup;
        float ibi = (CurrentHeartRate > 0) ? (60000f / CurrentHeartRate) : 0;

        string csvLine = $"{sysTime},{gameTime:F2},{CurrentHeartRate},{ibi:F0}\n";

        try
        {
            File.AppendAllText(fullFilePath, csvLine);
            // 调试：每次写入都打印（确认工作后可以注释掉）
            Debug.Log($"写入数据: HR={CurrentHeartRate}, 路径={fullFilePath}");
        }
        catch (Exception e)
        {
            Debug.LogError($"写入失败: {e.Message}");
        }
    }

    async void SendJoinMessage()
    {
        if (websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("{\"topic\": \"hr:"+currentDeviceID+"\", \"event\": \"phx_join\", \"payload\": {}, \"ref\": 0}");
        }
    }

    async void SendHeartbeat()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("{\"topic\": \"phoenix\",\"event\": \"heartbeat\",\"payload\": {},\"ref\": 0}");
        }
    }

    private async void OnApplicationQuit()
    {
        if(websocket != null) await websocket.Close();
    }
}