using UnityEngine;
using DG.Tweening; // 引入 DoTween 命名空间

namespace LSP.Gameplay
{
    /// <summary>
    /// 竞技场墙壁管理器：控制四面墙的升起、向内挤压与降下消失 (DoTween驱动)
    /// </summary>
    public class ArenaWallManager : MonoBehaviour
    {
        [Header("===== 墙壁引用 =====")]
        [Tooltip("拖入前后左右四面墙")]
        public Transform[] walls = new Transform[4];

        [Header("===== 升起 (Rise) 设置 (DoTween) =====")]
        [Tooltip("墙壁升起的高度 (Y轴位移)")]
        public float riseHeight = 5f;
        [Tooltip("升起所需的时间 (秒)")]
        public float riseDuration = 1.5f;
        [Tooltip("升起动画的缓动曲线 (推荐 OutBack 带有机械卡扣的沉重感)")]
        public Ease riseEase = Ease.OutBack; 
        [Tooltip("升起时的音效 (轰隆隆的沉重感)")]
        public AudioClip riseSound;

        [Header("===== 缩圈 (Shrink) 设置 =====")]
        [Tooltip("【新增】自定义缩圈的中心点 (拖入一个空物体)。如果不填，默认使用当前脚本挂载的物体位置")]
        public Transform centerPoint;
        [Tooltip("墙壁向中心挤压的速度 (米/秒)")]
        public float shrinkSpeed = 0.5f;
        [Tooltip("距离中心点的最小极限距离 (防止把玩家压穿模)")]
        public float minCenterDistance = 2f;

        [Header("===== 降下/消失 (Descend) 设置 (DoTween) =====")]
        [Tooltip("降下所需的时间 (秒)")]
        public float descendDuration = 1.5f;
        [Tooltip("降下动画的缓动曲线 (推荐 InBack 带有机关收回的力度感，或 InQuad)")]
        public Ease descendEase = Ease.InBack; 
        [Tooltip("降下/解除危机时的音效")]
        public AudioClip descendSound;

        [Header("===== 碰撞稳定修复 =====")]
        [Tooltip("运行时自动修复墙体物理配置，避免墙体乱飞。")]
        public bool autoFixWallPhysics = true;
        [Tooltip("将墙体 Rigidbody 强制设为 Kinematic，并禁用重力。")]
        public bool forceKinematicRigidbody = true;
        [Tooltip("冻结墙体旋转，防止碰撞后倾倒。")]
        public bool freezeWallRotation = true;
        [Tooltip("当墙体只有 Trigger 时自动补一个实体 BoxCollider，防止玩家穿墙。")]
        public bool addBlockingColliderForTriggerWalls = true;

        private AudioSource _audioSource;
        private bool _isShrinking = false;

        private void Awake()
        {
            _audioSource = GetComponent<AudioSource>();
            if (_audioSource == null)
            {
                _audioSource = gameObject.AddComponent<AudioSource>();
                _audioSource.spatialBlend = 1f;
            }

            PrepareWallsForStableCollision();
        }

        // =========================================================
        // 【触发升起】供 Event System 调用 (用于开战)
        // =========================================================
        public void TriggerWallsRise()
        {
            Debug.Log("【ArenaWallManager】墙壁开始升起 (DoTween驱动)！");

            if (riseSound != null)
            {
                _audioSource.PlayOneShot(riseSound);
            }

            Sequence riseSequence = DOTween.Sequence();

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] != null)
                {
                    Vector3 targetPos = walls[i].position + new Vector3(0, riseHeight, 0);
                    riseSequence.Join(walls[i].DOMove(targetPos, riseDuration).SetEase(riseEase));
                }
            }

            riseSequence.OnComplete(() => 
            {
                Debug.Log("【ArenaWallManager】墙壁升起完毕，开始缩圈！");
                _isShrinking = true;
            });
        }

        // =========================================================
        // 【缩圈逻辑】持续执行
        // =========================================================
        private void FixedUpdate()
        {
            if (!_isShrinking) return;

            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

            foreach (var wall in walls)
            {
                if (wall == null) continue;

                Vector3 dirToCenter = center - wall.position;
                dirToCenter.y = 0;

                float currentDistance = dirToCenter.magnitude;

                if (currentDistance > minCenterDistance)
                {
                    Vector3 moveDelta = dirToCenter.normalized * (shrinkSpeed * Time.fixedDeltaTime);
                    MoveWall(wall, moveDelta);
                }
            }
        }

        public void StopShrinking()
        {
            _isShrinking = false;
            Debug.Log("【ArenaWallManager】缩圈已停止。");
        }

        // =========================================================
        // 【触发降下】供 Event System 调用 (用于战斗结束/清场)
        // =========================================================
        public void TriggerWallsDescend()
        {
            Debug.Log("【ArenaWallManager】危机解除，墙壁开始降下！");

            // 1. 强制停止缩圈
            _isShrinking = false;

            // 2. 播放降下音效
            if (descendSound != null)
            {
                _audioSource.PlayOneShot(descendSound);
            }

            // 3. 启动降下动画序列
            Sequence descendSequence = DOTween.Sequence();

            for (int i = 0; i < walls.Length; i++)
            {
                if (walls[i] != null)
                {
                    // 仅改变 Y 轴，让它原地沉底，保持当前的 XZ 坐标不变
                    float targetY = walls[i].position.y - riseHeight;
                    descendSequence.Join(walls[i].DOMoveY(targetY, descendDuration).SetEase(descendEase));
                }
            }

            // 4. 动画完成后的收尾工作
            descendSequence.OnComplete(() => 
            {
                Debug.Log("【ArenaWallManager】墙壁已完全降下，场地清理完毕。");
                // 如果你想让墙壁降下后彻底禁用不占用性能，可以取消下面这段注释：
                /*
                foreach (var wall in walls)
                {
                    if (wall != null) wall.gameObject.SetActive(false);
                }
                */
            });
        }

        private void PrepareWallsForStableCollision()
        {
            if (!autoFixWallPhysics || walls == null)
            {
                return;
            }

            foreach (Transform wall in walls)
            {
                if (wall == null)
                {
                    continue;
                }

                StabilizeWallRigidbody(wall);

                if (addBlockingColliderForTriggerWalls)
                {
                    EnsureBlockingCollider(wall);
                }
            }
        }

        private void StabilizeWallRigidbody(Transform wall)
        {
            Rigidbody rb = wall.GetComponent<Rigidbody>();
            if (rb == null)
            {
                return;
            }

            if (forceKinematicRigidbody)
            {
                rb.isKinematic = true;
            }

            rb.useGravity = false;
            rb.interpolation = RigidbodyInterpolation.Interpolate;
            rb.collisionDetectionMode = CollisionDetectionMode.ContinuousSpeculative;

            if (freezeWallRotation)
            {
                rb.constraints |= RigidbodyConstraints.FreezeRotationX
                    | RigidbodyConstraints.FreezeRotationY
                    | RigidbodyConstraints.FreezeRotationZ;
            }
        }

        private void EnsureBlockingCollider(Transform wall)
        {
            Collider[] colliders = wall.GetComponents<Collider>();
            if (colliders == null || colliders.Length == 0)
            {
                return;
            }

            bool hasSolidCollider = false;
            BoxCollider triggerBoxCollider = null;
            Collider firstEnabledCollider = null;

            foreach (Collider collider in colliders)
            {
                if (collider == null || !collider.enabled)
                {
                    continue;
                }

                if (firstEnabledCollider == null)
                {
                    firstEnabledCollider = collider;
                }

                if (collider.isTrigger)
                {
                    if (triggerBoxCollider == null && collider is BoxCollider boxCollider)
                    {
                        triggerBoxCollider = boxCollider;
                    }

                    continue;
                }

                hasSolidCollider = true;
                break;
            }

            if (hasSolidCollider)
            {
                return;
            }

            if (triggerBoxCollider != null)
            {
                BoxCollider blocker = wall.gameObject.AddComponent<BoxCollider>();
                blocker.center = triggerBoxCollider.center;
                blocker.size = triggerBoxCollider.size;
                blocker.sharedMaterial = triggerBoxCollider.sharedMaterial;
                blocker.contactOffset = triggerBoxCollider.contactOffset;
                blocker.isTrigger = false;
                return;
            }

            if (firstEnabledCollider != null)
            {
                firstEnabledCollider.isTrigger = false;
            }
        }

        private void MoveWall(Transform wall, Vector3 worldDelta)
        {
            Rigidbody rb = wall.GetComponent<Rigidbody>();
            if (rb != null && rb.isKinematic)
            {
                rb.MovePosition(rb.position + worldDelta);
                return;
            }

            wall.Translate(worldDelta, Space.World);
        }
    }
}
