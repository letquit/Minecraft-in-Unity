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

    [Header("UI")] 
    [Tooltip("所有UI界面")] 
    public UI[] uis;

    [Tooltip("HUD快捷栏引用")]
    public HUD hud;

    private Transform blockContainer;

    /// <summary>
    /// 初始化玩家设置：锁定并隐藏光标，获取组件引用，并初始化物理材质。
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;

        capsuleCollider = GetComponent<CapsuleCollider>();
        InitBlockContainer();
        InitPhysicsMaterials();
    }

    /// <summary>
    /// 初始化物理材质
    /// </summary>
    private void InitPhysicsMaterials()
    {
        if (standingMaterial == null)
        {
            standingMaterial = new PhysicsMaterial("Standing")
            {
                staticFriction = 1f,
                dynamicFriction = 1f,
                frictionCombine = PhysicsMaterialCombine.Maximum
            };
        }

        if (movingMaterial == null)
        {
            movingMaterial = new PhysicsMaterial("Moving")
            {
                staticFriction = 0f,
                dynamicFriction = 0f,
                frictionCombine = PhysicsMaterialCombine.Minimum
            };
        }
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
    /// 固定更新逻辑，在FixedUpdate中调用重力增强、墙体接触检测及物理材质更新。
    /// </summary>
    private void FixedUpdate()
    {
        ApplyExtraGravity();
        CheckWallContact();
        UpdatePhysicsMaterial();
    }

    /// <summary>
    /// 主循环中的每帧更新逻辑
    /// </summary>
    private void Update()
    {
        // 检测 E 键按下，切换背包显示状态
        if (Input.GetKeyDown(KeyCode.E))
        {
            TryToggleInventory();
        }

        // 检查背包是否打开，如果打开则暂停游戏逻辑
        if (UIOpen())
        {
            HandleUIOpenState();
            return;
        }

        // 游戏逻辑更新
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckGrounded();
        CheckTargetBlock();
        CheckBreakAndPlace();
    }

    /// <summary>
    /// 处理UI打开时的状态
    /// </summary>
    private void HandleUIOpenState()
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
    }

    /// <summary>
    /// 尝试切换物品栏，如果有其他UI打开则先关闭
    /// </summary>
    private void TryToggleInventory()
    {
        // 检查是否有其他UI打开
        if (uis != null)
        {
            foreach (UI ui in uis)
            {
                if (ui == null || ui == inventory) continue;

                if (ui.isOpen)
                {
                    ui.Toggle();
                    return;
                }
            }
        }

        // 如果没有其他UI打开，则切换背包
        if (inventory != null)
        {
            inventory.Toggle();
        }
    }

    /// <summary>
    /// 检查是否有任何UI界面处于打开状态
    /// </summary>
    /// <returns>如果有UI打开则返回true</returns>
    public bool UIOpen()
    {
        if (Inventory.open) return true;

        if (uis != null)
        {
            foreach (UI ui in uis)
            {
                if (ui != null && ui.isOpen)
                {
                    return true;
                }
            }
        }

        return false;
    }

    #region 物理系统

    /// <summary>
    /// 检测是否贴墙（侧面碰撞检测）
    /// </summary>
    private void CheckWallContact()
    {
        if (capsuleCollider == null) return;

        float checkDistance = capsuleCollider.radius + 0.05f;
        Vector3 center = transform.position + capsuleCollider.center;

        Vector3[] directions = { transform.forward, -transform.forward, transform.right, -transform.right };

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
    /// 根据玩家当前的状态动态切换物理材质
    /// </summary>
    private void UpdatePhysicsMaterial()
    {
        if (capsuleCollider == null) return;

        float horizontalInput = Mathf.Abs(Input.GetAxis("Horizontal"));
        float verticalInput = Mathf.Abs(Input.GetAxis("Vertical"));
        isMoving = horizontalInput > moveInputThreshold || verticalInput > moveInputThreshold;

        bool shouldUseStandingMaterial = grounded && !isMoving && !(!grounded && isTouchingWall);

        capsuleCollider.material = shouldUseStandingMaterial ? standingMaterial : movingMaterial;
    }

    /// <summary>
    /// 对刚体施加额外的重力效果
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
    /// 检查玩家是否站在可行走的地面上
    /// </summary>
    private void CheckGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 footPosition = transform.position + capsule.center - Vector3.up * (capsule.height / 2f);
        Vector3 checkPosition = footPosition + Vector3.down * 0.1f;
        grounded = Physics.CheckSphere(checkPosition, groundCheckRadius, groundLayer);
    }

    #endregion

    #region 移动与旋转

    /// <summary>
    /// 处理鼠标输入来控制摄像机和玩家朝向的旋转
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
    /// 控制玩家水平方向上的移动速度
    /// </summary>
    private void CheckMovement()
    {
        rigidbody.linearVelocity = new Vector3(
            Input.GetAxis("Horizontal") * speed,
            rigidbody.linearVelocity.y,
            Input.GetAxis("Vertical") * speed
        );

        rigidbody.linearVelocity = transform.TransformDirection(rigidbody.linearVelocity);
    }

    /// <summary>
    /// 判断是否满足跳跃条件并在按下跳跃键时执行跳跃动作
    /// </summary>
    private void CheckJump()
    {
        if (!grounded) return;
        if (Time.time - lastJumpTime < jumpCooldown) return;
        if (Mathf.Abs(rigidbody.linearVelocity.y) > maxVerticalSpeedToJump) return;

        if (Input.GetButtonDown("Jump"))
        {
            Jump();
            lastJumpTime = Time.time;
        }
    }

    #endregion

    #region 方块交互

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
    /// 使用射线投射检测准心所指的目标方块对象
    /// </summary>
    private void CheckTargetBlock()
    {
        targetBlock = null;

        Ray ray = cameraSettings.camera.ScreenPointToRay(Input.mousePosition);

        if (Physics.Raycast(ray, out RaycastHit hit))
        {
            Transform objectHit = hit.transform;
            Block blockComponent = objectHit.GetComponent<Block>();

            if (blockComponent == null) return;
            if (Vector3.Distance(transform.position, objectHit.position) > range) return;

            targetRaycastHit = hit;
            targetBlock = blockComponent;
        }
    }

    /// <summary>
    /// 尝试持续破坏目标方块
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
    /// 尝试在目标方块的相邻位置放置一个新的方块实例。
    /// 如果目标方块是工作台类型，则打开其UI界面而不是放置新方块。
    /// </summary>
    private void TryPlaceBlock()
    {
        // 如果没有目标方块，直接返回
        if (! targetBlock) return;

        // 检查是否点击了工作台方块
        CraftingTableBlock craftingTableBlock = targetBlock.GetComponent<CraftingTableBlock>();
        if (craftingTableBlock != null)
        {
            craftingTableBlock.OpenUI();
            return;
        }

        // 检查是否有选中的物品可以放置
        if (hud != null && ! hud.HasSelectedItem())
        {
            // 快捷栏没有选中物品，不能放置
            return;
        }
    
        // 检查当前激活的方块是否有效
        if (activeBlock == null)
        {
            return;
        }

        // 根据射线碰撞法线计算新方块应放置的位置
        Vector3 hitNormal = targetRaycastHit.normal;
        Vector3 placeDirection = new Vector3(
            Mathf.Round(hitNormal.x),
            Mathf.Round(hitNormal.y),
            Mathf.Round(hitNormal.z)
        );
        Vector3 newBlockPosition = targetBlock.transform.position + placeDirection;

        // 检查玩家与目标位置之间的距离是否超出范围
        if (Vector3.Distance(transform.position, newBlockPosition) > range) return;

        // 检查该位置是否允许放置方块（如无遮挡、无其他方块等）
        if (! CanPlaceBlockAt(newBlockPosition)) return;

        // 确保用于存放方块的游戏对象容器已初始化
        if (blockContainer == null)
        {
            InitBlockContainer();
        }

        // 实例化新的方块并设置其世界坐标
        GameObject block = Instantiate(activeBlock.gameObject, blockContainer);
        block.transform.position = newBlockPosition;
    
        // 放置成功后减少快捷栏中对应物品的数量
        if (hud != null)
        {
            hud.DecreaseSelectedItem(1);
        }
    }

    /// <summary>
    /// 检查指定位置是否可以放置方块
    /// </summary>
    /// <param name="position">待检查的位置</param>
    /// <returns>是否可以放置</returns>
    private bool CanPlaceBlockAt(Vector3 position)
    {
        CapsuleCollider playerCapsule = GetComponent<CapsuleCollider>();
        float playerFootY = transform.position.y + playerCapsule.center.y - playerCapsule.height / 2f;
        float blockTopY = position.y + 0.5f;
        bool isPlacingBelowFeet = blockTopY <= playerFootY + 0.1f;

        Collider[] colliders = Physics.OverlapBox(position, Vector3.one * 0.45f, Quaternion.identity);

        foreach (Collider col in colliders)
        {
            if (col.gameObject == this.gameObject)
            {
                if (!isPlacingBelowFeet) return false;
            }
            else
            {
                return false;
            }
        }

        return true;
    }

    #endregion

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
    /// 存储摄像机相关配置信息的数据结构
    /// </summary>
    [System.Serializable]
    public struct CameraSettings
    {
        public Camera camera;
        public float sensitivityX;
        public float sensitivityY;
    }
}