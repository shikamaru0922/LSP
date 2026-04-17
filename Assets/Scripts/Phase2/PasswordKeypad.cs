using UnityEngine;
using TMPro; 
using UnityEngine.Events; 
using LSP.Gameplay;
using StarterAssets;

public class PasswordKeypad : MonoBehaviour
{
    [Header("UI 设置")]
    [Tooltip("整个密码锁UI的根物体，用于显示/隐藏")]
    public GameObject uiPanel; 
    
    [Tooltip("输入框组件")]
    public TMP_InputField inputField; 
    
    [Tooltip("提示文字 (可选)")]
    public TextMeshProUGUI feedbackText;

    [Header("密码逻辑")]
    [Tooltip("正确的密码")]
    public string correctPassword = "1234";

    [Header("按键设置 (新增)")]
    [Tooltip("用于关闭密码锁的按键")]
    public KeyCode closeKey = KeyCode.Escape;

    [Header("成功/失败事件 (Excel/Event)")]
    [Tooltip("密码正确时触发这里配置的事件")]
    public UnityEvent onPasswordCorrect;
    
    [Tooltip("密码错误时触发")]
    public UnityEvent onPasswordWrong;

    [Header("依赖 (可选)")]
    [Tooltip("打开密码 UI 时用于暂停交互的控制器，不填会自动查找")]
    [SerializeField] private PlayerInteractionController interactionController;

    [Tooltip("StarterAssets 输入组件，不填会自动查找")]
    [SerializeField] private StarterAssetsInputs starterInputs;

    private bool isSolved = false;
    private bool isOpen = false; // 【新增】用于追踪当前UI是否处于打开状态

    private InteractableSetFlag _interactFlagScript;
    private bool cursorOverrideActive;
    private CursorLockMode cachedCursorLockMode;
    private bool cachedCursorVisible;
    private bool starterInputOverrideActive;
    private bool cachedCursorLocked;
    private bool cachedCursorInputForLook;
    private bool cachedStarterInputsEnabled;

    /// <summary>
    /// 当前密码 UI 是否处于打开状态。
    /// </summary>
    public bool IsOpen => isOpen;

    /// <summary>
    /// 密码锁是否已经被解开。
    /// </summary>
    public bool IsSolved => isSolved;
    
    void Start()
    {
        ResolveDependenciesIfNeeded();
        _interactFlagScript = GetComponent<InteractableSetFlag>();

        // 如果此脚本在首次交互时才被激活，OpenKeypad 可能先于 Start 调用。
        // 这种情况下不能在 Start 再次强制隐藏，否则会出现“第一次按键打不开、第二次才打开”。
        bool openedBeforeStart = isOpen || (uiPanel != null && uiPanel.activeSelf);
        if (uiPanel != null && !openedBeforeStart)
        {
            uiPanel.SetActive(false);
        }

        isOpen = openedBeforeStart;
        if (isOpen)
        {
            SetUiInputState(true);
        }
        
        // 监听输入框的回车键 (Enter提交)
        if (inputField != null)
        {
            inputField.onSubmit.AddListener(OnInputSubmit);

            if (isOpen)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
        }
    }

    // 【新增】每帧检测玩家是否按下退出键
    void Update()
    {
        if (isOpen && Input.GetKeyDown(closeKey))
        {
            Debug.Log("玩家按下了退出键，关闭密码锁");
            CloseKeypad();
        }
    }

    /// <summary>
    /// 供外部调用：打开密码锁UI
    /// </summary>
    public void OpenKeypad()
    {
        if (isSolved) 
        {
            Debug.Log("密码锁已经解开了");
            return; 
        }
        
        if (isOpen)
        {
            return;
        }

        ResolveDependenciesIfNeeded();
        isOpen = true; // 【新增】标记状态为打开
        SetUiInputState(true);
        
        if (uiPanel != null)
        {
            uiPanel.SetActive(true);
        }
        
        if (inputField != null)
        {
            inputField.text = ""; // 清空上次输入的
            inputField.Select();
            inputField.ActivateInputField();
        }
        
        if(feedbackText) feedbackText.text = "";
        
        if (IsInvoking(nameof(CloseKeypad)))
        {
            CancelInvoke(nameof(CloseKeypad));
        }
    }

    /// <summary>
    /// 供外部调用：关闭密码锁UI
    /// </summary>
    public void CloseKeypad()
    {
        if (!isOpen && (uiPanel == null || !uiPanel.activeSelf))
        {
            return;
        }
        
        isOpen = false; // 【新增】标记状态为关闭
        
        if (uiPanel != null)
        {
            uiPanel.SetActive(false);
        }

        SetUiInputState(false);
    }

    /// <summary>
    /// 绑定在确认按钮上，也可以通过回车键(onSubmit)触发
    /// </summary>
    public void CheckPassword()
    {
        if (inputField == null)
        {
            Debug.LogWarning("PasswordKeypad 缺少 InputField，无法校验密码。");
            return;
        }

        if (inputField.text == correctPassword)
        {
            // --- 密码正确 ---
            Debug.Log("密码正确！");
            isSolved = true;
            if (feedbackText)
            {
                feedbackText.text = "ACCESS GRANTED";
                feedbackText.color = Color.green;
            }

            // 触发事件
            onPasswordCorrect.Invoke(); 

            // 延迟一点关闭UI，让玩家看到成功提示
            Invoke("CloseKeypad", 1.0f);
            
            if (_interactFlagScript != null)
            {
                _interactFlagScript.ExecuteLogic("密码锁解开触发");
            }
        }
        else
        {
            // --- 密码错误 ---
            Debug.Log("密码错误！");
            if (feedbackText)
            {
                feedbackText.text = "INVALID PASSWORD";
                feedbackText.color = Color.red;
            }

            if (inputField != null)
            {
                inputField.text = ""; // 清空重输
            }
            
            // 重新强制聚焦，确保玩家可以无缝继续敲键盘试下一个密码
            if (inputField != null)
            {
                inputField.Select();
                inputField.ActivateInputField();
            }
            
            onPasswordWrong.Invoke();
        }
    }

    private void OnDisable()
    {
        if (isOpen)
        {
            CloseKeypad();
        }
        else
        {
            SetUiInputState(false);
        }
    }

    private void OnDestroy()
    {
        if (inputField != null)
        {
            inputField.onSubmit.RemoveListener(OnInputSubmit);
        }
    }

    private void OnInputSubmit(string _)
    {
        CheckPassword();
    }

    private void SetUiInputState(bool uiOpened)
    {
        ResolveDependenciesIfNeeded();

        if (interactionController != null)
        {
            interactionController.IsUiOpen = uiOpened;
        }

        if (uiOpened)
        {
            BeginCursorOverride();
            BeginStarterInputOverride();
        }
        else
        {
            EndStarterInputOverride();
            EndCursorOverride();
        }
    }

    private void BeginCursorOverride()
    {
        if (cursorOverrideActive)
        {
            return;
        }

        cachedCursorLockMode = Cursor.lockState;
        cachedCursorVisible = Cursor.visible;
        Cursor.lockState = CursorLockMode.None;
        Cursor.visible = true;
        cursorOverrideActive = true;
    }

    private void EndCursorOverride()
    {
        if (!cursorOverrideActive)
        {
            return;
        }

        Cursor.lockState = cachedCursorLockMode;
        Cursor.visible = cachedCursorVisible;
        cursorOverrideActive = false;
    }

    private void BeginStarterInputOverride()
    {
        if (starterInputs == null || starterInputOverrideActive)
        {
            return;
        }

        cachedStarterInputsEnabled = starterInputs.enabled;
        cachedCursorLocked = starterInputs.cursorLocked;
        cachedCursorInputForLook = starterInputs.cursorInputForLook;
        starterInputs.MoveInput(Vector2.zero);
        starterInputs.LookInput(Vector2.zero);
        starterInputs.JumpInput(false);
        starterInputs.SprintInput(false);
        starterInputs.cursorLocked = false;
        starterInputs.cursorInputForLook = false;
        starterInputs.enabled = false;
        starterInputOverrideActive = true;
    }

    private void EndStarterInputOverride()
    {
        if (!starterInputOverrideActive)
        {
            return;
        }

        if (starterInputs != null)
        {
            starterInputs.enabled = cachedStarterInputsEnabled;
            starterInputs.cursorLocked = cachedCursorLocked;
            starterInputs.cursorInputForLook = cachedCursorInputForLook;
            starterInputs.MoveInput(Vector2.zero);
            starterInputs.LookInput(Vector2.zero);
            starterInputs.JumpInput(false);
            starterInputs.SprintInput(false);
        }

        starterInputOverrideActive = false;
    }

    private void ResolveDependenciesIfNeeded()
    {
        if (interactionController == null)
        {
            interactionController = FindObjectOfType<PlayerInteractionController>();
        }

        if (starterInputs == null)
        {
            starterInputs = FindObjectOfType<StarterAssetsInputs>();
        }
    }
}
