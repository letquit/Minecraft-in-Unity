using UnityEngine;

/// <summary>
/// 可交互的工作台方块，右键点击打开工作台UI
/// </summary>
public class CraftingTableBlock : Block
{
    /// <summary>
    /// 工作台UI的Tag
    /// </summary>
    public string uiTag = "CraftingTable";

    /// <summary>
    /// 打开工作台UI
    /// 通过tag查找UI对象，获取CraftingTable组件并调用Toggle方法切换显示状态
    /// </summary>
    public void OpenUI()
    {
        // 根据tag查找工作台UI对象
        GameObject uiObject = GameObject.FindGameObjectWithTag(uiTag);
        if (uiObject != null)
        {
            // 获取CraftingTable组件并切换UI显示状态
            CraftingTable craftingTable = uiObject.GetComponent<CraftingTable>();
            if (craftingTable != null)
            {
                craftingTable.Toggle();
            }
        }
    }
}