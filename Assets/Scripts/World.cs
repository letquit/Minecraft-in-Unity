using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

/// <summary>
/// 世界生成器类，用于在Unity编辑器模式下生成一个由不同方块构成的地形。
/// 使用分形叠加（FBM）和双层柏林噪声生成更自然的地形，包含高山和深谷。
/// </summary>
[ExecuteInEditMode]
public class World : MonoBehaviour
{
    /// <summary>
    /// 地形的宽度（X轴方向）
    /// </summary>
    public int width;

    /// <summary>
    /// 地形的高度（Y轴方向）
    /// </summary>
    public int height;

    /// <summary>
    /// 地形的深度（Z轴方向）
    /// </summary>
    public int depth;

    /// <summary>
    /// 随机种子，用于控制Perlin噪声生成地形时的一致性
    /// </summary>
    public int seed;

    /// <summary>
    /// 树木生成概率（0-1范围）
    /// </summary>
    [Range(0, 1)] public float treeProbability;

    /// <summary>
    /// 包含各种类型方块预制体的数据结构
    /// </summary>
    public Blocks blocks;

    /// <summary>
    /// 噪声设置
    /// </summary>
    [Header("Noise Settings")] public NoiseSettings noiseSettings = NoiseSettings.Default;

    /// <summary>
    /// 当前World实例的静态引用，方便全局访问
    /// </summary>
    public static World instance;

    /// <summary>
    /// 在Awake阶段设置当前实例为全局唯一实例
    /// </summary>
    private void Awake()
    {
        instance = this;
    }

    /// <summary>
    /// 实例化方块，在编辑器模式下保持Prefab连接，运行时使用普通实例化。
    /// </summary>
    /// <param name="prefab">要实例化的方块预制体</param>
    /// <param name="position">放置位置</param>
    /// <returns>实例化的方块</returns>
    private Block InstantiateBlock(Block prefab, Vector3 position)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 编辑器模式：使用 PrefabUtility 保持 Prefab 连接
            Block block = (Block)PrefabUtility.InstantiatePrefab(prefab);
            block.transform.position = position;
            block.transform.rotation = Quaternion.identity;
            block.transform.SetParent(transform);
            return block;
        }
        else
        {
            // 运行时：使用普通实例化
            Block block = Instantiate(prefab, position, Quaternion.identity);
            block.transform.SetParent(transform);
            return block;
        }
#else
        // 非编辑器构建：使用普通实例化
        Block block = Instantiate(prefab, position, Quaternion.identity);
        block.transform.SetParent(transform);
        return block;
#endif
    }

    /// <summary>
    /// 使用分形叠加（FBM）计算噪声值。
    /// 将多层不同频率和振幅的柏林噪声叠加，生成更自然的地形。
    /// 同时结合两个偏移的柏林噪声，增加差异性，产生高山和深谷。
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="z">Z坐标</param>
    /// <returns>归一化的噪声值（0-1范围）</returns>
    private float GetFractalNoise(float x, float z)
    {
        float amplitude = 1f; // 当前层的振幅
        float frequency = 1f; // 当前层的频率
        float noiseValue = 0f; // 累积噪声值
        float maxValue = 0f; // 用于归一化的最大可能值

        float scale = noiseSettings.scale;
        if (scale <= 0) scale = 0.0001f; // 防止除零

        // 分形叠加：多层噪声叠加
        for (int i = 0; i < noiseSettings.octaves; i++)
        {
            // 计算采样坐标
            float sampleX = (x + seed) / scale * frequency;
            float sampleZ = (z + seed) / scale * frequency;

            // 第一层柏林噪声（主噪声）
            float primaryNoise = Mathf.PerlinNoise(sampleX, sampleZ);

            // 第二层柏林噪声（使用偏移量增加差异性）
            float secondarySampleX = sampleX + noiseSettings.secondaryOffset.x;
            float secondarySampleZ = sampleZ + noiseSettings.secondaryOffset.y;
            float secondaryNoise = Mathf.PerlinNoise(secondarySampleX, secondarySampleZ);

            // 结合两个噪声：相乘后重新映射（产生更多极端值，形成高山和深谷）
            float combinedNoise = primaryNoise * secondaryNoise * 2f;

            // 将噪声值从 [0,1] 映射到 [-1,1]，使地形可以有高山和深谷
            combinedNoise = combinedNoise * 2f - 1f;

            // 叠加当前层
            noiseValue += combinedNoise * amplitude;
            maxValue += amplitude;

            // 为下一层调整振幅和频率
            amplitude *= noiseSettings.persistence; // 振幅衰减
            frequency *= noiseSettings.lacunarity; // 频率增加
        }

        // 归一化到 [0, 1] 范围
        noiseValue = (noiseValue / maxValue + 1f) * 0.5f;

        return noiseValue;
    }

    /// <summary>
    /// 获取指定坐标的地形高度
    /// </summary>
    /// <param name="x">X坐标</param>
    /// <param name="z">Z坐标</param>
    /// <returns>地形高度</returns>
    private int GetTerrainHeight(float x, float z)
    {
        float noiseValue = GetFractalNoise(x, z);
        int terrainHeight = Mathf.RoundToInt(noiseValue * noiseSettings.heightMultiplier);
        return terrainHeight;
    }

    /// <summary>
    /// 根据指定参数生成整个地形。首先清理旧对象，然后使用分形噪声生成地表草方块，
    /// 并在其下方依次创建土层、石层和基岩层，最后随机生成树木。
    /// </summary>
    public void Generate()
    {
        Clean();
        Random.InitState(seed);

        // 使用双重循环遍历X和Z轴上的每个位置
        for (float x = -(width / 2); x < width / 2; x++)
        {
            for (float z = -(depth / 2); z < depth / 2; z++)
            {
                // 使用分形噪声计算该点的地表高度
                int y = GetTerrainHeight(x, z);
                InstantiateBlock(blocks.grass, new Vector3(x, y, z));

                // 创建从草方块向下延伸两格的泥土柱
                Vector3 dirtPosition = new Vector3(x, y, z) - new Vector3(0, 2, 0);
                CreateBlockLine(new Vector3(x, y, z), dirtPosition, blocks.dirt);

                // 创建从泥土底部到设定最低高度的石头柱
                Vector3 stonePosition = new Vector3(x, -height + 1, z);
                CreateBlockLine(dirtPosition, stonePosition, blocks.stone);

                // 创建最底层的基岩柱
                Vector3 bedrockPosition = new Vector3(x, -height, z);
                CreateBlockLine(stonePosition, bedrockPosition, blocks.bedrock);

                // 根据概率生成树木
                Vector3 treePosition = new Vector3(x, y, z);
                if (Random.Range(0f, 1f) < treeProbability)
                {
                    CreateTree(treePosition);
                }
            }
        }

#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 标记场景为已修改，确保可以保存
            UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(
                UnityEditor.SceneManagement.EditorSceneManager.GetActiveScene());
        }
#endif
    }

    /// <summary>
    /// 在两个三维坐标之间创建一条垂直线段，并沿线填充指定类型的方块。
    /// </summary>
    /// <param name="from">起始点坐标</param>
    /// <param name="to">结束点坐标</param>
    /// <param name="prefab">要实例化的方块预制体</param>
    public void CreateBlockLine(Vector3 from, Vector3 to, Block prefab)
    {
        Vector3 position = from;
        do
        {
            // 调整当前位置使其逐步接近目标点
            if (position.y < to.y)
            {
                position.y++;
            }

            if (position.y > to.y)
            {
                position.y--;
            }

            // 实例化并放置方块
            InstantiateBlock(prefab, new Vector3(position.x, position.y, position.z));
        } while (position != to); // 循环直到到达终点
    }

    /// <summary>
    /// 在指定位置创建一棵普通橡树，包含树干（原木）和树叶。
    /// 模拟 Minecraft 普通橡树的生成结构：
    /// - 树干：1×1，高度 4-6 格随机
    /// - 树冠：大致 5×5×4 的叶子体积，带有随机空洞
    /// 每棵树会创建一个独立的 GameObject 作为容器。
    /// </summary>
    /// <param name="position">树木底部位置（地表位置）</param>
    public void CreateTree(Vector3 position)
    {
        // 创建树的父物体容器
        GameObject treeContainer = new GameObject($"Tree_{position.x}_{position.z}");
        treeContainer.transform.position = position;
        treeContainer.transform.SetParent(transform);

        // 随机树干高度：4-6格
        int trunkHeight = Random.Range(4, 7);

        // 创建树干（从地表向上）
        CreateTreeBlockLine(position, position + Vector3.up * trunkHeight, blocks.log, treeContainer.transform);

        // 生成树冠
        // 树冠共4层：从树干顶部向上2层，向下1层
        // 层级：-1, 0, 1, 2（相对于树干顶部）

        // === 第一层（最下层，y = trunkHeight - 1）===
        // 较大的 5×5 区域，四角有概率挖空
        GenerateLeafLayer(position, trunkHeight - 1, 2, 0.3f, true, treeContainer.transform);

        // === 第二层（树干顶部层，y = trunkHeight）===
        // 5×5 区域，较为密集，四角有概率挖空
        GenerateLeafLayer(position, trunkHeight, 2, 0.2f, true, treeContainer.transform);

        // === 第三层（y = trunkHeight + 1）===
        // 3×3 区域，较为密集
        GenerateLeafLayer(position, trunkHeight + 1, 1, 0.1f, false, treeContainer.transform);

        // === 第四层（最顶层，y = trunkHeight + 2）===
        // 十字形或小范围，只有中心附近
        GenerateTopLeafLayer(position, trunkHeight + 2, treeContainer.transform);
    }

    /// <summary>
    /// 为树木创建方块线段（树干专用）
    /// </summary>
    /// <param name="from">起始点坐标</param>
    /// <param name="to">结束点坐标</param>
    /// <param name="prefab">要实例化的方块预制体</param>
    /// <param name="parent">父物体Transform</param>
    private void CreateTreeBlockLine(Vector3 from, Vector3 to, Block prefab, Transform parent)
    {
        Vector3 position = from;
        do
        {
            if (position.y < to.y)
            {
                position.y++;
            }

            if (position.y > to.y)
            {
                position.y--;
            }

            InstantiateBlockWithParent(prefab, new Vector3(position.x, position.y, position.z), parent);
        } while (position != to);
    }

    /// <summary>
    /// 实例化方块并指定父物体
    /// </summary>
    /// <param name="prefab">要实例化的方块预制体</param>
    /// <param name="position">放置位置</param>
    /// <param name="parent">父物体Transform</param>
    /// <returns>实例化的方块</returns>
    private Block InstantiateBlockWithParent(Block prefab, Vector3 position, Transform parent)
    {
#if UNITY_EDITOR
        if (!Application.isPlaying)
        {
            // 编辑器模式：使用 PrefabUtility 保持 Prefab 连接
            Block block = (Block)UnityEditor.PrefabUtility.InstantiatePrefab(prefab);
            block.transform.position = position;
            block.transform.rotation = Quaternion.identity;
            block.transform.SetParent(parent);
            return block;
        }
        else
        {
            // 运行时：使用普通实例化
            Block block = Instantiate(prefab, position, Quaternion.identity);
            block.transform.SetParent(parent);
            return block;
        }
#else
        // 非编辑器构建：使用普通实例化
        Block block = Instantiate(prefab, position, Quaternion.identity);
        block.transform.SetParent(parent);
        return block;
#endif
    }

    /// <summary>
    /// 生成一层树叶
    /// </summary>
    /// <param name="treeBase">树木底部位置</param>
    /// <param name="yOffset">相对于树木底部的Y偏移</param>
    /// <param name="radius">树叶半径（1=3×3, 2=5×5）</param>
    /// <param name="cornerHoleChance">四角挖空概率</param>
    /// <param name="hollowCorners">是否对四角应用挖空</param>
    /// <param name="parent">父物体Transform</param>
    private void GenerateLeafLayer(Vector3 treeBase, int yOffset, int radius, float cornerHoleChance,
        bool hollowCorners, Transform parent)
    {
        for (int x = -radius; x <= radius; x++)
        {
            for (int z = -radius; z <= radius; z++)
            {
                // 跳过树干位置（中心点在树干顶部层）
                if (x == 0 && z == 0 && yOffset >= 4)
                {
                    // 树干位置不放叶子（树干已经存在）
                    continue;
                }

                // 四角挖空逻辑
                if (hollowCorners && Mathf.Abs(x) == radius && Mathf.Abs(z) == radius)
                {
                    // 四角有概率不生成叶子
                    if (Random.Range(0f, 1f) < cornerHoleChance)
                    {
                        continue;
                    }
                }

                Vector3 leafPos = treeBase + new Vector3(x, yOffset, z);
                InstantiateBlockWithParent(blocks.leaves, leafPos, parent);
            }
        }
    }

    /// <summary>
    /// 生成树冠最顶层（十字形或小范围）
    /// </summary>
    /// <param name="treeBase">树木底部位置</param>
    /// <param name="yOffset">相对于树木底部的Y偏移</param>
    /// <param name="parent">父物体Transform</param>
    private void GenerateTopLeafLayer(Vector3 treeBase, int yOffset, Transform parent)
    {
        // 顶层：十字形 + 中心，带随机
        Vector3[] topPositions = new Vector3[]
        {
            new Vector3(0, 0, 0), // 中心
            new Vector3(1, 0, 0), // +X
            new Vector3(-1, 0, 0), // -X
            new Vector3(0, 0, 1), // +Z
            new Vector3(0, 0, -1), // -Z
        };

        foreach (Vector3 offset in topPositions)
        {
            // 中心必定生成，四周有小概率不生成
            if (offset == Vector3.zero || Random.Range(0f, 1f) > 0.2f)
            {
                Vector3 leafPos = treeBase + new Vector3(offset.x, yOffset, offset.z);
                InstantiateBlockWithParent(blocks.leaves, leafPos, parent);
            }
        }

        // 对角线位置有较小概率生成
        Vector3[] diagonalPositions = new Vector3[]
        {
            new Vector3(1, 0, 1),
            new Vector3(1, 0, -1),
            new Vector3(-1, 0, 1),
            new Vector3(-1, 0, -1),
        };

        foreach (Vector3 offset in diagonalPositions)
        {
            if (Random.Range(0f, 1f) < 0.3f)
            {
                Vector3 leafPos = treeBase + new Vector3(offset.x, yOffset, offset.z);
                InstantiateBlockWithParent(blocks.leaves, leafPos, parent);
            }
        }
    }

    /// <summary>
    /// 清除所有子物体（即已生成的所有方块），以便重新生成地形。
    /// </summary>
    public void Clean()
    {
        // 销毁所有直接子对象
        while (transform.childCount > 0)
        {
            DestroyImmediate(transform.GetChild(0).gameObject);
        }
    }

    /// <summary>
    /// 定义了世界中使用的各种基础方块类型
    /// </summary>
    [System.Serializable]
    public struct Blocks
    {
        /// <summary>
        /// 表面草方块
        /// </summary>
        public Block grass;

        /// <summary>
        /// 泥土方块
        /// </summary>
        public Block dirt;

        /// <summary>
        /// 石头方块
        /// </summary>
        public Block stone;

        /// <summary>
        /// 基岩方块（不可破坏）
        /// </summary>
        public Block bedrock;

        /// <summary>
        /// 原木方块（树干）
        /// </summary>
        public Block log;

        /// <summary>
        /// 树叶方块
        /// </summary>
        public Block leaves;
    }

    /// <summary>
    /// 噪声设置参数，用于控制分形叠加和双噪声生成
    /// </summary>
    [System.Serializable]
    public struct NoiseSettings
    {
        /// <summary>
        /// 噪声层数（octaves），越多细节越丰富
        /// </summary>
        [Tooltip("噪声层数（octaves），越多细节越丰富")] [Range(1, 8)]
        public int octaves;

        /// <summary>
        /// 持续度，控制每层振幅衰减的速度
        /// </summary>
        [Tooltip("持续度，控制每层振幅衰减的速度")] [Range(0f, 1f)]
        public float persistence;

        /// <summary>
        /// 频率倍增因子，控制每层频率增加的速度
        /// </summary>
        [Tooltip("频率倍增因子，控制每层频率增加的速度")] [Range(1f, 4f)]
        public float lacunarity;

        /// <summary>
        /// 基础缩放比例，越大地形越平缓
        /// </summary>
        [Tooltip("基础缩放比例，越大地形越平缓")] public float scale;

        /// <summary>
        /// 高度乘数，控制山峰和深谷的幅度
        /// </summary>
        [Tooltip("高度乘数，控制山峰和深谷的幅度")] public float heightMultiplier;

        /// <summary>
        /// 第二噪声偏移量，用于增加差异性
        /// </summary>
        [Tooltip("第二噪声偏移量，用于增加差异性")] public Vector2 secondaryOffset;

        /// <summary>
        /// 返回默认噪声设置
        /// </summary>
        public static NoiseSettings Default => new NoiseSettings
        {
            octaves = 4,
            persistence = 0.5f,
            lacunarity = 2f,
            scale = 20f,
            heightMultiplier = 15f,
            secondaryOffset = new Vector2(1000f, 1000f)
        };
    }
}