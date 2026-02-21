using UnityEngine;
using DG.Tweening; // 引入 DoTween 命名空间

namespace LSP.Gameplay
{
    /// <summary>
    /// 竞技场墙壁管理器：控制四面墙的升起与向内挤压 (DoTween驱动)
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
        }

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

        private void Update()
        {
            if (!_isShrinking) return;

            // 【核心修改】优先使用你指定的 centerPoint，如果没有，才使用自身位置
            Vector3 center = centerPoint != null ? centerPoint.position : transform.position;

            foreach (var wall in walls)
            {
                if (wall == null) continue;

                Vector3 dirToCenter = center - wall.position;
                dirToCenter.y = 0;

                float currentDistance = dirToCenter.magnitude;

                if (currentDistance > minCenterDistance)
                {
                    wall.Translate(dirToCenter.normalized * (shrinkSpeed * Time.deltaTime), Space.World);
                }
            }
        }

        public void StopShrinking()
        {
            _isShrinking = false;
            Debug.Log("【ArenaWallManager】缩圈已停止。");
        }
    }
}