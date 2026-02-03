using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MuseumGame.Interaction
{
    public class ElectronicDoor : MonoBehaviour
    {
        [Header("Door Setup")]
        [SerializeField] private Transform _doorPanel;  // 门板
        [SerializeField] private Transform _doorHandle; // 门把手
        
        // 【新增】初始状态设置
        [Tooltip("勾选后，游戏开始时门就是开着的")]
        [SerializeField] private bool _startOpen = false; 
        
        [Header("Door Rotation (门板)")]
        [SerializeField] private float _closedAngle = 0f;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _doorRotateSpeed = 90f;

        [Header("Handle Animation (门把手)")]
        [SerializeField] private Vector3 _handleDownRotation = new Vector3(0, 0, -45);
        [SerializeField] private Vector3 _handleUpRotation = Vector3.zero;
        [SerializeField] private float _handleTurnSpeed = 5.0f; 
        [SerializeField] private float _preOpenDelay = 0.1f;

        [Header("Lock & Key Logic")]
        [SerializeField] private bool _startLocked = true;
        [SerializeField] private KeyID _requiredKey = KeyID.None;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _handleSound; // 把手转动
        [SerializeField] private AudioClip _openSfx;     // 开门声
        [SerializeField] private AudioClip _closeSfx;    // 关门声
        [SerializeField] private AudioClip _lockedSfx;   // 锁住的声音
        [SerializeField] private AudioClip _unlockSfx;   // 解锁声

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened;
        [SerializeField] private UnityEvent _onUnlockSuccess;

        private bool _isOpen;
        private bool _isLocked;
        private float _currentDoorAngle;
        private float _targetDoorAngle;
        private Coroutine _openRoutine; 
        private InteractableSetFlag _interactFlagScript;
        
        
        public bool IsLocked => _isLocked;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            
            _isLocked = _startLocked;

            // === 【核心修改】初始化状态 ===
            if (_startOpen)
            {
                // 如果设置了初始开门
                _isOpen = true;
                _currentDoorAngle = _openAngle;
                _targetDoorAngle = _openAngle;
                // 注意：如果初始开门，建议把 isLocked 在 Inspector 里勾掉，或者这里强制设为 false
                // _isLocked = false; 
            }
            else
            {
                // 默认关门状态
                _isOpen = false;
                _currentDoorAngle = _closedAngle;
                _targetDoorAngle = _closedAngle;
            }

            // 应用初始角度
            UpdateDoorRotation(_currentDoorAngle);

            if (_doorHandle != null) _doorHandle.localRotation = Quaternion.Euler(_handleUpRotation);
        }

        private void Start()
        {
            _interactFlagScript = GetComponent<InteractableSetFlag>();
        }

        private void Update()
        {
            // 平滑旋转处理
            if (_doorPanel != null && Mathf.Abs(_currentDoorAngle - _targetDoorAngle) > 0.1f)
            {
                _currentDoorAngle = Mathf.MoveTowards(_currentDoorAngle, _targetDoorAngle, _doorRotateSpeed * Time.deltaTime);
                UpdateDoorRotation(_currentDoorAngle);
            }
        }

        // =========================================================
        //  交互逻辑
        // =========================================================

        public void OnHandleTriggered()
        {
            // 如果正在动，或者是开着的，就忽略 (或者你可以改成：如果是开着的就调用 Close())
            if (_isOpen || _openRoutine != null) 
            {
                // 可选：如果你希望点击开着的门自动关门，可以在这里调用 Close();
                return; 
            }

            if (!_isLocked)
            {
                StartCoroutine(OpenSequence()); 
                return;
            }

            // 检查钥匙
            bool playerHasKey = false;
            if (_requiredKey == KeyID.None) playerHasKey = true;
            else if (PlayerKeyRing.Instance != null && PlayerKeyRing.Instance.HasKey(_requiredKey)) playerHasKey = true;

            if (playerHasKey)
            {
                UnlockAndOpen();
            }
            else
            {
                PlayClip(_lockedSfx);
                Debug.Log($"门锁住了，需要钥匙: {_requiredKey}");
            }
        }

        public void ForceLock()
        {
            _isOpen = false;
            _isLocked = true;
            _targetDoorAngle = _closedAngle;
            _currentDoorAngle = _closedAngle; // 瞬间关上
            // 如果不想瞬间关上，可以把上面这行删掉，只保留 _targetDoorAngle
            
            UpdateDoorRotation(_currentDoorAngle);
            if(_doorHandle != null) _doorHandle.localRotation = Quaternion.Euler(_handleUpRotation);
            PlayClip(_lockedSfx);
        }

        private void UnlockAndOpen()
        {
            _isLocked = false;
            PlayClip(_unlockSfx); 
            _onUnlockSuccess?.Invoke();
            StartCoroutine(OpenSequence());
        }

        // =========================================================
        //  关门逻辑
        // =========================================================
        public void Close()
        {
            if (!_isOpen || _openRoutine != null) return;
            StartCoroutine(CloseSequence());
        }

        private IEnumerator CloseSequence()
        {
            _openRoutine = StartCoroutine(DoNothing());

            // 设置目标为关闭角度
            _targetDoorAngle = _closedAngle;

            // 等待门转到位
            while (Mathf.Abs(_currentDoorAngle - _closedAngle) > 0.5f)
            {
                yield return null;
            }

            // 撞击门框，强制归位
            _currentDoorAngle = _closedAngle;
            UpdateDoorRotation(_currentDoorAngle);
            PlayClip(_closeSfx); // 播放砰的一声

            _isOpen = false;
            _openRoutine = null;
        }

        // =========================================================
        //  开门动画序列
        // =========================================================
        private IEnumerator OpenSequence()
        {
            _openRoutine = StartCoroutine(DoNothing());

            PlayClip(_handleSound);

            // 转把手
            if (_doorHandle != null)
            {
                Quaternion startRot = _doorHandle.localRotation;
                Quaternion targetRot = Quaternion.Euler(_handleDownRotation);
                float t = 0;
                while(t < 1f)
                {
                    t += Time.deltaTime * _handleTurnSpeed;
                    _doorHandle.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                    yield return null;
                }
            }

            yield return new WaitForSeconds(_preOpenDelay);

            _isOpen = true;
            _targetDoorAngle = _openAngle;
            PlayClip(_openSfx);
            _onOpened?.Invoke();
            
            if (_interactFlagScript != null)
            {
                // 调用它的公共方法，传入来源字符串方便调试
                // 这会自动处理：SetFlag、播放成功音效、显示关联物体(objectToShow) 等
                _interactFlagScript.ExecuteLogic("视线移开消失");
            
                // 注意：InteractableSetFlag 内部可能会隐藏物体(hideOnInteract=true)
                // 但为了双重保险，我们检查一下，如果它没隐藏，我们这里强制隐藏
               
            }
            

            // 把手回弹
            yield return new WaitForSeconds(0.5f);
            if (_doorHandle != null)
            {
                Quaternion startRot = _doorHandle.localRotation;
                Quaternion targetRot = Quaternion.Euler(_handleUpRotation);
                float t = 0;
                while (t < 1f)
                {
                    t += Time.deltaTime * _handleTurnSpeed;
                    _doorHandle.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                    yield return null;
                }
            }

            _openRoutine = null;
        }

        IEnumerator DoNothing() { yield return null; }

        private void UpdateDoorRotation(float angle)
        {
            if (_doorPanel != null) _doorPanel.localRotation = Quaternion.Euler(0f, angle, 0f);
        }

        private void PlayClip(AudioClip clip)
        {
            if (clip != null && _audioSource != null) _audioSource.PlayOneShot(clip);
        }
    }
}