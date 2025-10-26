# 重力机关使用指南

本文档介绍如何在场景中配置 `GravitySwitch` 与 `GravitySwitchGroup` 组件来制作需要多个角色协作的重力机关/解谜玩法。

## 前置条件
- 已导入本仓库中的脚本。
- 场景中存在可供玩家或怪物站立的地面模型。

## 配置单个重力开关 `GravitySwitch`
1. **添加触发器碰撞体**：
   - 选择你的地面或机关模型，在 `Inspector` 中点击 `Add Component`。
   - 添加一个合适的 `Collider`（例如 `Box Collider`）。
   - `GravitySwitch` 会自动把碰撞体设置为 `Is Trigger = true`，无需手动勾选。
2. **挂载脚本**：
   - 在同一对象上点击 `Add Component` 并搜索 `Gravity Switch`，添加脚本。
3. **设置事件**：
   - `State Changed Event`：当按压状态发生变化时触发，携带 `bool` 参数表示当前是否被按下。
   - `On Pressed Event`：任意有效碰撞体第一次踩上开关时触发，只要有玩家/怪物持续站在上面就会保持按下状态。
   - `On Released Event`：所有有效碰撞体离开或失效（例如对象被禁用/销毁）后触发，开关会立即回弹为未按下。
   - 可在 Inspector 中直接拖拽其他对象的方法到这些事件槽位，或在代码中通过 `StateChangedEvent` / `OnPressedEvent` / `OnReleasedEvent` 访问。
4. **检测对象**：
   - 开关默认只对挂有 `PlayerStateController` 或 `MonsterController`（或其子对象）的碰撞体生效。
   - 如需扩展，可继承 `GravitySwitch` 并重写 `IsValidActivator` 方法。

## 多个开关串联：`GravitySwitchGroup`
1. 创建一个空物体，添加 `Gravity Switch Group` 组件。
2. 在 `Switches` 数组中将需要串联的 `GravitySwitch` 拖入。
3. 设置 `Required Active Switches`：
   - 设为 `0`（默认）时表示所有开关都必须被按下。
   - 设为 `N (>0)` 时表示只要有 `N` 个开关被按下就会激活。
4. 配置事件：
   - `State Changed Event`：携带布尔值指示当前组是否激活。
   - `On Activated Event` / `On Deactivated Event`：在组激活或关闭时触发。

## 示例：控制重力方向
```csharp
using UnityEngine;
using LSP.Gameplay.Interactions;

public class GravityFlipper : MonoBehaviour
{
    [SerializeField] private GravitySwitchGroup gravitySwitchGroup;
    [SerializeField] private Vector3 activeGravity = new Vector3(0, 9.81f, 0f);
    [SerializeField] private Vector3 inactiveGravity = new Vector3(0, -9.81f, 0f);

    private void OnEnable()
    {
        if (gravitySwitchGroup != null)
        {
            gravitySwitchGroup.StateChangedEvent.AddListener(OnGroupStateChanged);
        }
    }

    private void OnDisable()
    {
        if (gravitySwitchGroup != null)
        {
            gravitySwitchGroup.StateChangedEvent.RemoveListener(OnGroupStateChanged);
        }
    }

    private void OnGroupStateChanged(bool isActive)
    {
        Physics.gravity = isActive ? activeGravity : inactiveGravity;
    }
}
```
> 将该脚本挂在场景任意物体上，把 `Gravity Switch Group` 引用拖入 Inspector。激活后即改变世界重力方向，可根据需要自定义逻辑。

## 调试提示
- 如果开关无法触发，确认角色的碰撞体是否在触发器层级中，并且对象上存在 `PlayerStateController` 或 `MonsterController` 组件。
- 使用 `Gizmos` 视图检查触发器的大小是否覆盖玩家/怪物脚底。
- 若多个开关串联仍无法激活，确认 `Required Active Switches` 设置正确，同时检查是否有重复引用造成忽略。

