using UnityEngine;

/// <summary>
/// 玩家控制类，继承自Entity。负责处理玩家的移动、视角旋转、跳跃、方块破坏与放置等核心功能。
/// 同时管理物理材质切换以优化角色在不同状态下的行为表现。
/// </summary>
public class Player : Entity
{
    public CameraSettings cameraSettings;
    public Block activeBlock;

    /// <summary>
    /// 玩家的库存系统引用
    /// </summary>
    public Inventory inventory;

    private float xRotation;
    private float yRotation;
    public bool grounded;
    private float breakSeconds;

    private Block targetBlock;
    private Block breakingBlock;
    private RaycastHit targetRaycastHit;

    [Header("Ground Detection")] public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;

    [Header("Gravity Settings")] public float gravityMultiplier = 2.5f;

    [Header("Edge Stand Settings")] [Tooltip("用于站立的物理材质（高摩擦力）")]
    public PhysicsMaterial standingMaterial;

    [Tooltip("用于移动的物理材质（低摩擦力）")] public PhysicsMaterial movingMaterial;
    [Tooltip("判定为移动的最小输入阈值")] public float moveInputThreshold = 0.1f;

    [Header("Jump Settings")] [Tooltip("跳跃冷却时间")]
    public float jumpCooldown = 0.2f;

    [Tooltip("允许跳跃的最大垂直速度")] public float maxVerticalSpeedToJump = 2f;

    private float lastJumpTime = -1f;

    private CapsuleCollider capsuleCollider;
    private bool isMoving;
    private bool isTouchingWall;

    [Header("Block Container")] [Tooltip("存放放置方块的容器名称")]
    public string blockContainerName = "Blocks";

    private Transform blockContainer;

    /// <summary>
    /// 初始化玩家设置：锁定并隐藏光标，获取组件引用，并初始化物理材质。
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        capsuleCollider = GetComponent<CapsuleCollider>();

        // 查找或创建方块容器
        InitBlockContainer();

        // 如果没有指定材质，自动创建
        if (standingMaterial == null)
        {
            standingMaterial = new PhysicsMaterial("Standing");
            standingMaterial.staticFriction = 1f;
            standingMaterial.dynamicFriction = 1f;
            standingMaterial.frictionCombine = PhysicsMaterialCombine.Maximum;
        }

        if (movingMaterial == null)
        {
            movingMaterial = new PhysicsMaterial("Moving");
            movingMaterial.staticFriction = 0f;
            movingMaterial.dynamicFriction = 0f;
            movingMaterial.frictionCombine = PhysicsMaterialCombine.Minimum;
        }
    }

    /// <summary>
    /// 固定更新逻辑，在FixedUpdate中调用重力增强、墙体接触检测及物理材质更新。
    /// </summary>
    private void FixedUpdate()
    {
        ApplyExtraGravity();
        CheckWallContact();
        UpdatePhysicsMaterial();
    }

    /// <summary>
    /// 主循环中的每帧更新逻辑，包括旋转、移动、跳跃、地面检测以及交互操作。
    /// </summary>
    private void Update()
    {
        if (CheckInventory())
        {
            return;
        }

        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckGrounded();

        CheckTargetBlock();

        CheckBreakAndPlace();
    }

    /// <summary>
    /// 初始化方块容器，查找或创建
    /// </summary>
    private void InitBlockContainer()
    {
        GameObject container = GameObject.Find(blockContainerName);

        if (container == null)
        {
            container = new GameObject(blockContainerName);
        }

        blockContainer = container.transform;
    }

    /// <summary>
    /// 检测是否贴墙（侧面碰撞检测），通过从胶囊体中心向四个方向发射射线判断是否接触到墙体。
    /// </summary>
    private void CheckWallContact()
    {
        if (capsuleCollider == null) return;

        float checkDistance = capsuleCollider.radius + 0.05f;
        Vector3 center = transform.position + capsuleCollider.center;

        Vector3[] directions =
        {
            transform.forward,
            -transform.forward,
            transform.right,
            -transform.right
        };

        isTouchingWall = false;
        foreach (var dir in directions)
        {
            if (Physics.Raycast(center, dir, checkDistance, groundLayer))
            {
                isTouchingWall = true;
                break;
            }
        }
    }

    /// <summary>
    /// 根据玩家当前的状态（是否着地、是否正在移动、是否贴墙）动态切换物理材质，
    /// 实现更自然的角色操控体验。
    /// </summary>
    private void UpdatePhysicsMaterial()
    {
        if (capsuleCollider == null) return;

        float horizontalInput = Mathf.Abs(Input.GetAxis("Horizontal"));
        float verticalInput = Mathf.Abs(Input.GetAxis("Vertical"));
        isMoving = horizontalInput > moveInputThreshold || verticalInput > moveInputThreshold;

        bool shouldUseStandingMaterial = false;

        if (grounded && !isMoving)
        {
            shouldUseStandingMaterial = true;
        }
        else if (!grounded && isTouchingWall)
        {
            shouldUseStandingMaterial = false;
        }
        else if (isMoving)
        {
            shouldUseStandingMaterial = false;
        }

        capsuleCollider.material = shouldUseStandingMaterial ? standingMaterial : movingMaterial;
    }

    /// <summary>
    /// 对刚体施加额外的重力效果，提升下落速度或空中控制感。
    /// </summary>
    private void ApplyExtraGravity()
    {
        if (rigidbody.useGravity)
        {
            Vector3 extraGravityForce = Physics.gravity * (gravityMultiplier - 1);
            rigidbody.AddForce(extraGravityForce, ForceMode.Acceleration);
        }
    }

    /// <summary>
    /// 检查物品栏状态并在打开时禁用相关游戏操作
    /// </summary>
    /// <returns>如果物品栏处于开启状态则返回true，否则返回false。</returns>
    private bool CheckInventory()
    {
        if (Inventory.open)
        {
            // 停止破坏方块
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
                breakingBlock = null;
            }

            breakSeconds = 0;

            // 停止玩家的水平移动，只保留垂直速度（重力）
            rigidbody.linearVelocity = new Vector3(0, rigidbody.linearVelocity.y, 0);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 检查玩家是否站在可行走的地面上，通过球形检测脚底区域实现。
    /// </summary>
    private void CheckGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 footPosition = transform.position + capsule.center - Vector3.up * (capsule.height / 2f);
        Vector3 checkPosition = footPosition + Vector3.down * 0.1f;
        grounded = Physics.CheckSphere(checkPosition, groundCheckRadius, groundLayer);
    }

    /// <summary>
    /// 处理鼠标输入来控制摄像机和玩家朝向的旋转。
    /// </summary>
    private void CheckRotation()
    {
        float mouseX = Input.GetAxisRaw("Mouse X") * Time.deltaTime * cameraSettings.sensitivityX;
        float mouseY = Input.GetAxisRaw("Mouse Y") * Time.deltaTime * cameraSettings.sensitivityY;

        yRotation += mouseX;
        xRotation -= mouseY;
        xRotation = Mathf.Clamp(xRotation, -90f, 90f);

        cameraSettings.camera.transform.rotation = Quaternion.Euler(xRotation, yRotation, 0);
        cameraSettings.camera.transform.SetParent(null);
        transform.rotation = Quaternion.Euler(0, yRotation, 0);
        cameraSettings.camera.transform.SetParent(transform);
    }

    /// <summary>
    /// 控制玩家水平方向上的移动速度，基于键盘输入进行位移计算。
    /// </summary>
    private void CheckMovement()
    {
        rigidbody.linearVelocity = new Vector3(Input.GetAxis("Horizontal") * speed,
            rigidbody.linearVelocity.y, Input.GetAxis("Vertical") * speed);

        rigidbody.linearVelocity = transform.TransformDirection(rigidbody.linearVelocity);
    }

    /// <summary>
    /// 判断是否满足跳跃条件并在按下跳跃键时执行跳跃动作。
    /// </summary>
    private void CheckJump()
    {
        if (!grounded)
        {
            return;
        }

        // 检查跳跃冷却
        if (Time.time - lastJumpTime < jumpCooldown)
        {
            return;
        }

        // 检查垂直速度，防止在被弹起时跳跃
        if (Mathf.Abs(rigidbody.linearVelocity.y) > maxVerticalSpeedToJump)
        {
            return;
        }

        if (Input.GetButtonDown("Jump"))
        {
            Jump();
            lastJumpTime = Time.time;
        }
    }

    /// <summary>
    /// 检查玩家的破坏和放置方块输入操作
    /// </summary>
    private void CheckBreakAndPlace()
    {
        if (Input.GetButton("Fire1"))
        {
            TryBreakBlock();
        }
        else
        {
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
                breakingBlock = null;
            }

            breakSeconds = 0;
        }

        if (Input.GetButtonDown("Fire2"))
        {
            TryPlaceBlock();
        }
    }

    /// <summary>
    /// 使用射线投射检测准心所指的目标方块对象。
    /// </summary>
    private void CheckTargetBlock()
    {
        targetBlock = null;

        RaycastHit hit;
        Ray ray = cameraSettings.camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out hit))
        {
            Transform objectHit = hit.transform;
            Block blockComponent = objectHit.GetComponent<Block>();

            if (blockComponent == null)
                return;
            if (Vector3.Distance(transform.position, objectHit.position) > range)
                return;

            targetRaycastHit = hit;
            targetBlock = blockComponent;
        }
    }

    /// <summary>
    /// 尝试持续破坏目标方块，根据按住时间累积进度直到完成破坏。
    /// </summary>
    private void TryBreakBlock()
    {
        if (!targetBlock)
        {
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
                breakingBlock = null;
            }

            breakSeconds = 0;
            return;
        }

        if (breakingBlock != targetBlock)
        {
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
            }

            breakSeconds = 0;
        }

        breakingBlock = targetBlock;
        breakSeconds += Time.deltaTime;

        bool breakSuccess = targetBlock.TryBreak(breakSeconds);

        if (breakSuccess)
        {
            breakSeconds = 0;
            breakingBlock = null;
        }
    }

    /// <summary>
    /// 在目标方块相邻位置尝试放置一个新的方块实例。
    /// </summary>
    void TryPlaceBlock()
    {
        if (!targetBlock)
            return;

        Vector3 hitNormal = targetRaycastHit.normal;

        Vector3 placeDirection = new Vector3(
            Mathf.Round(hitNormal.x),
            Mathf.Round(hitNormal.y),
            Mathf.Round(hitNormal.z)
        );
        Vector3 newBlockPosition = targetBlock.transform.position + placeDirection;
        if (Vector3.Distance(transform.position, newBlockPosition) > range)
        {
            return;
        }

        CapsuleCollider playerCapsule = GetComponent<CapsuleCollider>();
        float playerFootY = transform.position.y + playerCapsule.center.y - playerCapsule.height / 2f;

        float blockTopY = newBlockPosition.y + 0.5f;
        bool isPlacingBelowFeet = blockTopY <= playerFootY + 0.1f;

        Collider[] colliders = Physics.OverlapBox(newBlockPosition, Vector3.one * 0.45f, Quaternion.identity);

        foreach (Collider col in colliders)
        {
            if (col.gameObject == this.gameObject)
            {
                if (isPlacingBelowFeet)
                {
                    continue;
                }
                else
                {
                    return;
                }
            }
            else
            {
                return;
            }
        }

        // 确保容器存在
        if (blockContainer == null)
        {
            InitBlockContainer();
        }

        // 在容器下创建方块
        GameObject block = Instantiate(activeBlock.gameObject, blockContainer);
        block.transform.position = newBlockPosition;
    }

    /// <summary>
    /// 获取物品并添加到玩家库存中。
    /// 由掉落物（Drop）拾取时调用。
    /// </summary>
    /// <param name="item">要添加到库存的物品</param>
    public void GetItem(Item item)
    {
        if (inventory != null)
        {
            inventory.GetItem(item);
        }
    }

    /// <summary>
    /// 存储摄像机相关配置信息的数据结构。
    /// </summary>
    [System.Serializable]
    public struct CameraSettings
    {
        public Camera camera;
        public float sensitivityX;
        public float sensitivityY;
    }
}