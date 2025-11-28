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
}
