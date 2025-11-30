using UnityEngine;

/// <summary>
/// 工作台UI管理器，提供3x3合成功能
/// </summary>
public class CraftingTable : InventoryUI
{
    /// <summary>
    /// 合成槽位数组（3x3格子）
    /// </summary>
    [Header("Crafting")] [Tooltip("合成槽位数组（9个）")]
    public InventorySlot[] craftingSlots;

    /// <summary>
    /// 合成结果槽位
    /// </summary>
    [Tooltip("合成结果槽位")] public InventorySlot outputSlot;

    /// <summary>
    /// 可合成的物品列表
    /// </summary>
    [Tooltip("可合成的物品列表")] public Item[] craftableItems;

    /// <summary>
    /// 玩家背包引用（用于共享物品数据）
    /// </summary>
    [Header("References")] public Inventory playerInventory;

    private CraftingSystem craftingSystem;

    /// <summary>
    /// 初始化窗口矩形和玩家对象引用，并初始化合成系统
    /// </summary>
    private void Start()
    {
        InitializeBase();

        if (craftingSlots != null && craftingSlots.Length > 0)
        {
            craftingSystem = new CraftingSystem(craftingSlots, outputSlot, craftableItems, itemPrefab, itemParent);
        }
    }

    /// <summary>
    /// 每帧更新逻辑：处理高亮、配方检测以及拖拽交互等操作
    /// </summary>
    private void Update()
    {
        if (!isOpen) return;

        craftingSystem?.CheckRecipes(AddOutputItemTriggers);
        HandleDragUpdate();
    }

    /// <summary>
    /// 切换工作台UI的显示状态
    /// </summary>
    public override void Toggle()
    {
        ClearDraggedItem();

        bool enabled = !window.gameObject.activeSelf;

        if (!enabled)
        {
            SyncItemsBackToInventory();
            craftingSystem?.ClearCraftingSlots(playerInventory);
            ClearDisplayedItems();
        }

        window.gameObject.SetActive(enabled);
        Cursor.visible = enabled;
        isOpen = enabled;
        Inventory.open = enabled;

        if (!enabled)
        {
            ClearHighlight();
        }

        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;

        if (enabled)
        {
            SyncItemsFromInventory();
        }
    }

    /// <summary>
    /// 获取所有可接受放置的有效槽位列表（包括合成槽、背包槽与快捷栏槽）
    /// </summary>
    /// <returns>有效槽位数组</returns>
    protected override InventorySlot[] GetValidDropSlots()
    {
        int totalCount = 0;
        if (craftingSlots != null) totalCount += craftingSlots.Length;
        if (baggageSlots != null) totalCount += baggageSlots.Length;
        if (hotbarSlots != null) totalCount += hotbarSlots.Length;

        InventorySlot[] validSlots = new InventorySlot[totalCount];
        int index = 0;

        if (craftingSlots != null)
        {
            foreach (var slot in craftingSlots)
            {
                validSlots[index++] = slot;
            }
        }

        if (baggageSlots != null)
        {
            foreach (var slot in baggageSlots)
            {
                validSlots[index++] = slot;
            }
        }

        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                validSlots[index++] = slot;
            }
        }

        return validSlots;
    }

    #region 物品同步

    /// <summary>
    /// 从玩家背包同步物品显示到工作台UI。
    /// 在 CraftingTable 的槽位里创建物品的"镜像"显示。
    /// </summary>
    private void SyncItemsFromInventory()
    {
        if (playerInventory == null) return;

        // 同步背包槽位
        if (baggageSlots != null && playerInventory.baggageSlots != null)
        {
            for (int i = 0; i < baggageSlots.Length && i < playerInventory.baggageSlots.Length; i++)
            {
                SyncSlotDisplay(playerInventory.baggageSlots[i], baggageSlots[i]);
            }
        }

        // 同步快捷栏槽位
        if (hotbarSlots != null && playerInventory.hotbarSlots != null)
        {
            for (int i = 0; i < hotbarSlots.Length && i < playerInventory.hotbarSlots.Length; i++)
            {
                SyncSlotDisplay(playerInventory.hotbarSlots[i], hotbarSlots[i]);
            }
        }
    }

    /// <summary>
    /// 同步单个槽位的显示内容。如果源槽位有物品，则在目标槽位创建其副本并设置相关属性。
    /// </summary>
    /// <param name="sourceSlot">来源槽位</param>
    /// <param name="targetSlot">目标槽位</param>
    private void SyncSlotDisplay(InventorySlot sourceSlot, InventorySlot targetSlot)
    {
        if (sourceSlot == null || targetSlot == null) return;

        // 清除目标槽位的现有物品
        if (targetSlot.item != null)
        {
            Destroy(targetSlot.item.gameObject);
            targetSlot.item = null;
        }

        // 如果源槽位有物品，在目标槽位创建副本
        if (sourceSlot.item != null)
        {
            InventoryItem newItem = Instantiate(itemPrefab, itemParent);
            newItem.transform.position = targetSlot.transform.position;
            newItem.itemName = sourceSlot.item.itemName;
            newItem.scriptableItem = sourceSlot.item.scriptableItem;
            newItem.SetSprite(sourceSlot.item.icon);
            newItem.SetAmount(sourceSlot.item.amount);

            newItem.slot = targetSlot;
            newItem.lastSlot = targetSlot;
            targetSlot.item = newItem;

            AddItemTriggers(newItem);
        }
    }

    /// <summary>
    /// 将工作台里的物品同步回 Inventory
    /// </summary>
    private void SyncItemsBackToInventory()
    {
        if (playerInventory == null) return;

        // 同步背包槽位
        if (baggageSlots != null && playerInventory.baggageSlots != null)
        {
            for (int i = 0; i < baggageSlots.Length && i < playerInventory.baggageSlots.Length; i++)
            {
                SyncSlotBack(baggageSlots[i], playerInventory.baggageSlots[i]);
            }
        }

        // 同步快捷栏槽位
        if (hotbarSlots != null && playerInventory.hotbarSlots != null)
        {
            for (int i = 0; i < hotbarSlots.Length && i < playerInventory.hotbarSlots.Length; i++)
            {
                SyncSlotBack(hotbarSlots[i], playerInventory.hotbarSlots[i]);
            }
        }
    }

    /// <summary>
    /// 将单个槽位的数据同步回 Inventory
    /// </summary>
    /// <param name="craftingTableSlot">工作台中的槽位</param>
    /// <param name="inventorySlot">玩家背包中的槽位</param>
    private void SyncSlotBack(InventorySlot craftingTableSlot, InventorySlot inventorySlot)
    {
        if (craftingTableSlot == null || inventorySlot == null) return;

        if (craftingTableSlot.item != null)
        {
            // CraftingTable 槽位有物品
            if (inventorySlot.item != null)
            {
                // Inventory 也有物品，检查是否是同一种
                if (inventorySlot.item.itemName == craftingTableSlot.item.itemName)
                {
                    // 同种物品，更新数量
                    inventorySlot.item.SetAmount(craftingTableSlot.item.amount);
                }
                else
                {
                    // 不同物品（说明在 CraftingTable 里进行了交换）
                    // 更新 Inventory 的物品为 CraftingTable 的物品
                    inventorySlot.item.itemName = craftingTableSlot.item.itemName;
                    inventorySlot.item.scriptableItem = craftingTableSlot.item.scriptableItem;
                    inventorySlot.item.SetSprite(craftingTableSlot.item.icon);
                    inventorySlot.item.SetAmount(craftingTableSlot.item.amount);
                }
            }
            else
            {
                // Inventory 没有物品，但 CraftingTable 有（说明物品被移动到了这个槽位）
                // 在 Inventory 创建新物品
                InventoryItem newItem = Instantiate(playerInventory.itemPrefab, playerInventory.itemParent);
                newItem.transform.position = inventorySlot.transform.position;
                newItem.itemName = craftingTableSlot.item.itemName;
                newItem.scriptableItem = craftingTableSlot.item.scriptableItem;
                newItem.SetSprite(craftingTableSlot.item.icon);
                newItem.SetAmount(craftingTableSlot.item.amount);
                newItem.slot = inventorySlot;
                newItem.lastSlot = inventorySlot;
                inventorySlot.item = newItem;
                playerInventory.AddItemTriggersPublic(newItem);
            }
        }
        else
        {
            // CraftingTable 槽位为空
            if (inventorySlot.item != null)
            {
                // Inventory 有物品，但 CT 没有
                // 检查这个物品是否被移动到了合成栏（不要销毁，让 ClearCraftingSlots 处理）
                string itemName = inventorySlot.item.itemName;
                bool isInCraftingSlots = IsItemInCraftingSlots(itemName);

                if (isInCraftingSlots)
                {
                    // 物品在合成栏中，销毁 Inventory 的物品（它会通过 ClearCraftingSlots 返回）
                    Destroy(inventorySlot.item.gameObject);
                    inventorySlot.item = null;
                }
            }
        }
    }

    /// <summary>
    /// 检查指定名称的物品是否存在于合成槽中
    /// </summary>
    /// <param name="itemName">要查找的物品名称</param>
    /// <returns>若存在则返回true，否则返回false</returns>
    private bool IsItemInCraftingSlots(string itemName)
    {
        if (craftingSlots == null) return false;

        foreach (var slot in craftingSlots)
        {
            if (slot != null && slot.item != null && slot.item.itemName == itemName)
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 清除 CraftingTable 中显示的物品（不影响 Inventory 数据）
    /// </summary>
    private void ClearDisplayedItems()
    {
        if (baggageSlots != null)
        {
            foreach (var slot in baggageSlots)
            {
                if (slot != null && slot.item != null)
                {
                    Destroy(slot.item.gameObject);
                    slot.item = null;
                }
            }
        }

        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                if (slot != null && slot.item != null)
                {
                    Destroy(slot.item.gameObject);
                    slot.item = null;
                }
            }
        }
    }

    #endregion

    #region 合成系统回调

    /// <summary>
    /// 获取输出槽位
    /// </summary>
    /// <returns>输出槽位</returns>
    protected override InventorySlot GetOutputSlot()
    {
        return outputSlot;
    }

    /// <summary>
    /// 获取合成系统实例
    /// </summary>
    /// <returns>合成系统实例</returns>
    protected override CraftingSystem GetCraftingSystem()
    {
        return craftingSystem;
    }

    /// <summary>
    /// 尝试将手中拖拽的物品放入背包（CraftingTable版本，操作playerInventory的槽位）
    /// </summary>
    /// <returns>是否成功放入</returns>
    private bool TryPlaceDraggedItemToInventory()
    {
        if (draggedItem == null || playerInventory == null) return true;

        // 尝试堆叠到现有物品（操作Inventory的真实槽位）
        foreach (InventorySlot slot in playerInventory.hotbarSlots)
        {
            if (slot?.item != null &&
                slot.item.itemName == draggedItem.itemName &&
                slot.item.amount + draggedItem.amount <= 64)
            {
                slot.item.IncreaseAmount(draggedItem.amount);
                Destroy(draggedItem.gameObject);
                return true;
            }
        }

        foreach (InventorySlot slot in playerInventory.baggageSlots)
        {
            if (slot?.item != null &&
                slot.item.itemName == draggedItem.itemName &&
                slot.item.amount + draggedItem.amount <= 64)
            {
                slot.item.IncreaseAmount(draggedItem.amount);
                Destroy(draggedItem.gameObject);
                return true;
            }
        }

        // 尝试放入空槽位
        InventorySlot emptySlot = FindEmptySlotInInventory();
        if (emptySlot != null)
        {
            // 在Inventory中创建新物品
            InventoryItem newItem = Instantiate(playerInventory.itemPrefab, playerInventory.itemParent);
            newItem.transform.position = emptySlot.transform.position;
            newItem.itemName = draggedItem.itemName;
            newItem.scriptableItem = draggedItem.scriptableItem;
            newItem.SetSprite(draggedItem.icon);
            newItem.SetAmount(draggedItem.amount);
            newItem.slot = emptySlot;
            newItem.lastSlot = emptySlot;
            emptySlot.item = newItem;
            playerInventory.AddItemTriggersPublic(newItem);

            Destroy(draggedItem.gameObject);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 在玩家背包中查找空槽位
    /// </summary>
    /// <returns>空槽位，未找到返回null</returns>
    private InventorySlot FindEmptySlotInInventory()
    {
        if (playerInventory == null) return null;

        foreach (InventorySlot slot in playerInventory.hotbarSlots)
        {
            if (slot != null && slot.item == null)
                return slot;
        }

        foreach (InventorySlot slot in playerInventory.baggageSlots)
        {
            if (slot != null && slot.item == null)
                return slot;
        }

        return null;
    }

    #endregion
}