using UnityEngine;

/// <summary>
/// 实体类，继承自MonoBehaviour，用于控制游戏中的物理实体
/// </summary>
public class Entity : MonoBehaviour
{
    public float speed;
    public float jump;
    public float range;

    protected new Rigidbody rigidbody;

    /// <summary>
    /// 唤醒时调用的方法，获取刚体组件
    /// </summary>
    public virtual void Awake() 
    {
        // 获取当前对象的Rigidbody组件
        rigidbody = GetComponent<Rigidbody>();
    }

    /// <summary>
    /// 执行跳跃动作，给刚体施加向上的力
    /// </summary>
    public void Jump() 
    {
        // 给刚体施加向上的冲量力实现跳跃效果
        rigidbody.AddForce(Vector3.up * jump, ForceMode.Impulse);
    }
}