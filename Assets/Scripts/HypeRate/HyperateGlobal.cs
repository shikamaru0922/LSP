using System;
using System.Collections;
using UnityEngine;
using UnityEngine.UI; // 如果需要引用UI
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using System.IO;

public class HyperateGlobal : MonoBehaviour
{
    // 单例模式：让其他脚本方便找到我
    public static HyperateGlobal Instance { get; private set; }

    [Header("默认设置")]
    public string websocketToken = "在此填入你的Token"; 
    // deviceID 现在不写死，而是通过 UI 输入
    public string currentDeviceID = ""; 
    
    [Header("数据保存")]
    public string folderName = "HeartRateData";
    public string fileName = "GameSession_Log";

    [Header("路径可见 (只读)")]
    [SerializeField] private string lastLogDirectory = string.Empty;
    [SerializeField] private string lastLogFilePath = string.Empty;

    // 公开变量，供 UI 读取
    public int CurrentHeartRate { get; private set; } = 0;
    public bool IsConnected { get; private set; } = false;

    private WebSocket websocket;
    private bool isLogging = false;

    public string LogDirectory => Path.Combine(Application.persistentDataPath, folderName);
    public string LastLogFilePath => lastLogFilePath;

    void Awake()
    {
        // 保证全场只有一个 HyperateGlobal
        if (Instance == null)
        {
            Instance = this;
            DontDestroyOnLoad(gameObject); // 【关键】切换场景时，我不会被销毁
            Application.runInBackground = true; // 【关键】后台运行
        }
        else
        {
            Destroy(gameObject); // 如果已经有一个我了，销毁新的这个
        }
    }

    void Start()
    {
        // 这里不自动连接，等待 LoginUI 调用 Connect()
    }

    // --- 由 UI 调用的连接函数 ---
    public async void Connect(string deviceID)
    {
        if (IsConnected) return; // 防止重复点

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
                
                // 收到心率更新
                if (msg["event"] != null && msg["event"].ToString() == "hr_update")
                {
                    string hrStr = (string)msg["payload"]["hr"];
                    int.TryParse(hrStr, out int hr);
                    
                    // 更新核心数据
                    CurrentHeartRate = hr;
                    
                    // 如果收到有效数据，标记为连接成功
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

        await websocket.Connect();

        // 3. 开启数据记录 (1Hz)
        if (!isLogging)
        {
            StartCoroutine(LogDataRoutine());
            InvokeRepeating("SendHeartbeat", 1.0f, 20.0f);
            isLogging = true;
        }
    }

    void SetupFile()
    {
        string directoryPath = LogDirectory;
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        string timestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        lastLogDirectory = directoryPath;
        lastLogFilePath = Path.Combine(directoryPath, $"{fileName}_{currentDeviceID}_{timestamp}.csv");

        string header = "SystemTime,GameTime,HeartRate,IBI_ms\n";
        try
        {
            File.WriteAllText(lastLogFilePath, header);
            Debug.Log($"心率日志将写入: {lastLogFilePath}");
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
            yield return new WaitForSeconds(1.0f); // 1秒记录一次
            WriteDataToFile();
        }
    }

    void WriteDataToFile()
    {
        if (string.IsNullOrEmpty(lastLogFilePath)) return;

        string sysTime = DateTime.Now.ToString("HH:mm:ss.fff");
        float gameTime = Time.realtimeSinceStartup;
        float ibi = (CurrentHeartRate > 0) ? (60000f / CurrentHeartRate) : 0;

        string csvLine = $"{sysTime},{gameTime:F2},{CurrentHeartRate},{ibi:F0}\n";

        try
        {
            File.AppendAllText(lastLogFilePath, csvLine);
        }
        catch {}
    }

    public void OpenLogFolder()
    {
        string directoryPath = LogDirectory;
        if (!Directory.Exists(directoryPath))
        {
            Directory.CreateDirectory(directoryPath);
        }

        Debug.Log($"日志目录: {directoryPath}");

        #if UNITY_STANDALONE || UNITY_EDITOR
            Application.OpenURL("file://" + directoryPath);
        #endif
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