using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using UnityEngine.UI;
using System.Globalization;
using Newtonsoft.Json.Linq;
using NativeWebSocket;
using System.IO;

public class HyperateDataLogger : MonoBehaviour
{[Header("Hyperate 设置")]
    public string websocketToken = "在此填入你的Token"; 
    public string hyperateID = "internal-testing";
    
    [Header("UI 显示")]
    public Text textBox;

    [Header("数据保存设置")]
    public string folderName = "HeartRateData"; 
    public string fileName = "HR_Log_BgRun"; // 文件名标记为后台运行版

    private WebSocket websocket;
    private string fullFilePath;
    
    private int currentHeartRate = 0; 
    private bool isConnected = false;

    async void Start()
    {
        // ============================================================
        // 【核心修改】强制后台运行
        // 这一行代码保证了当你切出游戏、Alt-Tab 甚至最小化时，
        // Unity 依然会在后台全速运行，不会暂停数据采集。
        // ============================================================
        Application.runInBackground = true;

        // 1. 路径设置
        string directoryPath = Path.Combine(Application.persistentDataPath, folderName);
        if (!Directory.Exists(directoryPath)) Directory.CreateDirectory(directoryPath);

        string fileTimestamp = DateTime.Now.ToString("yyyy-MM-dd_HH-mm-ss");
        fullFilePath = Path.Combine(directoryPath, $"{fileName}_{fileTimestamp}.csv");

        // 2. 写入表头
        string header = "SystemTime,GameTime,HeartRate,IBI_ms\n";
        try 
        {
            File.WriteAllText(fullFilePath, header);
            Debug.Log($"[后台模式] 启动! 数据保存至: {fullFilePath}");
        }
        catch (Exception e) 
        {
            Debug.LogError($"[错误] 文件创建失败: {e.Message}");
        }

        // 3. 启动连接
        await ConnectToHyperate();

        // 4. 开启每秒记录
        StartCoroutine(LogDataRoutine());

        // 5. 开启心跳包
        InvokeRepeating("SendHeartbeat", 1.0f, 20.0f);
    }

    async System.Threading.Tasks.Task ConnectToHyperate()
    {
        websocket = new WebSocket("wss://app.hyperate.io/socket/websocket?token=" + websocketToken);
        
        websocket.OnOpen += () =>
        {
            Debug.Log("Hyperate 连接成功!");
            isConnected = true;
            SendWebSocketMessage();
        };

        websocket.OnError += (e) => Debug.Log("Websocket 错误: " + e);
        
        websocket.OnClose += (e) => 
        {
            Debug.Log("连接断开，尝试重连...");
            isConnected = false;
            Invoke("Reconnect", 3.0f);
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
                    int.TryParse(hrStr, out currentHeartRate);
                    if (textBox != null) textBox.text = hrStr;
                }
            }
            catch { }
        };

        await websocket.Connect();
    }

    void Reconnect()
    {
        ConnectToHyperate();
    }

    void Update()
    {
        // 因为设置了 runInBackground = true，即使切出去了
        // Update 依然会每帧执行，保证消息队列不堵塞
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
        string sysTime = DateTime.Now.ToString("HH:mm:ss.fff");
        float gameTime = Time.realtimeSinceStartup;
        float ibi = (currentHeartRate > 0) ? (60000f / currentHeartRate) : 0;

        string csvLine = $"{sysTime},{gameTime:F2},{currentHeartRate},{ibi:F0}\n";

        try
        {
            File.AppendAllText(fullFilePath, csvLine);
        }
        catch (IOException)
        {
            // 依然提醒不要开Excel
            Debug.LogError("写入失败：请关闭 Excel 文件！");
        }
        catch (Exception) { }
    }

    async void SendWebSocketMessage()
    {
        if (websocket != null && websocket.State == WebSocketState.Open)
        {
            await websocket.SendText("{\"topic\": \"hr:"+hyperateID+"\", \"event\": \"phx_join\", \"payload\": {}, \"ref\": 0}");
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