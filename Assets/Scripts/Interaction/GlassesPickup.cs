using UnityEngine;
using System.Collections;
using LSP.Gameplay; 

public class GlassesPickup : MonoBehaviour
{
    [Header("配置")]
    public GameObject worldGlasses;           
    public GameObject cinematicGlasses;       

    [Header("飞行参数")]
    public float flyDuration = 0.5f;          
    public Vector3 endLocalPos = new Vector3(0f, 0f, 0.3f); 
    
    [Header("时间控制")]
    public float waitBeforeDestroy = 0.2f; 

    void Update()
    {
        if (Input.GetKeyDown(KeyCode.Space)) PickUp();
    }

    public void PickUp()
    {
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

        // ============================================================
        // === 阶段二：硬编码查找逻辑 (已修复找不到隐藏物体的问题) ===
        // ============================================================
        Debug.Log("眼镜戴好，开始执行逻辑...");

        // 1. 寻找 PlayerEyeControl (即使挂在隐藏物体上也能找到)
        var eyeControl = FindObjectOfType<PlayerEyeControl>(true); // true 表示包括 Inactive 的物体

        // 2. 寻找 WorldAbnormalTrigger (同样允许 Inactive)
        var worldTrigger = FindObjectOfType<WorldAbnormalTrigger>(true);

        // 3. 【重点修改】寻找 BlinkCanvas (即使它是灰色的/隐藏的也能找到！)
        GameObject blinkCanvas = FindInactiveGameObjectByName("BlinkCanvas");

        // --- 执行动作 ---

        // 动作 A: 唤醒 BlinkCanvas
        if (blinkCanvas != null)
        {
            blinkCanvas.SetActive(true); // 把它从沉睡中唤醒！
        }
        else
        {
            Debug.LogError("还是找不到 'BlinkCanvas'！请确认它在场景里，且名字一个字母都不差。");
        }

        // 动作 B & C: 眨眼
        if (eyeControl != null)
        {
            eyeControl.enabled = true;
            eyeControl.BeginManualBlink();
        }
        else
        {
            Debug.LogError("找不到 PlayerEyeControl！");
        }

        // 动作 D: 异化世界
        if (worldTrigger != null)
        {
            worldTrigger.EnableAbnormalVisuals();
        }
        else
        {
            Debug.LogError("找不到 WorldAbnormalTrigger！");
        }

        // ============================================================

        // === 阶段三：收尾 ===
        yield return new WaitForSeconds(waitBeforeDestroy);
        if(cinematicGlasses != null) Destroy(cinematicGlasses);
        yield return new WaitForSeconds(1.0f);
        Destroy(gameObject);
    }

    // ==========================================================
    //  新增：能够找到隐藏物体的强力查找函数
    // ==========================================================
    GameObject FindInactiveGameObjectByName(string name)
    {
        // 这是一个非常底层的查找方式，能找到内存里所有的物体
        Transform[] objs = Resources.FindObjectsOfTypeAll<Transform>() as Transform[];
        
        foreach (Transform t in objs)
        {
            // 1. 必须是场景里的物体 (过滤掉 Project 里的 Prefab 资源)
            // 2. 名字必须匹配
            if (t.hideFlags == HideFlags.None && t.gameObject.name == name && t.gameObject.scene.IsValid())
            {
                return t.gameObject;
            }
        }
        return null;
    }
}