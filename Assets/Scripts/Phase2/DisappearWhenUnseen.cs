using LSP.Gameplay;
using UnityEngine;
using UnityEngine.Events;

public class DisappearWhenUnseen : MonoBehaviour
{
    [Header("设置")]
    [Tooltip("视线移开后，延迟多久消失？")]
    public float vanishDelay = 0.5f;

    [Header("调试信息 (只读)")]
    [SerializeField] private bool _hasBeenSeen = false;
    [SerializeField] private float _timer = 0f;

    // 缓存引用
    private PlayerVision _playerVision;
    private Collider _myCollider;
    
    // 【新增】引用同物体上的交互脚本
    private InteractableSetFlag _interactFlagScript;
    
    public UnityEvent OnDisappear;

    private void Start()
    {
        _myCollider = GetComponent<Collider>();
        if (_myCollider == null)
        {
            Debug.LogError($"【错误】{gameObject.name} 需要一个 Collider 才能被 PlayerVision 检测！");
            enabled = false;
            return;
        }

        _playerVision = FindObjectOfType<PlayerVision>();
        if (_playerVision == null)
        {
            Debug.LogError("【错误】场景里找不到 PlayerVision！");
            enabled = false;
        }

        // 【新增】自动获取身上的 InteractableSetFlag 脚本
        _interactFlagScript = GetComponent<InteractableSetFlag>();
    }

    private void Update()
    {
        if (_playerVision == null || _myCollider == null) return;

        bool isCurrentlyVisible = _playerVision.CanSee(_myCollider);

        if (isCurrentlyVisible)
        {
            // A. 正在看：标记已发现，重置计时
            if (!_hasBeenSeen)
            {
                _hasBeenSeen = true;
                Debug.Log($"【{gameObject.name}】被看见了！");
            }
            _timer = 0f;
        }
        else
        {
            // B. 没在看：必须先被发现过，才开始消失倒计时
            if (_hasBeenSeen)
            {
                _timer += Time.deltaTime;

                if (_timer >= vanishDelay)
                {
                    PerformDisappear();
                }
            }
        }
    }

    private void PerformDisappear()
    {
        Debug.Log($"【{gameObject.name}】玩家移开视线，触发消失逻辑。");
        
        // 1. 调用 UnityEvent (保留灵活性)
        OnDisappear?.Invoke();

        // 2. 【核心修改】调用 InteractableSetFlag 的逻辑
        // 这相当于告诉系统：“虽然是消失了，但这算作玩家完成了一次交互”
        if (_interactFlagScript != null)
        {
            // 调用它的公共方法，传入来源字符串方便调试
            // 这会自动处理：SetFlag、播放成功音效、显示关联物体(objectToShow) 等
            _interactFlagScript.ExecuteLogic("视线移开消失");
            
            // 注意：InteractableSetFlag 内部可能会隐藏物体(hideOnInteract=true)
            // 但为了双重保险，我们检查一下，如果它没隐藏，我们这里强制隐藏
            if (gameObject.activeSelf)
            {
                gameObject.SetActive(false);
            }
        }
        else
        {
            // 如果没有挂那个交互脚本，就直接消失
            gameObject.SetActive(false);
        }
    }
}