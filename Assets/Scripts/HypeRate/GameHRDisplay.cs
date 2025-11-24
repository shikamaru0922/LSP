using UnityEngine;
using UnityEngine.UI;

public class GameHRDisplay : MonoBehaviour
{
    [Header("UI 显示")]
    public Text heartRateText; // 游戏里显示心率的 Text

    void Update()
    {
        // 因为 HyperateGlobal 是单例且 DontDestroyOnLoad
        // 所以我们在任何场景都能直接找到它
        if (HyperateGlobal.Instance != null)
        {
            int hr = HyperateGlobal.Instance.CurrentHeartRate;
            
            // 更新 UI
            if(heartRateText != null)
            {
                heartRateText.text = hr.ToString();
            }
        }
    }
}