using UnityEngine;

#if UNITY_EDITOR
[ExecuteInEditMode]
#endif
public class MaterialReplacer : MonoBehaviour
{
    [Header("要替换成的材质（把你的材质拖到这里）")]
    public Material newMaterial;

    [Header("选项")]
    [Tooltip("是否也替换 SkinnedMeshRenderer 的材质")]
    public bool includeSkinnedMeshRenderer = false;

    [Tooltip("是否包含未激活（Inactive）的对象")]
    public bool includeInactive = true;

    // 这个脚本只作为“承载参数”的组件；真正的按钮在自定义 Inspector 里。
}