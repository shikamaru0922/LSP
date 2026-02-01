using UnityEngine;

// 这一行非常重要！它让你可以在 Project 窗口右键创建物品
[CreateAssetMenu(fileName = "New Item", menuName = "Inventory/Item Data")]
public class ItemData : ScriptableObject
{
    [Header("物品基础信息")]
    public string itemName;       // 物品名字
    
    [TextArea] 
    public string description;    // 物品描述 (比如显示在检视面板里)

    public Sprite icon;           // 物品图标 (UI上显示的图片)

    [Header("模型引用 (可选)")]
    public GameObject prefab;     // 扔在地上时长什么样 (如果不涉及扔东西可以不填)

    // 你可以在这里加更多变量，比如 isStackable (是否可堆叠) 等
}