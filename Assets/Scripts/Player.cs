using UnityEngine;

/// <summary>
/// 玩家控制类，继承自Entity。负责处理玩家的移动、视角旋转、跳跃、方块破坏与放置等核心功能。
/// 同时管理物理材质切换以优化角色在不同状态下的行为表现。
/// </summary>
public class Player : Entity
{
    public CameraSettings cameraSettings;
    public Block activeBlock;

    private float xRotation;
    private float yRotation;
    private bool grounded;
    private float breakSeconds;
    
    private Block targetBlock;
    private Block breakingBlock;
    private RaycastHit targetRaycastHit;
    
    [Header("Ground Detection")]
    // public float groundCheckDistance = 0.1f;
    public float groundCheckRadius = 0.3f;
    public LayerMask groundLayer;
    
    [Header("Gravity Settings")]
    public float gravityMultiplier = 2.5f;
    
    [Header("Edge Stand Settings")]
    [Tooltip("用于站立的物理材质（高摩擦力）")]
    public PhysicsMaterial standingMaterial;
    [Tooltip("用于移动的物理材质（低摩擦力）")]
    public PhysicsMaterial movingMaterial;
    [Tooltip("判定为移动的最小输入阈值")]
    public float moveInputThreshold = 0.1f;
    
    private CapsuleCollider capsuleCollider;
    private bool isMoving;
    private bool isTouchingWall;

    /// <summary>
    /// 初始化玩家设置：锁定并隐藏光标，获取组件引用，并初始化物理材质。
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
        
        capsuleCollider = GetComponent<CapsuleCollider>();
        
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
    /// 检测是否贴墙（侧面碰撞检测），通过从胶囊体中心向四个方向发射射线判断是否接触到墙体。
    /// </summary>
    private void CheckWallContact()
    {
        if (capsuleCollider == null) return;
        
        // 从胶囊体中心向四个方向检测墙壁
        float checkDistance = capsuleCollider.radius + 0.05f;
        Vector3 center = transform.position + capsuleCollider.center;
        
        // 检测前后左右四个方向
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
        
        // 获取移动输入
        float horizontalInput = Mathf.Abs(Input.GetAxis("Horizontal"));
        float verticalInput = Mathf.Abs(Input.GetAxis("Vertical"));
        isMoving = horizontalInput > moveInputThreshold || verticalInput > moveInputThreshold;
        
        // 决定使用哪种材质
        bool shouldUseStandingMaterial = false;
        
        if (grounded && !isMoving)
        {
            // 站在地面且不移动 → 高摩擦力（防止边缘滑落）
            shouldUseStandingMaterial = true;
        }
        else if (!grounded && isTouchingWall)
        {
            // 空中且贴墙 → 低摩擦力（防止卡墙）
            shouldUseStandingMaterial = false;
        }
        else if (isMoving)
        {
            // 正在移动 → 低摩擦力（顺滑移动）
            shouldUseStandingMaterial = false;
        }
        
        // 应用材质
        capsuleCollider.material = shouldUseStandingMaterial ?  standingMaterial : movingMaterial;
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
    /// 主循环中的每帧更新逻辑，包括旋转、移动、跳跃、地面检测以及交互操作。
    /// </summary>
    private void Update()
    {
        // 物品栏打开时禁用游戏操作
        if (CheckInventory())
        {
            return; // 跳过所有游戏逻辑
        }
        
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckGrounded();
        
        CheckTargetBlock();
        
        CheckBreakAndPlace();
    }
    
    /// <summary>
    /// 检查物品栏状态并在打开时禁用相关游戏操作
    /// </summary>
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
    private void CheckRotation() {
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

        if (Input.GetButtonDown("Jump"))
        {
            Jump();
        }
    }
    
    /// <summary>
    /// 检查玩家的破坏和放置方块输入操作
    /// </summary>
    private void CheckBreakAndPlace() 
    {
        // 检测左键持续按下状态，用于破坏方块
        if (Input.GetButton("Fire1"))
        {
            TryBreakBlock();
        }
        else
        {
            // 左键未按下时，取消正在进行的破坏操作
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
                breakingBlock = null;
            }
            breakSeconds = 0; 
        }
        
        // 检测右键按下事件，用于放置方块
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
        //每一帧必须先清空目标 清除上一帧的方块引用
        targetBlock = null;

        RaycastHit hit;
        Ray ray = cameraSettings.camera.ScreenPointToRay(Input.mousePosition);
        
        // 如果射线没打中任何东西，逻辑正确结束
        if (Physics.Raycast(ray, out hit)) 
        {
            // 获取命中的物体
            Transform objectHit = hit.transform;
            Block blockComponent = objectHit.GetComponent<Block>();

            // 如果命中的不是方块，直接返回
            if (blockComponent == null)
                return;
            // 如果距离超过操作范围，直接返回
            if (Vector3.Distance(transform.position, objectHit.position) > range)
                return;
            
            // 只有完全符合条件，才赋值
            targetRaycastHit = hit;
            targetBlock = blockComponent;
        }
    }

    /// <summary>
    /// 尝试持续破坏目标方块，根据按住时间累积进度直到完成破坏。
    /// </summary>
    private void TryBreakBlock() 
    {
        // 若无目标方块，则重置破坏时间和引用
        if (!targetBlock)
        {
            // 如果之前有正在破坏的方块，通知它停止
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
                breakingBlock = null;
            }
            breakSeconds = 0;
            return;
        }

        // 如果正在破坏的方块与当前目标不同，则重新开始计时
        if (breakingBlock != targetBlock)
        {
            // 通知上一个方块停止特效
            if (breakingBlock != null)
            {
                breakingBlock.CancelBreak();
            }
            breakSeconds = 0;
        }
        
        breakingBlock = targetBlock;
        breakSeconds += Time.deltaTime;
        
        // 调用方块自身的破坏尝试逻辑
        bool breakSuccess = targetBlock.TryBreak(breakSeconds);
        
        // 破坏成功后重置计时器
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
        // 若没有有效目标方块则不执行放置逻辑
        if (!targetBlock)
            return;
    
        // 获取碰撞点的法线
        Vector3 hitNormal = targetRaycastHit.normal;
    
        // 将法线取整，消除浮点数误差
        Vector3 placeDirection = new Vector3(
            Mathf.Round(hitNormal.x),
            Mathf.Round(hitNormal.y),
            Mathf.Round(hitNormal.z)
        );
        // 计算新方块的目标位置
        Vector3 newBlockPosition = targetBlock.transform.position + placeDirection;
        // 二次距离检测，检查新放置的位置是否超出了玩家的 range
        if (Vector3.Distance(transform.position, newBlockPosition) > range)
        {
            return;
        }
        // 玩家防卡死检测，检查新位置是否有玩家（或任何其他物理实体）
        if (Physics.CheckBox(newBlockPosition, Vector3.one * 0.45f, Quaternion.identity, LayerMask.GetMask("Default", "Player")))
        {
            return;
        }
        
        GameObject block = Instantiate(activeBlock.gameObject);
        block.transform.position = newBlockPosition;
    }
    
    // private void OnDrawGizmosSelected()
    // {
    //     CapsuleCollider capsule = GetComponent<CapsuleCollider>();
    //     if (capsule != null)
    //     {
    //         // 地面检测可视化
    //         Gizmos.color = grounded ? Color.green : Color.red;
    //         Vector3 footPosition = transform.position + capsule.center - Vector3.up * (capsule.height / 2f);
    //         Vector3 checkPosition = footPosition + Vector3.down * 0.1f;
    //         Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
    //         
    //         // 墙壁检测可视化
    //         Gizmos.color = isTouchingWall ? Color.yellow : Color.cyan;
    //         float checkDistance = capsule.radius + 0.05f;
    //         Vector3 center = transform.position + capsule.center;
    //         Gizmos.DrawRay(center, transform.forward * checkDistance);
    //         Gizmos.DrawRay(center, -transform.forward * checkDistance);
    //         Gizmos.DrawRay(center, transform.right * checkDistance);
    //         Gizmos.DrawRay(center, -transform.right * checkDistance);
    //     }
    // }
    
    // private void OnDrawGizmos()
    // {
    //     if (targetBlock != null)
    //     {
    //         Gizmos.color = Color.yellow;
    //         Gizmos.DrawWireCube(targetBlock.transform.position, Vector3.one);
    //     }
    //     
    //     if (targetBlock != null && Input.GetButton("Fire2"))
    //     {
    //         Vector3 hitNormal = targetRaycastHit.normal;
    //         Vector3 placeDirection = new Vector3(
    //             Mathf.Round(hitNormal.x),
    //             Mathf.Round(hitNormal.y),
    //             Mathf.Round(hitNormal.z)
    //         );
    //         Vector3 placePosition = targetBlock.transform.position + placeDirection;
    //         
    //         Gizmos.color = Color.green;
    //         Gizmos.DrawCube(placePosition, Vector3.one * 0.9f);
    //     }
    // }
    
    /// <summary>
    /// 存储摄像机相关配置信息的数据结构。
    /// 包含摄像机组件引用及其横向和纵向灵敏度参数。
    /// </summary>
    [System.Serializable]
    public struct CameraSettings 
    {
        public Camera camera;               // 当前使用的摄像机组件
        public float sensitivityX;          // 横向旋转灵敏度
        public float sensitivityY;          // 纵向旋转灵敏度
    }
}