using UnityEngine;

/// <summary>
/// 方块类，用于处理方块的破坏逻辑和粒子效果
/// </summary>
public class Block : MonoBehaviour
{

    public float durabilitySeconds;
    public ParticleSystem breakingParticlesPrefab;

    private ParticleSystem breakingParticles;
    private float lastBreakProgress;

    /// <summary>
    /// 每帧更新函数，用于处理破坏粒子效果的销毁逻辑
    /// </summary>
    private void Update() {
        // 如果存在破坏粒子效果，检查是否需要销毁
        if (breakingParticles) {
            // 如果距离上次破坏进度已经超过0.1秒，则销毁粒子效果
            if (Time.time > lastBreakProgress + .1f)
            {
                CancelBreak();
            }
        }
    }

    /// <summary>
    /// 尝试破坏方块
    /// </summary>
    /// <param name="breakSeconds">破坏所用的时间（秒）</param>
    /// <returns>如果破坏成功则返回true，否则返回false</returns>
    public bool TryBreak(float breakSeconds) {
        lastBreakProgress = Time.time;
        // 如果当前没有破坏粒子效果且预制体存在，则创建粒子效果
        if (!breakingParticles && breakingParticlesPrefab) {
            breakingParticles = Instantiate(breakingParticlesPrefab);
            breakingParticles.transform.position = transform.position;
        }
        // 如果破坏时间超过耐久度，则完全破坏方块
        if (breakSeconds > durabilitySeconds) {
            Break();
            return true;
        }
        return false;
    }
    
    /// <summary>
    /// 取消破坏操作并销毁正在播放的粒子效果
    /// </summary>
    public void CancelBreak()
    {
        if (breakingParticles)
        {
            Destroy(breakingParticles.gameObject); // 确保销毁的是 GameObject
            breakingParticles = null; // 必须置空引用
        }
    }

    /// <summary>
    /// 完全破坏方块，销毁粒子效果和游戏对象
    /// </summary>
    public void Break() {
        CancelBreak();
        // 销毁当前游戏对象
        Destroy(gameObject);
    }
}