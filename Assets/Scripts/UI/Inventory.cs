using UnityEngine;

/// <summary>
/// 库存系统管理器，用于控制玩家物品栏的显示、隐藏以及物品拖拽逻辑。
/// </summary>
public class Inventory : InventoryUI
{
    /// <summary>
    /// 静态变量表示当前是否打开了物品栏界面
    /// </summary>
    public static bool open;

    /// <summary>
    /// 合成槽位数组（2x2或3x3格子）
    /// </summary>
    [Header("Crafting")] [Tooltip("合成槽位数组")]
    public InventorySlot[] craftingSlots;

    /// <summary>
    /// 合成结果槽位
    /// </summary>
    [Tooltip("合成结果槽位")] public InventorySlot outputSlot;

    /// <summary>
    /// 可合成的物品列表
    /// </summary>
    [Tooltip("可合成的物品列表")] public Item[] craftableItems;

    private CraftingSystem craftingSystem;

    /// <summary>
    /// 初始化方法，在对象启用时调用。
    /// 设置初始状态为关闭，隐藏窗口，锁定并隐藏光标。
    /// </summary>
    private void Start()
    {
        window.gameObject.SetActive(false);
        isOpen = false;
        open = false;
        Cursor.visible = false;
        Cursor.lockState = CursorLockMode.Locked;

        InitializeBase();

        // 初始化合成系统
        if (craftingSlots != null && craftingSlots.Length > 0)
        {
            craftingSystem = new CraftingSystem(craftingSlots, outputSlot, craftableItems, itemPrefab, itemParent);
        }

        // 遍历所有物品槽位，如果有物品则添加触发器
        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null)
            {
                AddItemTriggers(slot.item);
            }
        }
    }

    /// <summary>
    /// 每帧更新方法。
    /// 处理 inventory UI 的交互逻辑，包括按键检测、物品拖拽、鼠标点击等操作。
    /// </summary>
    private void Update()
    {
        if (!isOpen) return;

        // 检查合成配方
        craftingSystem?.CheckRecipes(AddOutputItemTriggers);

        // 处理拖拽交互
        HandleDragUpdate();
    }

    /// <summary>
    /// 公共方法：给物品添加触发器（供 CraftingTable 调用）
    /// </summary>
    /// <param name="item">需要添加触发器的物品实例</param>
    public void AddItemTriggersPublic(InventoryItem item)
    {
        AddItemTriggers(item);
    }

    /// <summary>
    /// 更新槽位高亮效果（重写以检查open状态）
    /// </summary>
    protected override void UpdateSlotHighlight()
    {
        if (!open)
        {
            ClearHighlight();
            return;
        }

        base.UpdateSlotHighlight();
    }

    /// <summary>
    /// 切换物品栏窗口的开启与关闭状态
    /// </summary>
    public void ToggleInventory()
    {
        Toggle();
    }

    /// <summary>
    /// 切换物品栏窗口的开启与关闭状态
    /// </summary>
    public override void Toggle()
    {
        if (window == null) return;

        ClearDraggedItem();

        bool enabled = !window.gameObject.activeSelf;

        // 关闭时清空合成格子，将材料返回背包
        if (!enabled)
        {
            craftingSystem?.ClearCraftingSlots(this);
        }

        window.gameObject.SetActive(enabled);
        Cursor.visible = enabled;
        isOpen = enabled;
        open = enabled;

        if (!enabled)
        {
            ClearHighlight();
        }

        Cursor.lockState = enabled ? CursorLockMode.None : CursorLockMode.Locked;
    }

    /// <summary>
    /// 获取有效的放置槽位（包括背包槽、快捷栏槽和合成槽）
    /// </summary>
    /// <returns>包含所有有效放置槽位的数组</returns>
    protected override InventorySlot[] GetValidDropSlots()
    {
        int totalCount = 0;
        if (baggageSlots != null) totalCount += baggageSlots.Length;
        if (hotbarSlots != null) totalCount += hotbarSlots.Length;
        if (craftingSlots != null) totalCount += craftingSlots.Length;

        InventorySlot[] validSlots = new InventorySlot[totalCount];
        int index = 0;

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

        if (craftingSlots != null)
        {
            foreach (var slot in craftingSlots)
            {
                validSlots[index++] = slot;
            }
        }

        return validSlots;
    }

    /// <summary>
    /// 抛出物品
    /// </summary>
    /// <param name="itemData">物品数据</param>
    /// <param name="amount">数量</param>
    protected override void ThrowItem(Item itemData, int amount)
    {
        if (player == null || dropPrefab == null) return;

        Vector3 spawnPos = player.transform.position + Vector3.up * 1f;
        Vector3 targetPos = ItemUtils.CalculateThrowTargetPosition(player, throwDistance);

        for (int i = 0; i < amount; i++)
        {
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                0,
                Random.Range(-0.3f, 0.3f)
            );

            Drop drop = Instantiate(dropPrefab, spawnPos, Quaternion.identity);
            drop.modelPrefab = itemData.model;
            drop.item = itemData;
            drop.Init();
            drop.ThrowTo(targetPos + randomOffset);
        }
    }

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
    /// 尝试将手中拖拽的物品放入背包
    /// </summary>
    /// <returns>是否成功放入</returns>
    private bool TryPlaceDraggedItemToInventory()
    {
        if (draggedItem == null) return true;

        // 尝试堆叠到现有物品
        foreach (InventorySlot slot in hotbarSlots)
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

        foreach (InventorySlot slot in baggageSlots)
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
        InventorySlot emptySlot = FindEmptySlot();
        if (emptySlot != null)
        {
            draggedItem.transform.position = emptySlot.transform.position;
            draggedItem.slot = emptySlot;
            draggedItem.lastSlot = emptySlot;
            emptySlot.item = draggedItem;
            return true;
        }

        return false;
    }

    #endregion

    #region 物品管理

    /// <summary>
    /// 获取物品并添加到背包槽位中。
    /// 优先级：Hotbar现有堆叠 > Inventory现有堆叠 > Hotbar空槽 > Inventory空槽
    /// </summary>
    /// <param name="item">要添加的物品</param>
    public void GetItem(Item item)
    {
        if (TryAddToExistingStack(hotbarSlots, item)) return;
        if (TryAddToExistingStack(baggageSlots, item)) return;
        if (TryCreateNewStack(hotbarSlots, item)) return;
        if (TryCreateNewStack(baggageSlots, item)) return;

        Debug.LogWarning($"[Inventory] 背包已满，无法拾取物品: {item.name}");
    }

    /// <summary>
    /// 尝试将物品添加到现有堆叠中
    /// </summary>
    /// <param name="targetSlots">槽位数组</param>
    /// <param name="item">要添加的物品</param>
    /// <returns>是否成功添加</returns>
    private bool TryAddToExistingStack(InventorySlot[] targetSlots, Item item)
    {
        if (targetSlots == null) return false;

        foreach (InventorySlot slot in targetSlots)
        {
            if (slot == null) continue;
            if (slot.item == null) continue;
            if (slot.item.itemName != item.name) continue;
            if (slot.item.amount >= 64) continue;

            slot.item.IncreaseAmount(1);
            return true;
        }

        return false;
    }

    /// <summary>
    /// 尝试在空槽位创建新的物品堆叠
    /// </summary>
    /// <param name="targetSlots">槽位数组</param>
    /// <param name="item">要创建的新物品</param>
    /// <returns>是否成功创建</returns>
    private bool TryCreateNewStack(InventorySlot[] targetSlots, Item item)
    {
        if (targetSlots == null) return false;

        foreach (InventorySlot slot in targetSlots)
        {
            if (slot == null) continue;
            if (slot.item != null) continue;

            Transform actualParent = itemParent ?? FindItemParent();

            if (actualParent == null)
                return false;

            InventoryItem inventoryItem = Instantiate(itemPrefab, actualParent);
            inventoryItem.transform.position = slot.transform.position;
            inventoryItem.itemName = item.name;
            inventoryItem.scriptableItem = item;
            inventoryItem.SetSprite(item.sprite);
            inventoryItem.SetAmount(1);

            inventoryItem.slot = slot;
            inventoryItem.lastSlot = slot;
            slot.item = inventoryItem;

            AddItemTriggers(inventoryItem);

            return true;
        }

        return false;
    }

    /// <summary>
    /// 查找物品的父物体
    /// </summary>
    /// <returns>物品的父级Transform</returns>
    private Transform FindItemParent()
    {
        foreach (InventorySlot checkSlot in baggageSlots)
        {
            if (checkSlot != null && checkSlot.item != null)
            {
                return checkSlot.item.transform.parent;
            }
        }

        foreach (InventorySlot checkSlot in hotbarSlots)
        {
            if (checkSlot != null && checkSlot.item != null)
            {
                return checkSlot.item.transform.parent;
            }
        }

        return null;
    }

    #endregion
}