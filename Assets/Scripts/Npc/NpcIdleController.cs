using UnityEngine;
using System.Collections;

namespace LSP.Gameplay
{
    [RequireComponent(typeof(Animator))]
    public class NpcIdleController : MonoBehaviour
    {
        // === 动画设置 ===
        [HideInInspector] public string stateName; 
        public bool randomStartOffset = true;
        public bool loopAnimation = true;

        // === 新增：移动设置 ===
        [Header("Movement Settings")]
        [Tooltip("是否启用折返走动？(适用于 Walk/Patrol 动作)")]
        public bool enableMovement = false;

        [Tooltip("移动速度")]
        public float moveSpeed = 1.5f;

        [Tooltip("单程行走的距离 (米)")]
        public float patrolDistance = 5.0f;

        [Tooltip("转身时的旋转速度")]
        public float turnSpeed = 180f;

        [Tooltip("到达端点后停留多久再转身？")]
        public float waitTimeAtEnd = 1.0f;

        // === 内部变量 ===
        private Animator animator;
        private int stateHash;
        private bool hasFinishedPlaying;

        // 巡逻相关状态
        private Vector3 startPos;
        private Vector3 endPos;
        private Vector3 currentTarget;
        private bool isWaiting; // 是否正在端点发呆

        private void Awake()
        {
            animator = GetComponent<Animator>();
            // 记录初始位置作为起点
            startPos = transform.position;
            // 计算终点 (当前朝向的前方 X 米处)
            endPos = startPos + transform.forward * patrolDistance;
            currentTarget = endPos;
            
            PlayAnimation();
        }

        private void OnEnable()
        {
            PlayAnimation();
        }

        // 供 Editor 预览更新用
        public void UpdatePatrolPoints()
        {
            if (!Application.isPlaying)
            {
                startPos = transform.position;
                endPos = startPos + transform.forward * patrolDistance;
            }
        }

        public void PlayAnimation()
        {
            if (animator == null) animator = GetComponent<Animator>();
            if (animator == null || string.IsNullOrEmpty(stateName)) return;

            animator.enabled = true;
            animator.speed = 1f;
            hasFinishedPlaying = false;
            stateHash = Animator.StringToHash(stateName);

            float startOffset = randomStartOffset ? Random.Range(0f, 1f) : 0f;
            animator.Play(stateHash, 0, startOffset);
        }

        private void Update()
        {
            // 1. 动画循环检查逻辑
            CheckAnimationLoop();

            // 2. 移动逻辑
            if (enableMovement && !isWaiting && animator.enabled && animator.speed > 0)
            {
                HandleMovement();
            }
        }

        private void HandleMovement()
        {
            // --- 位移 ---
            // 向当前目标移动
            transform.position = Vector3.MoveTowards(transform.position, currentTarget, moveSpeed * Time.deltaTime);

            // --- 转身逻辑 ---
            // 计算目标方向
            Vector3 directionToTarget = (currentTarget - transform.position).normalized;
            
            // 如果还没到目标，且方向不为零，就平滑旋转朝向目标
            if (directionToTarget != Vector3.zero)
            {
                Quaternion lookRotation = Quaternion.LookRotation(directionToTarget);
                transform.rotation = Quaternion.RotateTowards(transform.rotation, lookRotation, turnSpeed * Time.deltaTime);
            }

            // --- 到达检测 ---
            // 这里的 0.05f 是误差容忍度
            if (Vector3.Distance(transform.position, currentTarget) < 0.05f)
            {
                StartCoroutine(WaitAndTurnRoutine());
            }
        }

        IEnumerator WaitAndTurnRoutine()
        {
            isWaiting = true;

            // 1. 原地待机一会
            yield return new WaitForSeconds(waitTimeAtEnd);

            // 2. 切换目标点 (如果是起点就切到终点，反之亦然)
            if (currentTarget == endPos)
                currentTarget = startPos;
            else
                currentTarget = endPos;

            // 3. 继续移动 (Update里会自动处理转身)
            isWaiting = false;
        }

        private void CheckAnimationLoop()
        {
            if (loopAnimation || animator == null || !animator.enabled || hasFinishedPlaying) return;

            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);
            if (stateInfo.shortNameHash == stateHash)
            {
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    animator.speed = 0f;
                    hasFinishedPlaying = true;
                }
            }
        }
        
        // 用于在 Editor 脚本里画线
        public Vector3 GetStartPos() => Application.isPlaying ? startPos : transform.position;
        public Vector3 GetEndPos() => Application.isPlaying ? endPos : transform.position + transform.forward * patrolDistance;
    }
}