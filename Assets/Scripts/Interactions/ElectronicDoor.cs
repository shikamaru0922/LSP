using System;
using System.Collections;
using UnityEngine;
using UnityEngine.Events;

namespace MuseumGame.Interaction
{
    public class ElectronicDoor : MonoBehaviour
    {
        [Header("Door Setup")]
        [SerializeField] private Transform _doorPanel;  // 门板 (大的那个)
        [SerializeField] private Transform _doorHandle; // 门把手 (小的那个，必须是门板的子物体或者独立物体)
        
        [Header("Door Rotation (门板)")]
        [SerializeField] private float _closedAngle = 0f;
        [SerializeField] private float _openAngle = 90f;
        [SerializeField] private float _doorRotateSpeed = 90f;

        [Header("Handle Animation (门把手)")]
        [Tooltip("把手按下去的角度 (比如局部 Z 轴转 -45 度)")]
        [SerializeField] private Vector3 _handleDownRotation = new Vector3(0, 0, -45);
        [Tooltip("把手平时(抬起)的角度")]
        [SerializeField] private Vector3 _handleUpRotation = Vector3.zero;
        [Tooltip("把手转动的速度")]
        [SerializeField] private float _handleTurnSpeed = 5.0f; 
        [Tooltip("把手转到底后，等待多久门才开始动？(模拟机械延迟)")]
        [SerializeField] private float _preOpenDelay = 0.1f;

        [Header("Lock & Key Logic")]
        [SerializeField] private bool _startLocked = true;
        [SerializeField] private KeyID _requiredKey = KeyID.None;
        
        [Header("Audio")]
        [SerializeField] private AudioSource _audioSource;
        [SerializeField] private AudioClip _handleSound; // 把手转动的声音 (咔哒)
        [SerializeField] private AudioClip _openSfx;     // 门轴转动的声音 (吱呀)
        [SerializeField] private AudioClip _closeSfx;
        [SerializeField] private AudioClip _lockedSfx;   // 拉不动的声音
        [SerializeField] private AudioClip _unlockSfx;   // 钥匙开锁声

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened;
        [SerializeField] private UnityEvent _onUnlockSuccess;

        private bool _isOpen;
        private bool _isLocked;
        private float _currentDoorAngle;
        private float _targetDoorAngle;
        private Coroutine _openRoutine; // 防止重复触发

        public bool IsLocked => _isLocked;
        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_audioSource == null) _audioSource = GetComponent<AudioSource>();
            
            _isLocked = _startLocked;
            _isOpen = false; // 默认开始是关的，除非你想支持 StartOpen
            
            _currentDoorAngle = _closedAngle;
            _targetDoorAngle = _closedAngle;
            UpdateDoorRotation(_currentDoorAngle);

            // 初始化把手角度
            if (_doorHandle != null) _doorHandle.localRotation = Quaternion.Euler(_handleUpRotation);
        }

        private void Update()
        {
            // 1. 处理门板的平滑旋转
            if (_doorPanel != null && Mathf.Abs(_currentDoorAngle - _targetDoorAngle) > 0.1f)
            {
                _currentDoorAngle = Mathf.MoveTowards(_currentDoorAngle, _targetDoorAngle, _doorRotateSpeed * Time.deltaTime);
                UpdateDoorRotation(_currentDoorAngle);
            }
        }

        // === 交互入口 ===

        public void OnHandleTriggered()
        {
            if (_isOpen || _openRoutine != null) return; // 正在开或者已经开了，就不管

            if (!_isLocked)
            {
                StartCoroutine(OpenSequence()); // 没锁，直接执行开门动画序列
                return;
            }

            // 检查钥匙
            bool playerHasKey = false;
            if (_requiredKey == KeyID.None) playerHasKey = true;
            else if (PlayerKeyRing.Instance != null && PlayerKeyRing.Instance.HasKey(_requiredKey)) playerHasKey = true;

            if (playerHasKey)
            {
                // 解锁并开门
                UnlockAndOpen();
            }
            else
            {
                // 锁住了：播放拉不动的声音 + 简单的把手抖动(可选)
                PlayClip(_lockedSfx);
                // 这里如果想做细致，也可以加一个 StartCoroutine(ShakeHandle());
                Debug.Log($"门锁住了，需要钥匙: {_requiredKey}");
            }
        }

        public void ForceLock()
        {
            _isOpen = false;
            _isLocked = true;
            _targetDoorAngle = _closedAngle;
            _currentDoorAngle = _closedAngle;
            UpdateDoorRotation(_currentDoorAngle);
            if(_doorHandle != null) _doorHandle.localRotation = Quaternion.Euler(_handleUpRotation);
            PlayClip(_lockedSfx);
        }

        private void UnlockAndOpen()
        {
            _isLocked = false;
            PlayClip(_unlockSfx); 
            _onUnlockSuccess?.Invoke();
            
            StartCoroutine(OpenSequence()); // 紧接着执行开门序列
        }

        // === 核心动画序列：转把手 -> 等待 -> 开门 ===
        private IEnumerator OpenSequence()
        {
            _openRoutine = StartCoroutine(DoNothing()); // 占位，防止重复调用

            // 1. 播放把手声音
            PlayClip(_handleSound);

            // 2. 把手向下转 (模拟按下)
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

            // 3. 稍微停顿一下 (机械传动延迟)
            yield return new WaitForSeconds(_preOpenDelay);

            // 4. 正式开门 (设置目标角度，Update里会自动转)
            _isOpen = true;
            _targetDoorAngle = _openAngle;
            PlayClip(_openSfx);
            _onOpened?.Invoke();

            // 5. 等门稍微开一点后，把手弹回原位 (回弹)
            yield return new WaitForSeconds(0.5f);
            if (_doorHandle != null)
            {
                Quaternion startRot = _doorHandle.localRotation;
                Quaternion targetRot = Quaternion.Euler(_handleUpRotation);
                float t = 0;
                while (t < 1f)
                {
                    t += Time.deltaTime * _handleTurnSpeed; // 回弹可以稍微慢点
                    _doorHandle.localRotation = Quaternion.Lerp(startRot, targetRot, t);
                    yield return null;
                }
            }

            _openRoutine = null; // 序列结束
        }

        // 一个空的协程辅助
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