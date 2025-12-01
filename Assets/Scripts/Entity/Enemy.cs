using UnityEngine;

/// <summary>
/// 敌人类，继承自Entity。负责处理敌人的AI行为，包括追踪玩家、攻击、随机巡逻等功能。
/// </summary>
public class Enemy : Entity
{
    [Header("Combat Settings")]
    [Tooltip("接触玩家时造成的伤害")] public int bodyDamage = 1;

    [Tooltip("停止追踪并准备攻击的目标距离")] public float targetRange = 2f;

    [Header("Ranged Attack")] public Ranged ranged;

    [Header("Animations")] public Animations animations;

    // 计时器
    private float lastShot;
    private Quaternion randomRotation;
    private float lastRandomRotation = -10f;
    private float lastJump;

    // 组件引用
    private Animator animator;
    private Transform playerTransform;

    /// <summary>
    /// 确保调用父类的 Awake 初始化 rigidbody
    /// </summary>
    public override void Awake()
    {
        base.Awake(); // 调用 Entity.Awake() 初始化 rigidbody

        // 确认 rigidbody 已获取
        if (rigidbody == null)
        {
            rigidbody = GetComponent<Rigidbody>();
        }
    }

    /// <summary>
    /// 初始化敌人，获取组件引用
    /// </summary>
    private void Start()
    {
        animator = GetComponentInChildren<Animator>();

        GameObject player = GameObject.FindGameObjectWithTag("Player");
        if (player != null)
        {
            playerTransform = player.transform;
        }
    }


    /// <summary>
    /// 每帧更新敌人AI逻辑
    /// </summary>
    private void Update()
    {
        // 如果玩家引用丢失，尝试重新查找
        if (playerTransform == null)
        {
            GameObject player = GameObject.FindGameObjectWithTag("Player");
            if (player != null)
            {
                playerTransform = player.transform;
            }
            else
            {
                // 没有玩家，播放待机动画
                PlayIdleAnimation();
                return;
            }
        }

        float distanceToPlayer = Vector3.Distance(playerTransform.position, transform.position);

        // 玩家在感知范围内
        if (distanceToPlayer < range)
        {
            HandlePlayerInRange(distanceToPlayer);
        }
        else
        {
            // 玩家不在范围内，随机巡逻
            HandlePatrol();
        }
    }

    /// <summary>
    /// 处理玩家在感知范围内的行为
    /// </summary>
    /// <param name="distanceToPlayer">与玩家的距离</param>
    private void HandlePlayerInRange(float distanceToPlayer)
    {
        // 还没到攻击距离，继续追踪
        if (distanceToPlayer > targetRange)
        {
            ChasePlayer();
        }
        else
        {
            // 到达攻击距离，停止移动
            PlayIdleAnimation();
        }

        // 检查远程攻击
        if (distanceToPlayer < ranged.shotRange)
        {
            TryRangedAttack();
        }
    }

    /// <summary>
    /// 追踪玩家
    /// </summary>
    private void ChasePlayer()
    {
        // 计算朝向玩家的方向（忽略Y轴）
        Vector3 target = playerTransform.position - transform.position;
        target.y = 0;

        // 旋转朝向玩家
        if (target != Vector3.zero)
        {
            transform.rotation = Quaternion.LookRotation(target, Vector3.up);
        }

        // 向前移动
        transform.Translate(Vector3.forward * Time.deltaTime * speed);

        // 播放行走动画
        PlayWalkAnimation();

        // 检查是否需要跳跃（遇到障碍物）
        CheckJump();
    }

    /// <summary>
    /// 尝试进行远程攻击
    /// </summary>
    private void TryRangedAttack()
    {
        if (Time.time > lastShot + ranged.shotInterval)
        {
            lastShot = Time.time;

            if (ranged.shotPrefab != null)
            {
                GameObject shot = Instantiate(ranged.shotPrefab, transform.position, Quaternion.identity);
                shot.transform.rotation = Quaternion.LookRotation(playerTransform.position - shot.transform.position);
            }
        }
    }

    /// <summary>
    /// 处理随机巡逻行为
    /// </summary>
    private void HandlePatrol()
    {
        // 每10秒选择一个新的随机方向
        if (Time.time > lastRandomRotation + 10f)
        {
            lastRandomRotation = Time.time;
            randomRotation = Quaternion.Euler(0, Random.Range(0, 360), 0);
        }

        // 在前6秒内向该方向移动
        if (Time.time < lastRandomRotation + 6f)
        {
            transform.rotation = randomRotation;
            transform.Translate(Vector3.forward * Time.deltaTime * speed);

            PlayWalkAnimation();
            CheckJump();
        }
        else
        {
            // 后4秒待机
            PlayIdleAnimation();
        }
    }

    /// <summary>
    /// 检查前方是否有障碍物，如果有则跳跃
    /// </summary>
    private void CheckJump()
    {
        if (rigidbody == null)
            return;

        Vector3 moveDirection = transform.forward;
        moveDirection.y = 0;
        moveDirection.Normalize();

        Vector3 rayOrigin = transform.position + Vector3.up * 0.005f;
        float rayDistance = 1.0f;

        Debug.DrawRay(rayOrigin, moveDirection * rayDistance, Color.green, 0.1f);

        RaycastHit hit;
        if (Physics.Raycast(rayOrigin, moveDirection, out hit, rayDistance))
        {
            if (hit.collider.CompareTag("Block"))
            {
                // 跳跃冷却检查
                if (Time.time < lastJump + 0.5f) return;

                // 检查Y轴速度
                if (rigidbody.linearVelocity.y > 0.5f) return;

                // 执行跳跃
                lastJump = Time.time;
                rigidbody.linearVelocity = new Vector3(
                    rigidbody.linearVelocity.x,
                    0,
                    rigidbody.linearVelocity.z
                );
                rigidbody.AddForce(Vector3.up * jump, ForceMode.Impulse);
            }
        }
    }

    /// <summary>
    /// 播放行走动画
    /// </summary>
    private void PlayWalkAnimation()
    {
        if (animator != null && animations.walk != null)
        {
            animator.Play(animations.walk.name);
        }
    }

    /// <summary>
    /// 播放待机动画
    /// </summary>
    private void PlayIdleAnimation()
    {
        if (animator != null)
        {
            animator.Play("Idle");
        }
    }

    /// <summary>
    /// 碰撞检测，处理与玩家的接触伤害
    /// </summary>
    /// <param name="collision">碰撞信息</param>
    private void OnCollisionEnter(Collision collision)
    {
        if (collision.gameObject.CompareTag("Player"))
        {
            // TODO: 实现玩家生命值系统后，在这里调用玩家受伤方法
            // Player player = collision.gameObject.GetComponent<Player>();
            // if (player != null)
            // {
            //     player.TakeDamage(bodyDamage);
            // }
        }
    }

    /// <summary>
    /// 在编辑器中绘制调试射线
    /// </summary>
    private void OnDrawGizmos()
    {
        // 绘制前方检测射线
        Gizmos.color = Color.red;
        Gizmos.DrawRay(transform.position + Vector3.up * 0.5f, transform.forward);

        // 绘制感知范围
        Gizmos.color = Color.yellow;
        Gizmos.DrawWireSphere(transform.position, range);

        // 绘制攻击范围
        Gizmos.color = Color.red;
        Gizmos.DrawWireSphere(transform.position, targetRange);
    }
}

/// <summary>
/// 远程攻击配置结构体，用于定义敌人的远程攻击属性
/// </summary>
[System.Serializable]
public struct Ranged
{
    [Tooltip("远程攻击的射程")] public float shotRange;

    [Tooltip("射击间隔时间（秒）")] public float shotInterval;

    [Tooltip("投射物预制体")] public GameObject shotPrefab;
}

/// <summary>
/// 动画配置结构体，用于存储动画剪辑资源
/// </summary>
[System.Serializable]
public struct Animations
{
    [Tooltip("行走动画")] public AnimationClip walk;
}