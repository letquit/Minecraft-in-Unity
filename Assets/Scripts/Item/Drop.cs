using UnityEngine;
using DG.Tweening;

/// <summary>
/// 掉落物品控制类。负责处理物品的初始化、拾取逻辑、抛出动画以及落地后的物理修正。
/// </summary>
public class Drop : MonoBehaviour
{
    public float collectDistance = 2f;

    [Tooltip("垂直方向的拾取距离（高度差）")] public float collectHeightDistance = 3f;

    [Header("Visual Setting")] [Tooltip("用于放置模型和动画的子物体")]
    public Transform visualContainer;

    [Header("Collect Anim Setting")] [Tooltip("拾取动画持续时间")]
    public float collectAnimDuration = 0.3f;

    [Header("Throw Anim Setting")] [Tooltip("抛出动画持续时间")]
    public float throwDuration = 0.5f;

    [Tooltip("抛出最高点高度")] public float throwHeight = 1.5f;

    [Header("Throw Setting")] [Tooltip("地面检测层")] public LayerMask groundLayer;
    [Tooltip("悬浮高度")] public float hoverHeight = 0.3f;

    [HideInInspector] public GameObject modelPrefab;
    [HideInInspector] public Item item;

    private bool isCollecting = false;
    private bool isThrowing = false;
    private bool isInitialized = false;

    /// <summary>
    /// 初始化掉落对象，在 Start 中调用。如果未手动设置 groundLayer，则默认排除 Drop 层进行地面检测。
    /// </summary>
    void Start()
    {
        if (modelPrefab && !isInitialized)
        {
            Init();
        }

        // 如果没有设置 groundLayer，默认检测除 Drop 外的所有层
        if (groundLayer == 0)
        {
            int dropLayerIndex = LayerMask.NameToLayer("Drop");
            groundLayer = ~(1 << dropLayerIndex);
        }
    }

    /// <summary>
    /// 实例化模型预制体，并移除其碰撞器以避免干扰拾取判断。
    /// </summary>
    public void Init()
    {
        if (isInitialized) return;
        isInitialized = true;

        Transform parent = visualContainer != null ? visualContainer : transform;

        GameObject model = Instantiate(modelPrefab, parent);
        model.transform.localPosition = Vector3.zero;
        model.transform.localRotation = Quaternion.identity;

        Collider modelCollider = model.GetComponent<Collider>();
        if (modelCollider != null)
        {
            Destroy(modelCollider);
        }
    }

    /// <summary>
    /// 每帧检查玩家是否进入拾取范围，若满足条件则触发拾取流程。
    /// </summary>
    void Update()
    {
        if (isCollecting || isThrowing) return;

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player)
        {
            if (IsWithinCollectRange(player.transform.position))
            {
                Collect();
            }
        }
    }

    /// <summary>
    /// 判断给定坐标是否处于可拾取范围内（水平与垂直距离均符合要求）。
    /// </summary>
    /// <param name="playerPosition">玩家当前世界坐标</param>
    /// <returns>是否在拾取范围内</returns>
    private bool IsWithinCollectRange(Vector3 playerPosition)
    {
        Vector3 dropPosFlat = new Vector3(transform.position.x, 0, transform.position.z);
        Vector3 playerPosFlat = new Vector3(playerPosition.x, 0, playerPosition.z);
        float horizontalDistance = Vector3.Distance(dropPosFlat, playerPosFlat);

        float verticalDistance = Mathf.Abs(playerPosition.y - transform.position.y);

        return horizontalDistance < collectDistance && verticalDistance < collectHeightDistance;
    }

    /// <summary>
    /// 触发拾取过程：播放缩放+移动动画，完成后通知玩家获取该物品并销毁自身。
    /// </summary>
    public void Collect()
    {
        isCollecting = true;

        GameObject player = GameObject.FindGameObjectWithTag("Player");

        Vector3 targetPos = player.transform.position;

        Sequence sequence = DOTween.Sequence();

        sequence.Append(transform.DOMove(targetPos, collectAnimDuration).SetEase(Ease.InQuad));
        sequence.Join(transform.DOScale(Vector3.zero, collectAnimDuration).SetEase(Ease.InQuad));

        sequence.OnComplete(() =>
        {
            player.GetComponent<Player>().GetItem(item);
            Destroy(gameObject);
        });
    }

    /// <summary>
    /// 抛出物品到指定位置，带抛物线动画
    /// </summary>
    /// <param name="targetPos">目标世界坐标</param>
    public void ThrowTo(Vector3 targetPos)
    {
        isThrowing = true;

        Vector3 startPos = transform.position;

        Vector3 middlePos = (startPos + targetPos) / 2f;
        middlePos.y = Mathf.Max(startPos.y, targetPos.y) + throwHeight;

        Vector3[] path = new Vector3[] { startPos, middlePos, targetPos };

        transform.DOPath(path, throwDuration, PathType.CatmullRom)
            .SetEase(Ease.OutQuad)
            .OnComplete(() =>
            {
                // 动画结束后检测并修正位置
                FixPositionIfInsideGround();
                isThrowing = false;
            });

        if (visualContainer != null)
        {
            visualContainer.DOLocalRotate(new Vector3(0, 360, 0), throwDuration, RotateMode.FastBeyond360)
                .SetEase(Ease.OutQuad);
        }
    }

    /// <summary>
    /// 检测是否卡在地面里，如果是则修正位置
    /// </summary>
    private void FixPositionIfInsideGround()
    {
        Vector3 currentPos = transform.position;

        // 从当前位置向上发射射线，检测是否在地面内部
        if (Physics.Raycast(currentPos, Vector3.up, out RaycastHit hitUp, 10f, groundLayer))
        {
            // 如果向上能打到东西，说明在地面内部
            // 尝试在落点附近寻找有效位置
            Vector3 validPos = FindValidPositionNearby(currentPos);
            transform.position = validPos;
            return;
        }

        // 从上方向下发射射线，确保在地面上方
        Vector3 rayStart = currentPos + Vector3.up * 5f;
        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hitDown, 10f, groundLayer))
        {
            float groundY = hitDown.point.y + hoverHeight;

            // 如果当前位置低于地面，修正到地面上
            if (currentPos.y < groundY)
            {
                transform.position = new Vector3(currentPos.x, groundY, currentPos.z);
            }
        }
    }

    /// <summary>
    /// 在落点附近寻找有效位置
    /// </summary>
    /// <param name="originalPos">原始落点坐标</param>
    /// <returns>有效的安全坐标</returns>
    private Vector3 FindValidPositionNearby(Vector3 originalPos)
    {
        // 定义搜索方向：上、四周
        Vector3[] searchDirections = new Vector3[]
        {
            Vector3.up, // 上方
            Vector3.forward, // 前
            Vector3.back, // 后
            Vector3.right, // 右
            Vector3.left, // 左
            (Vector3.forward + Vector3.right).normalized, // 右前
            (Vector3.forward + Vector3.left).normalized, // 左前
            (Vector3.back + Vector3.right).normalized, // 右后
            (Vector3.back + Vector3.left).normalized // 左后
        };

        // 搜索距离
        float[] searchDistances = new float[] { 0.5f, 1f, 1.5f, 2f };

        foreach (float distance in searchDistances)
        {
            foreach (Vector3 dir in searchDirections)
            {
                Vector3 testPos = originalPos + dir * distance;

                // 检测这个位置是否有效（不在地面内部）
                if (IsPositionValid(testPos))
                {
                    // 找到地面高度
                    Vector3 rayStart = testPos + Vector3.up * 5f;
                    if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 10f, groundLayer))
                    {
                        return new Vector3(testPos.x, hit.point.y + hoverHeight, testPos.z);
                    }

                    return testPos;
                }
            }
        }

        // 如果都找不到，强制放到原位置上方
        Vector3 abovePos = originalPos + Vector3.up * 3f;
        if (Physics.Raycast(abovePos, Vector3.down, out RaycastHit hitFinal, 10f, groundLayer))
        {
            return new Vector3(originalPos.x, hitFinal.point.y + hoverHeight, originalPos.z);
        }

        return originalPos + Vector3.up * 2f;
    }

    /// <summary>
    /// 检测位置是否有效（不在地面内部）
    /// </summary>
    /// <param name="pos">待检测的世界坐标</param>
    /// <returns>是否是合法的位置</returns>
    private bool IsPositionValid(Vector3 pos)
    {
        // 检测是否在地面内部：向上发射射线
        if (Physics.Raycast(pos, Vector3.up, 5f, groundLayer))
        {
            // 向上能打到东西，说明在内部
            return false;
        }

        // 使用球形检测该位置是否被占用
        Collider[] colliders = Physics.OverlapSphere(pos, 0.3f, groundLayer);
        return colliders.Length == 0;
    }
}