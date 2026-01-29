using UnityEngine;

namespace LSP.Gameplay
{
    /// <summary>
    /// 控制 NPC 在正常状态下的待机动画逻辑。
    /// 支持随机起始时间（防止动作整齐划一）和循环控制。
    /// </summary>
    [RequireComponent(typeof(Animator))]
    public class NpcIdleController : MonoBehaviour
    {
        [Header("Animation Settings")]
        [Tooltip("Animator Controller 中 State 的名字 (例如 'Idle', 'Talk', 'Sit')")]
        [SerializeField]
        public string stateName = "Idle";

        [Tooltip("是否随机打乱起始时间？(0% - 100%)\n勾选此项可避免所有 NPC 动作整齐划一。")]
        [SerializeField]
        public bool randomStartOffset = true;

        [Tooltip("动画播放完毕后是否循环？\n如果不勾选，动画播放一次后会停在最后一帧。")]
        [SerializeField]
        public bool loopAnimation = true;

        private Animator animator;
        private int stateHash;
        private bool hasFinishedPlaying;

        private void Awake()
        {
            animator = GetComponent<Animator>();
            // 预先计算 Hash，比用 string 性能更好
            stateHash = Animator.StringToHash(stateName);
        }

        private void OnEnable()
        {
            // 每次物体被激活（或脚本开启）时，重新播放
            PlayAnimation();
        }

        private void PlayAnimation()
        {
            if (animator == null) return;

            // 确保 Animator 是开启的 (可能被 DeadStareController 关闭过)
            animator.enabled = true;
            animator.speed = 1f;
            hasFinishedPlaying = false;

            // 计算起始时间 (0.0 - 1.0)
            float startOffset = randomStartOffset ? Random.Range(0f, 1f) : 0f;

            // 立即播放指定状态
            // layer: 0 (Base Layer)
            animator.Play(stateHash, 0, startOffset);
        }

        private void Update()
        {
            // 如果需要循环，或者是 Animator 被禁用（比如进入了 DeadStare 状态），就不需要检查
            if (loopAnimation || animator == null || !animator.enabled || hasFinishedPlaying)
            {
                return;
            }

            CheckAnimationFinish();
        }

        private void CheckAnimationFinish()
        {
            // 获取当前动画状态信息
            AnimatorStateInfo stateInfo = animator.GetCurrentAnimatorStateInfo(0);

            // 检查是否正在播放我们需要的那一个动画
            // (防止 Animator 还在过渡或者处于其他状态)
            if (stateInfo.shortNameHash == stateHash)
            {
                // normalizedTime >= 1.0f 表示播放了一遍
                // 注意：如果动画本身是 Loop 的，normalizedTime 会一直增加 (1.1, 1.2...)
                if (stateInfo.normalizedTime >= 1.0f)
                {
                    // 强制暂停动画，停在最后一帧
                    animator.speed = 0f;
                    hasFinishedPlaying = true;
                }
            }
        }

        /// <summary>
        /// 提供给外部修改动画的方法（如果需要动态改变行为）
        /// </summary>
        public void SetAnimation(string newStateName, bool loop, bool randomStart)
        {
            stateName = newStateName;
            loopAnimation = loop;
            randomStartOffset = randomStart;
            stateHash = Animator.StringToHash(stateName);
            PlayAnimation();
        }
    }
}