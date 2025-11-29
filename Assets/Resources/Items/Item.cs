using System;
using UnityEngine;

/// <summary>
/// 物品类脚本化对象，用于在Unity中创建可配置的物品数据
/// </summary>
[CreateAssetMenu(fileName = "Item", menuName = "Scriptable Object/Item")]
public class Item : ScriptableObject
{
    /// <summary>
    /// 物品名称，使用new关键字隐藏基类的name属性
    /// </summary>
    new public string name;

    /// <summary>
    /// 物品图标精灵，用于UI显示
    /// </summary>
    public Sprite sprite;

    /// <summary>
    /// 物品模型对象，用于场景中的3D显示
    /// </summary>
    public GameObject model;

    /// <summary>
    /// 合成配方信息
    /// </summary>
    [Header("Recipe")] public Recipe recipe;
}

/// <summary>
/// 表示一个合成配方的结构体，由九个格子组成（3x3网格）
/// </summary>
[Serializable]
public struct Recipe
{
    /// <summary>
    /// 上方三个格子：左、中、右
    /// </summary>
    [Header("Top")] public Item topLeft;

    public Item topCenter;
    public Item topRight;

    /// <summary>
    /// 中间三个格子：左、中、右
    /// </summary>
    [Header("Middle")] public Item middleLeft;

    public Item middleCenter;
    public Item middleRight;

    /// <summary>
    /// 下方三个格子：左、中、右
    /// </summary>
    [Header("Bottom")] public Item bottomLeft;

    public Item bottomCenter;
    public Item bottomRight;

    /// <summary>
    /// 检查配方是否为空，即所有格子都没有指定物品
    /// </summary>
    /// <returns>如果所有格子都为null则返回true，否则返回false</returns>
    public bool IsEmpty()
    {
        return topLeft == null && topCenter == null && topRight == null &&
               middleLeft == null && middleCenter == null && middleRight == null &&
               bottomLeft == null && bottomCenter == null && bottomRight == null;
    }

    /// <summary>
    /// 判断两个配方是否相等
    /// </summary>
    /// <param name="left">左侧比较的配方</param>
    /// <param name="right">右侧比较的配方</param>
    /// <returns>如果两个配方的所有格子对应物品相同则返回true，否则返回false</returns>
    public static bool operator ==(Recipe left, Recipe right)
    {
        return left.topLeft == right.topLeft &&
               left.topCenter == right.topCenter &&
               left.topRight == right.topRight &&
               left.middleLeft == right.middleLeft &&
               left.middleCenter == right.middleCenter &&
               left.middleRight == right.middleRight &&
               left.bottomLeft == right.bottomLeft &&
               left.bottomCenter == right.bottomCenter &&
               left.bottomRight == right.bottomRight;
    }

    /// <summary>
    /// 判断两个配方是否不相等
    /// </summary>
    /// <param name="left">左侧比较的配方</param>
    /// <param name="right">右侧比较的配方</param>
    /// <returns>如果两个配方有任何一个格子不同则返回true，否则返回false</returns>
    public static bool operator !=(Recipe left, Recipe right)
    {
        return !(left == right);
    }

    /// <summary>
    /// 覆盖Equals方法以支持对象比较
    /// </summary>
    /// <param name="obj">要与当前实例进行比较的对象</param>
    /// <returns>如果对象是Recipe类型且内容一致则返回true，否则返回false</returns>
    public override bool Equals(object obj)
    {
        if (obj is Recipe other)
        {
            return this == other;
        }

        return false;
    }

    /// <summary>
    /// 覆盖GetHashCode方法以便将Recipe用作字典键或集合元素
    /// </summary>
    /// <returns>基于配方各格子内容计算出的哈希码</returns>
    public override int GetHashCode()
    {
        var hash = new HashCode();
        hash.Add(topLeft);
        hash.Add(topCenter);
        hash.Add(topRight);
        hash.Add(middleLeft);
        hash.Add(middleCenter);
        hash.Add(middleRight);
        hash.Add(bottomLeft);
        hash.Add(bottomCenter);
        hash.Add(bottomRight);
        return hash.ToHashCode();
    }
}