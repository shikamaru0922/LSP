using System;
using UnityEngine;
using UnityEngine.Events;

namespace MuseumGame.Interaction
{
    public class ElectronicDoor : MonoBehaviour
    {
        [Header("Door Setup")]
        [SerializeField] private Transform _doorPanel;
        
        [Header("Rotation Settings")]
        [SerializeField] private float _closedAngle = 0f;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _rotateSpeed = 90f;

        [Header("Lock & Key Logic")]
        [SerializeField] private bool _startLocked = true;
        [SerializeField] private bool _startOpen = false;
        
        
        [Tooltip("这扇门具体需要哪一把钥匙才能开？")]
        [SerializeField] private KeyID _requiredKey = KeyID.None; // 关键修改！
        
        [Tooltip("【调试用】勾选这个代表玩家身上有钥匙。以后这会由背包系统控制。")]
        public bool DEBUG_PlayerHasKey = false; 

        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _openSfx;    // 正常的开门声
        [SerializeField] private AudioClip _closeSfx;   // 关门声
        [SerializeField] private AudioClip _lockedSfx;  // 拉不动的声音 (没钥匙)
        [SerializeField] private AudioClip _unlockSfx;  // 钥匙转动/解锁的声音 (有钥匙)

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened;
        [SerializeField] private UnityEvent _onUnlockSuccess; // 解锁成功时的额外事件
        
        
        

        private bool _isOpen;
        private bool _isLocked;
        private float _currentAngle;
        private float _targetAngle;

        public bool IsLocked => _isLocked;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            
            _isLocked = _startLocked;
            _isOpen = _startOpen;
            
            _currentAngle = _isOpen ? _openAngle : _closedAngle;
            _targetAngle = _currentAngle;
            UpdateDoorRotation(_currentAngle);
        }

        private void Update()
        {
            if (_doorPanel == null) return;
            if (Mathf.Abs(_currentAngle - _targetAngle) > 0.1f)
            {
                _currentAngle = Mathf.MoveTowards(_currentAngle, _targetAngle, _rotateSpeed * Time.deltaTime);
                UpdateDoorRotation(_currentAngle);
            }
        }

        // === 核心交互逻辑 ===

        /// <summary>
        /// 门把手触发时调用此方法
        /// </summary>
        // === 修改：门把手触发逻辑 ===
        public void OnHandleTriggered()
        {
            if (_isOpen) return;

            // 1. 如果没锁，直接开
            if (!_isLocked)
            {
                Open();
                return;
            }

            // 2. 检查钥匙 (连接 KeyRing 系统)
            bool playerHasKey = false;
            
            // 只要不需要钥匙(None)，或者玩家钥匙扣里有这把钥匙，就算通过
            if (_requiredKey == KeyID.None) 
            {
                playerHasKey = true; // 不需要钥匙的锁？通常意味着只能剧情解锁，或者永远锁着
            }
            else if (PlayerKeyRing.Instance != null)
            {
                playerHasKey = PlayerKeyRing.Instance.HasKey(_requiredKey);
            }
            else
            {
                Debug.LogError("场景里找不到 PlayerKeyRing 脚本！请把它挂在玩家身上。");
            }

            if (playerHasKey)
            {
                UnlockAndOpen();
            }
            else
            {
                PlayClip(_lockedSfx);
                // 可以在这里加个判断，如果是 Key_4 的门，提示“需要组合钥匙”
                Debug.Log($"门锁住了，需要钥匙: {_requiredKey}");
            }
        }

        private void UnlockAndOpen()
        {
            _isLocked = false;
            
            // 播放解锁声
            PlayClip(_unlockSfx); 
            _onUnlockSuccess?.Invoke();
            
            // 紧接着开门 (稍微延迟一点点或者直接开也可以，这里直接开比较流畅)
            Open(); 
            
            Debug.Log("钥匙匹配！门已解锁并打开。");
        }

        public void Open()
        {
            if (_isOpen) return;
            _isOpen = true;
            _targetAngle = _openAngle;
            PlayClip(_openSfx);
            _onOpened?.Invoke();
        }

        public void Close()
        {
            if (!_isOpen) return;
            _isOpen = false;
            _targetAngle = _closedAngle;
            PlayClip(_closeSfx);
        }

        public void ForceLock()
        {
            _isOpen = false;
            _isLocked = true;
            _targetAngle = _closedAngle;
            _currentAngle = _closedAngle; // 瞬移关闭
            UpdateDoorRotation(_currentAngle);
            PlayClip(_lockedSfx); // 播放上锁声
        }

        // 用于给外部脚本（如拾取钥匙脚本）真正给予钥匙
        public void SetPlayerHasKey(bool hasKey)
        {
            DEBUG_PlayerHasKey = hasKey;
        }

        private void UpdateDoorRotation(float angle)
        {
            if (_doorPanel != null)
                _doorPanel.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audioSource != null) _audioSource.PlayOneShot(clip);
        }
    }
}