using LSP.Gameplay;
using UnityEngine;
using UnityEngine.UI; // 如果你使用的是 TextMeshPro，则需要这个命名空间

public class HeartRateDisplay : MonoBehaviour
{
    [Header("References")]
    [Tooltip("场景中的心率运动控制器")]
    [SerializeField] private HeartRateMovementController heartRateController;

    [Tooltip("显示基准心率的 Text 或 TextMeshPro 组件")]
    [SerializeField] private Text restingHRText; 
    // 注意：如果你使用的是 Unity 旧版 Text 组件，请将类型改为 UnityEngine.UI.Text

    private bool isHRShown = false;

    private void Start()
    {
        // 尝试自动查找组件，如果Inspector中没有设置
        if (heartRateController == null)
        {
            heartRateController = FindObjectOfType<HeartRateMovementController>();
        }

        // 确保引用不为空
        if (heartRateController == null || restingHRText == null)
        {
            Debug.LogError("HeartRateController 或 RestingHRText 引用未设置!");
            enabled = false; // 禁用脚本以防错误
            return;
        }

        // 初始显示一个等待状态
        restingHRText.text = "Resting HR: Calibrating...";
    }

    private void Update()
    {
        // 只有当校准完成或手动设置后才显示最终结果
        if (!heartRateController.IsCalibrating && !isHRShown)
        {
            // 获取并格式化基准心率
            float restingHR = heartRateController.RestingHeartRate;
            
            // 确保心率值有效
            if (restingHR > 0f)
            {
                // 将浮点数格式化为整数显示
                restingHRText.text = $"Resting HR: {Mathf.RoundToInt(restingHR)} BPM";
                isHRShown = true;
            }
        }
        else if (heartRateController.IsCalibrating)
        {
            // 如果仍在校准，保持显示校准状态
            restingHRText.text = "Resting HR: Calibrating...";
            isHRShown = false;
        }
        
        // 可选：你也可以在这里显示实时的 CurrentHeartRate
        // if (restingHRText != null)
        // {
        //     int currentHR = heartRateController.GetCurrentHeartRate(); 
        //     restingHRText.text = $"Current HR: {currentHR} | Resting HR: {Mathf.RoundToInt(heartRateController.RestingHeartRate)}";
        // }
    }
}