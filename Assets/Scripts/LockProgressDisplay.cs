using UnityEngine;
using UnityEngine.UI;

public class LockProgressDisplay : MonoBehaviour
{
    [SerializeField] private Slider slider;

    public void SetProgress(float normalized)
    {
        slider.value = Mathf.Clamp01(normalized);
    }

    public void OnCompleted()
    {
        slider.value = 1f;
        // 可选：播放特效、隐藏面板等
    }
}