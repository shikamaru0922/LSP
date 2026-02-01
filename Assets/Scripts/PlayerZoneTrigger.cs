using UnityEngine;

public class PlayerZoneTrigger : MonoBehaviour
{
    [Tooltip("请把场景里的 Director 物体拖进来")]
    [SerializeField] private LevelOneFlowDirector director;

    private void OnTriggerEnter(Collider other)
    {
        // 假设玩家身上有 Player标签，或者你可以检测 CharacterController
        if (other.CompareTag("Player"))
        {
            if (director != null)
            {
                // 告诉导演：玩家进圈套了！
                director.SetPlayerInTrapZone(true);
                Debug.Log("【触发器】玩家进入陷阱区域！");
            }
        }
    }

    private void OnTriggerExit(Collider other)
    {
        if (other.CompareTag("Player"))
        {
            if (director != null)
            {
                // 玩家退出来了 (可选：如果你希望他退出后就不能触发，保留这句)
                director.SetPlayerInTrapZone(false);
            }
        }
    }
}