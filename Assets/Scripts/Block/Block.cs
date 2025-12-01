using UnityEngine;

/// <summary>
/// 方块类，用于处理方块的破坏逻辑、粒子效果和掉落物
/// </summary>
public class Block : MonoBehaviour
{
    /// <summary>
    /// 方块的耐久度（破坏所需秒数）
    /// </summary>
    public float durabilitySeconds;

    /// <summary>
    /// 破坏时的粒子效果预制体
    /// </summary>
    public ParticleSystem breakingParticlesPrefab;

    /// <summary>
    /// 掉落物预制体
    /// </summary>
    public Drop dropPrefab;

    /// <summary>
    /// 掉落的物品
    /// </summary>
    public Item dropItem;

    private ParticleSystem breakingParticles;
    private float lastBreakProgress;

    /// <summary>
    /// 每帧更新函数，用于处理破坏粒子效果的销毁逻辑
    /// </summary>
    private void Update()
    {
        // 如果存在破坏粒子效果，检查是否需要销毁
        if (breakingParticles)
        {
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
    public bool TryBreak(float breakSeconds)
    {
        lastBreakProgress = Time.time;
        // 如果当前没有破坏粒子效果且预制体存在，则创建粒子效果
        if (!breakingParticles && breakingParticlesPrefab)
        {
            breakingParticles = Instantiate(breakingParticlesPrefab);
            breakingParticles.transform.position = transform.position;
        }

        // 如果破坏时间超过耐久度，则完全破坏方块
        if (breakSeconds > durabilitySeconds)
        {
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
            Destroy(breakingParticles.gameObject);
            breakingParticles = null;
        }
    }

    /// <summary>
    /// 完全破坏方块，生成掉落物，销毁粒子效果和游戏对象
    /// </summary>
    public void Break()
    {
        // 生成掉落物
        SpawnDrop();

        // 取消破坏效果
        CancelBreak();

        // 销毁当前游戏对象
        Destroy(gameObject);
    }

    /// <summary>
    /// 生成掉落物
    /// </summary>
    private void SpawnDrop()
    {
        // 如果没有掉落物预制体或掉落物品，则不生成
        if (!dropPrefab || !dropItem)
        {
            return;
        }

        // 在方块位置实例化掉落物
        Drop drop = Instantiate(dropPrefab, transform.position, Quaternion.identity);

        // 设置掉落物的模型和物品引用
        drop.modelPrefab = dropItem.model;
        drop.item = dropItem;
    }
}