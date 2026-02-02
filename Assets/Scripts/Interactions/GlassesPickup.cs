using UnityEngine;
using System.Collections;
using UnityEngine.Events; // 【重要】引入事件命名空间
using LSP.Gameplay;
using LSP.Gameplay.Interactions;

public class GlassesPickup : MonoBehaviour, IInteractable
{
    [Header("配置")]
    public GameObject worldGlasses;           
    public GameObject cinematicGlasses;       

    [Header("飞行参数")]
    public float flyDuration = 0.5f;          
    public Vector3 endLocalPos = new Vector3(0f, 0f, 0.3f); 
    
    [Header("时间控制")]
    public float waitBeforeDestroy = 0.2f; 

    // ==========================================================
    //  【新增】这里定义一个事件，你在 Inspector 面板里把门拖进来
    // ==========================================================
    [Header("事件触发")]
    [Tooltip("当开始拾取眼镜时触发（请在这里绑定 关门 Close 方法）")]
    public UnityEvent onPickupStart; 

    public bool CanInteract(PlayerInteractionController controller)
    {
        return true;
    }

    public void Interact(PlayerInteractionController controller)
    {
        PickUp();
    }

    public void PickUp()
    {
        // ==========================================================
        //  【新增】执行事件：就在玩家点击的一瞬间，关门！
        // ==========================================================
        if (onPickupStart != null)
        {
            Debug.Log("【眼镜】触发拾取事件 (如: 关门)");
            onPickupStart.Invoke();
        }

        if (worldGlasses != null && cinematicGlasses != null)
        {
            cinematicGlasses.transform.position = worldGlasses.transform.position;
            cinematicGlasses.transform.rotation = worldGlasses.transform.rotation;
            worldGlasses.SetActive(false); 
            cinematicGlasses.SetActive(true);
            cinematicGlasses.transform.SetParent(Camera.main.transform);

            StartCoroutine(SequenceRoutine());
        }
    }

    IEnumerator SequenceRoutine()
    {
        // === 阶段一：飞行 ===
        float timer = 0f;
        Vector3 startPos = cinematicGlasses.transform.localPosition;
        Quaternion startRot = cinematicGlasses.transform.localRotation;

        while (timer < flyDuration)
        {
            timer += Time.deltaTime;
            float progress = timer / flyDuration; 
            float easeProgress = progress * progress; 

            cinematicGlasses.transform.localPosition = Vector3.Lerp(startPos, endLocalPos, easeProgress);
            cinematicGlasses.transform.localRotation = Quaternion.Lerp(startRot, Quaternion.Euler(Vector3.zero), easeProgress);
            yield return null;
        }
        
        cinematicGlasses.transform.localPosition = endLocalPos;

        // === 阶段二：查找逻辑 ===
        Debug.Log("眼镜戴好，开始执行逻辑...");

        var eyeControl = FindObjectOfType<PlayerEyeControl>(true);
        var worldTrigger = FindObjectOfType<WorldAbnormalTrigger>(true);
        GameObject blinkCanvas = FindInactiveGameObjectByName("BlinkCanvas");

        // --- 执行动作 ---
        if (blinkCanvas != null) blinkCanvas.SetActive(true);
        else Debug.LogError("找不到 'BlinkCanvas'！");

        if (eyeControl != null)
        {
            eyeControl.enabled = true;
            eyeControl.BeginManualBlink();
        }
        else Debug.LogError("找不到 PlayerEyeControl！");

        if (worldTrigger != null) worldTrigger.EnableAbnormalVisuals();
        else Debug.LogError("找不到 WorldAbnormalTrigger！");

        // === 阶段三：收尾 ===
        yield return new WaitForSeconds(waitBeforeDestroy);
        if(cinematicGlasses != null) Destroy(cinematicGlasses);
        yield return new WaitForSeconds(1.0f);
        Destroy(gameObject);
    }

    GameObject FindInactiveGameObjectByName(string name)
    {
        Transform[] objs = Resources.FindObjectsOfTypeAll<Transform>() as Transform[];
        foreach (Transform t in objs)
        {
            if (t.hideFlags == HideFlags.None && t.gameObject.name == name && t.gameObject.scene.IsValid())
            {
                return t.gameObject;
            }
        }
        return null;
    }
}