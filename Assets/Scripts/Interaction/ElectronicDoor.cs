using System;
using UnityEngine;
using UnityEngine.Events;

namespace MuseumGame.Interaction
{
    /// <summary>
    /// Controls a simple sliding door made of two panels that move apart when opened.
    /// Designed for white-box setups that use cubes instead of authored animations.
    /// </summary>
    public class ElectronicDoor : MonoBehaviour
    {
        [Header("Door Panels")]
        [SerializeField] private Transform _leftDoor;
        [SerializeField] private Transform _rightDoor;

        [Header("Open Offsets (Local Space)")]
        [SerializeField] private Vector3 _leftOpenLocalOffset = new Vector3(-1.0f, 0f, 0f);
        [SerializeField] private Vector3 _rightOpenLocalOffset = new Vector3(1.0f, 0f, 0f);

        [Header("Movement")]
        [SerializeField] private float _moveSpeed = 3f;
        [SerializeField] private bool _startOpen;

        [Header("Events")]
        [SerializeField] private UnityEvent _onOpened;
        [SerializeField] private UnityEvent _onClosed;

        public event Action<bool> DoorStateChanged;

        private bool _isOpen;
        private Vector3 _leftClosedLocalPos;
        private Vector3 _rightClosedLocalPos;
        private Vector3 _leftTargetLocalPos;
        private Vector3 _rightTargetLocalPos;

        public bool IsOpen => _isOpen;

        private void Awake()
        {
            if (_leftDoor != null)
            {
                _leftClosedLocalPos = _leftDoor.localPosition;
            }

            if (_rightDoor != null)
            {
                _rightClosedLocalPos = _rightDoor.localPosition;
            }

            SetOpenInternal(_startOpen, true);
        }

        private void Update()
        {
            UpdatePanel(_leftDoor, _leftTargetLocalPos);
            UpdatePanel(_rightDoor, _rightTargetLocalPos);
        }

        public void Open()
        {
            SetOpen(true);
        }

        public void Close()
        {
            SetOpen(false);
        }

        public void Toggle()
        {
            SetOpen(!_isOpen);
        }

        public void SetOpen(bool open)
        {
            SetOpenInternal(open, false);
        }

        private void SetOpenInternal(bool open, bool force)
        {
            if (!force && _isOpen == open)
            {
                return;
            }

            _isOpen = open;
            _leftTargetLocalPos = _leftClosedLocalPos + (open ? _leftOpenLocalOffset : Vector3.zero);
            _rightTargetLocalPos = _rightClosedLocalPos + (open ? _rightOpenLocalOffset : Vector3.zero);

            if (_moveSpeed <= 0f)
            {
                if (_leftDoor != null)
                {
                    _leftDoor.localPosition = _leftTargetLocalPos;
                }

                if (_rightDoor != null)
                {
                    _rightDoor.localPosition = _rightTargetLocalPos;
                }
            }

            if (!force)
            {
                if (_isOpen)
                {
                    _onOpened?.Invoke();
                }
                else
                {
                    _onClosed?.Invoke();
                }

                DoorStateChanged?.Invoke(_isOpen);
            }
        }

        private void UpdatePanel(Transform panel, Vector3 targetLocalPos)
        {
            if (panel == null)
            {
                return;
            }

            if (_moveSpeed <= 0f)
            {
                panel.localPosition = targetLocalPos;
                return;
            }

            panel.localPosition = Vector3.MoveTowards(panel.localPosition, targetLocalPos, _moveSpeed * Time.deltaTime);
        }
    }
}
