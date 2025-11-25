using UnityEngine;

/// <summary>
/// 玩家控制类，继承自Entity。用于处理玩家角色的基本行为：移动、跳跃、视角旋转以及地面检测。
/// </summary>
public class Player : Entity
{
    public CameraSettings cameraSettings;

    private float xRotation;
    private float yRotation;
    private bool grounded;

    [Header("Ground Detection")]
    public float groundCheckDistance = 0.1f;
    public float groundCheckRadius = 0.3f;   
    public LayerMask groundLayer;

    /// <summary>
    /// 初始化方法，在游戏开始时调用一次。
    /// 锁定并隐藏鼠标光标以支持第一人称视角控制。
    /// </summary>
    private void Start()
    {
        Cursor.lockState = CursorLockMode.Locked;
        Cursor.visible = false;
    }

    /// <summary>
    /// 每帧更新逻辑，依次执行以下操作：
    /// 1. 视角旋转检查（CheckRotation）
    /// 2. 移动输入处理（CheckMovement）
    /// 3. 跳跃输入处理（CheckJump）
    /// 4. 地面状态检测（CheckGrounded）
    /// </summary>
    private void Update()
    {
        CheckRotation();
        CheckMovement();
        CheckJump();
        CheckGrounded();
    }
    
    /// <summary>
    /// 使用球形检测判断玩家是否站在地面上。
    /// 检测点位于角色胶囊碰撞体底部略微向下偏移的位置。
    /// 结果存储在变量 grounded 中。
    /// </summary>
    private void CheckGrounded()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        Vector3 footPosition = transform.position + capsule.center - Vector3.up * (capsule.height / 2f);
        Vector3 checkPosition = footPosition + Vector3.down * 0.1f;
        grounded = Physics.CheckSphere(checkPosition, groundCheckRadius, groundLayer);
    }

    /// <summary>
    /// 处理摄像机与玩家的旋转逻辑。
    /// 根据鼠标输入调整摄像机俯仰角（xRotation）和玩家水平朝向（yRotation）。
    /// 并将摄像机保持在玩家子对象中以便跟随。
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
    /// 处理玩家的基础移动逻辑。
    /// 获取水平和垂直方向输入，并结合速度设置刚体线性速度。
    /// 输入被转换为世界坐标系中的方向进行应用。
    /// </summary>
    private void CheckMovement() {
        rigidbody.linearVelocity = new Vector3(Input.GetAxis("Horizontal") * speed,
            rigidbody.linearVelocity.y, Input.GetAxis("Vertical") * speed);

        rigidbody.linearVelocity = transform.TransformDirection(rigidbody.linearVelocity);
    }

    /// <summary>
    /// 判断当前是否可以跳跃，并响应跳跃按键按下事件。
    /// 只有当玩家处于地面状态且按下跳跃键时才会触发跳跃动作。
    /// </summary>
    private void CheckJump() {
        if (!grounded) { return; }

        if (Input.GetButtonDown("Jump")) {
            Jump();
        }
    }

    /// <summary>
    /// 在编辑器选中该对象时绘制辅助可视化图形。
    /// 显示地面检测范围的线框球体，颜色根据是否接触地面而变化（绿色表示接地，红色表示未接地）。
    /// </summary>
    private void OnDrawGizmosSelected()
    {
        CapsuleCollider capsule = GetComponent<CapsuleCollider>();
        if (capsule != null)
        {
            Gizmos.color = grounded ? Color.green : Color.red;
            Vector3 footPosition = transform.position + capsule.center - Vector3.up * (capsule.height / 2f);
            Vector3 checkPosition = footPosition + Vector3.down * 0.1f;
            Gizmos.DrawWireSphere(checkPosition, groundCheckRadius);
        }
    }

    /// <summary>
    /// 存储摄像机相关配置信息的数据结构。
    /// 包含摄像机组件引用及XY轴灵敏度设定。
    /// </summary>
    [System.Serializable]
    public struct CameraSettings {
        public Camera camera;
        public float sensitivityX;
        public float sensitivityY;
    }
}