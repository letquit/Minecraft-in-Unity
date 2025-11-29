using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;
using UnityEngine.UI;

/// <summary>
/// 工作台UI管理器，提供3x3合成功能
/// </summary>
public class CraftingTable : UI
{
    /// <summary>
    /// UI画布组件引用
    /// </summary>
    public Canvas canvas;

    /// <summary>
    /// 所有槽位数组（包含合成槽、背包槽、快捷栏槽）
    /// </summary>
    public InventorySlot[] slots;

    /// <summary>
    /// 背包槽位数组
    /// </summary>
    [Header("Baggage Slots")] public InventorySlot[] baggageSlots;

    /// <summary>
    /// 快捷栏槽位数组
    /// </summary>
    [Header("Hotbar Slots")] public InventorySlot[] hotbarSlots;

    /// <summary>
    /// 物品项预制体
    /// </summary>
    public InventoryItem itemPrefab;

    /// <summary>
    /// 存放物品项的父级Transform
    /// </summary>
    public Transform itemParent;

    /// <summary>
    /// 所有可交互区域
    /// </summary>
    [Header("Interactive Areas")] public RectTransform[] interactiveAreas;

    /// <summary>
    /// 槽位高亮检测的最大距离阈值
    /// </summary>
    [Header("Highlight Settings")] public float highlightDistanceThreshold = 60f;

    /// <summary>
    /// 丢弃物品预制体
    /// </summary>
    [Header("Throw")] public Drop dropPrefab;

    /// <summary>
    /// 丢弃距离
    /// </summary>
    public float throwDistance = 2f;

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

    private InventoryItem draggedItem;
    private RectTransform windowRect;
    private bool isMouseInsideWindow;
    private bool isMouseInsideInteractiveArea;
    private bool justPickedUp;
    private InventorySlot currentHighlightedSlot;
    private Player player;

    /// <summary>
    /// 初始化窗口矩形和玩家对象引用
    /// </summary>
    private void Start()
    {
        windowRect = canvas.GetComponent<RectTransform>();
        player = FindFirstObjectByType<Player>();
    }

    /// <summary>
    /// 每帧更新逻辑：处理高亮、配方检测以及拖拽交互等操作
    /// </summary>
    private void Update()
    {
        if (!isOpen) return;

        UpdateSlotHighlight();
        CheckRecipes();

        if (draggedItem)
        {
            // 将被拖拽的物品置顶显示
            draggedItem.transform.SetSiblingIndex(draggedItem.transform.parent.childCount - 1);
            UpdateDraggedItemPosition();

            if (Input.GetButtonDown("Fire2"))
            {
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    TrySplitItem();
                }
                else if (!isMouseInsideInteractiveArea)
                {
                    OnRightClickOutsideInteractiveArea();
                }
            }

            if (Input.GetButtonDown("Fire1"))
            {
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    Drop(draggedItem);
                }
                else if (!isMouseInsideInteractiveArea)
                {
                    OnLeftClickOutsideInteractiveArea();
                }
            }
        }

        justPickedUp = false;
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
            // 关闭时，先同步物品回 Inventory，再清空合成槽
            SyncItemsBackToInventory();

            ClearCraftingSlots();

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

        // 打开时同步显示物品
        if (enabled)
        {
            SyncItemsFromInventory();
        }
    }

    /// <summary>
    /// 从玩家背包同步物品显示到工作台UI。
    /// 在 CraftingTable 的槽位里创建物品的"镜像"显示。
    /// </summary>
    private void SyncItemsFromInventory()
    {
        if (playerInventory == null)
        {
            return;
        }

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

            // 添加拖拽触发器
            AddItemTriggers(newItem);

            // 记录对应关系，用于同步回去
            // 使用 sourceSlot 的引用存储在某处，或者通过索引对应
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
                else
                {
                    // 物品不在合成栏中，说明物品在 CraftingTable 中被完全移除/消耗了
                    // 这种情况不应该发生在正常的背包/快捷栏槽位同步中
                    // 但为了安全，保留 Inventory 的物品
                    // 不销毁，保留原物品
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

    /// <summary>
    /// 获取所有可接受放置的有效槽位列表（包括合成槽、背包槽与快捷栏槽）
    /// </summary>
    /// <returns>有效槽位数组</returns>
    private InventorySlot[] GetValidDropSlots()
    {
        int totalCount = 0;
        if (craftingSlots != null) totalCount += craftingSlots.Length;
        if (baggageSlots != null) totalCount += baggageSlots.Length;
        if (hotbarSlots != null) totalCount += hotbarSlots.Length;

        InventorySlot[] validSlots = new InventorySlot[totalCount];
        int index = 0;

        // 合成槽
        if (craftingSlots != null)
        {
            foreach (var slot in craftingSlots)
            {
                validSlots[index++] = slot;
            }
        }

        // 使用 CraftingTable 自己的背包槽位（不是 Inventory 的）
        if (baggageSlots != null)
        {
            foreach (var slot in baggageSlots)
            {
                validSlots[index++] = slot;
            }
        }

        // 使用 CraftingTable 自己的快捷栏槽位
        if (hotbarSlots != null)
        {
            foreach (var slot in hotbarSlots)
            {
                validSlots[index++] = slot;
            }
        }

        return validSlots;
    }

    #region 合成系统

    /// <summary>
    /// 检查当前合成槽中的物品是否匹配某个可合成配方。
    /// 若匹配成功且输出槽为空，则在输出槽中创建对应的合成结果。
    /// 若未匹配但输出槽中有刚合成的物品，则销毁该物品。
    /// </summary>
    private void CheckRecipes()
    {
        if (craftingSlots == null || craftingSlots.Length == 0 || outputSlot == null)
            return;

        // 将当前合成格子转换为配方并归一化
        Recipe currentRecipe = NormalizeRecipe(GridToRecipe());
        bool recipeFound = false;

        foreach (Item item in craftableItems)
        {
            if (item == null || item.recipe.IsEmpty())
                continue;

            // 归一化物品配方后比较
            Recipe normalizedItemRecipe = NormalizeRecipe(item.recipe);

            if (currentRecipe == normalizedItemRecipe)
            {
                recipeFound = true;

                if (outputSlot.item != null)
                    break;

                InventoryItem outputItem = InstantiateCraftingItem(item, outputSlot);
                AddOutputItemTriggers(outputItem);
                outputItem.justCrafted = true;

                break;
            }
        }

        if (!recipeFound && outputSlot.item != null)
        {
            if (outputSlot.item.justCrafted)
            {
                Destroy(outputSlot.item.gameObject);
                outputSlot.item = null;
            }
        }
    }

    /// <summary>
    /// 归一化配方，将配方内容移动到左上角以实现位置无关性匹配。
    /// </summary>
    /// <param name="recipe">原始配方</param>
    /// <returns>归一化后的配方</returns>
    private Recipe NormalizeRecipe(Recipe recipe)
    {
        // 将配方转换为一维数组 (索引: row * 3 + col)
        Item[] grid = new Item[9];
        grid[0] = recipe.topLeft;
        grid[1] = recipe.topCenter;
        grid[2] = recipe.topRight;
        grid[3] = recipe.middleLeft;
        grid[4] = recipe.middleCenter;
        grid[5] = recipe.middleRight;
        grid[6] = recipe.bottomLeft;
        grid[7] = recipe.bottomCenter;
        grid[8] = recipe.bottomRight;

        // 计算最小行和最小列（找到配方的左上角边界）
        int minRow = 3, minCol = 3;
        for (int i = 0; i < 9; i++)
        {
            if (grid[i] != null)
            {
                int row = i / 3;
                int col = i % 3;
                if (row < minRow) minRow = row;
                if (col < minCol) minCol = col;
            }
        }

        // 如果配方为空，返回空配方
        if (minRow == 3 || minCol == 3)
        {
            return new Recipe();
        }

        // 创建归一化后的配方（移动到左上角）
        Item[] normalizedGrid = new Item[9];
        for (int row = 0; row < 3; row++)
        {
            for (int col = 0; col < 3; col++)
            {
                int srcRow = row + minRow;
                int srcCol = col + minCol;
                if (srcRow < 3 && srcCol < 3)
                {
                    int srcIndex = srcRow * 3 + srcCol;
                    int destIndex = row * 3 + col;
                    normalizedGrid[destIndex] = grid[srcIndex];
                }
            }
        }

        // 转换回Recipe结构
        Recipe normalized = new Recipe();
        normalized.topLeft = normalizedGrid[0];
        normalized.topCenter = normalizedGrid[1];
        normalized.topRight = normalizedGrid[2];
        normalized.middleLeft = normalizedGrid[3];
        normalized.middleCenter = normalizedGrid[4];
        normalized.middleRight = normalizedGrid[5];
        normalized.bottomLeft = normalizedGrid[6];
        normalized.bottomCenter = normalizedGrid[7];
        normalized.bottomRight = normalizedGrid[8];

        return normalized;
    }


    /// <summary>
    /// 将当前合成槽的内容转换为一个Recipe对象。
    /// </summary>
    /// <returns>表示当前合成槽状态的Recipe对象</returns>
    private Recipe GridToRecipe()
    {
        Recipe recipe = new Recipe();

        if (craftingSlots.Length == 9)
        {
            if (craftingSlots[0].item) recipe.topLeft = craftingSlots[0].item.scriptableItem;
            if (craftingSlots[1].item) recipe.topCenter = craftingSlots[1].item.scriptableItem;
            if (craftingSlots[2].item) recipe.topRight = craftingSlots[2].item.scriptableItem;
            if (craftingSlots[3].item) recipe.middleLeft = craftingSlots[3].item.scriptableItem;
            if (craftingSlots[4].item) recipe.middleCenter = craftingSlots[4].item.scriptableItem;
            if (craftingSlots[5].item) recipe.middleRight = craftingSlots[5].item.scriptableItem;
            if (craftingSlots[6].item) recipe.bottomLeft = craftingSlots[6].item.scriptableItem;
            if (craftingSlots[7].item) recipe.bottomCenter = craftingSlots[7].item.scriptableItem;
            if (craftingSlots[8].item) recipe.bottomRight = craftingSlots[8].item.scriptableItem;
        }

        return recipe;
    }

    /// <summary>
    /// 开始拖拽输出槽中的合成物品，并消耗合成材料。
    /// </summary>
    /// <param name="item">要开始拖拽的合成物品</param>
    private void StartOutputDrag(InventoryItem item)
    {
        item.justCrafted = false;

        EventTrigger trigger = item.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            trigger.triggers.Clear();
        }

        AddItemTriggers(item);

        foreach (InventorySlot craftingSlot in craftingSlots)
        {
            if (craftingSlot.item == null)
                continue;

            if (craftingSlot.item.amount > 1)
            {
                craftingSlot.item.IncreaseAmount(-1);
            }
            else
            {
                Destroy(craftingSlot.item.gameObject);
                craftingSlot.item = null;
            }
        }

        StartDrag(item, true);
    }

    /// <summary>
    /// 给输出槽中的合成物品添加点击事件触发器，用于启动拖拽操作。
    /// </summary>
    /// <param name="item">需要添加触发器的合成物品</param>
    private void AddOutputItemTriggers(InventoryItem item)
    {
        EventTrigger trigger = item.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = item.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();

        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((eventData) => { StartOutputDrag(item); });
        trigger.triggers.Add(pointerDownEntry);
    }

    /// <summary>
    /// 实例化一个新的合成物品并放置到指定槽位中。
    /// </summary>
    /// <param name="item">要实例化的物品数据</param>
    /// <param name="slot">目标槽位</param>
    /// <returns>新创建的InventoryItem实例</returns>
    private InventoryItem InstantiateCraftingItem(Item item, InventorySlot slot)
    {
        Transform actualParent = itemParent ?? slot.transform.parent;

        InventoryItem inventoryItem = Instantiate(itemPrefab, actualParent);
        inventoryItem.transform.position = slot.transform.position;
        inventoryItem.itemName = item.name;
        inventoryItem.scriptableItem = item;
        inventoryItem.SetSprite(item.sprite);
        inventoryItem.SetAmount(1);

        inventoryItem.slot = slot;
        inventoryItem.lastSlot = slot;
        slot.item = inventoryItem;

        return inventoryItem;
    }

    /// <summary>
    /// 清除所有合成槽中的物品，并将其返还给玩家背包。
    /// 如果输出槽有刚合成的物品也会被清除。
    /// </summary>
    private void ClearCraftingSlots()
    {
        if (craftingSlots == null)
        {
            return;
        }

        foreach (InventorySlot slot in craftingSlots)
        {
            if (slot == null)
            {
                continue;
            }

            if (slot.item != null)
            {
                Item itemData = slot.item.scriptableItem;
                int amount = slot.item.amount;
                string itemName = slot.item.itemName;

                if (itemData == null)
                {
                    itemData = GetItemDataByName(itemName);
                }

                if (itemData != null && playerInventory != null)
                {
                    for (int i = 0; i < amount; i++)
                    {
                        playerInventory.GetItem(itemData);
                    }
                }

                Destroy(slot.item.gameObject);
                slot.item = null;
            }
        }

        if (outputSlot != null && outputSlot.item != null && outputSlot.item.justCrafted)
        {
            Destroy(outputSlot.item.gameObject);
            outputSlot.item = null;
        }
    }

    #endregion

    #region 拖拽系统

    /// <summary>
    /// 更新当前高亮显示的物品槽。
    /// </summary>
    private void UpdateSlotHighlight()
    {
        InventorySlot nearestSlot = GetNearestSlotToMouse();

        if (nearestSlot != currentHighlightedSlot)
        {
            if (currentHighlightedSlot != null)
            {
                currentHighlightedSlot.SetHighlight(false);
            }

            if (nearestSlot != null)
            {
                nearestSlot.SetHighlight(true);
            }

            currentHighlightedSlot = nearestSlot;
        }
    }

    /// <summary>
    /// 获取距离鼠标最近的有效物品槽。
    /// </summary>
    /// <returns>距离鼠标最近的物品槽；如果没有找到则返回 null。</returns>
    private InventorySlot GetNearestSlotToMouse()
    {
        float minDistance = float.MaxValue;
        InventorySlot nearestSlot = null;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        foreach (InventorySlot slot in slots)
        {
            if (slot == null) continue;

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect == null) continue;

            // 鼠标直接位于槽内时立即返回该槽
            if (RectTransformUtility.RectangleContainsScreenPoint(slotRect, Input.mousePosition, eventCamera))
            {
                return slot;
            }

            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, slot.transform.position);
            float distance = Vector2.Distance(Input.mousePosition, slotScreenPos);

            if (distance < minDistance && distance <= highlightDistanceThreshold)
            {
                minDistance = distance;
                nearestSlot = slot;
            }
        }

        return nearestSlot;
    }

    /// <summary>
    /// 清除当前高亮状态并重置引用。
    /// </summary>
    private void ClearHighlight()
    {
        if (currentHighlightedSlot != null)
        {
            currentHighlightedSlot.SetHighlight(false);
            currentHighlightedSlot = null;
        }
    }

    /// <summary>
    /// 更新被拖动物品的位置，使其跟随鼠标移动，并限制其活动范围。
    /// </summary>
    private void UpdateDraggedItemPosition()
    {
        if (draggedItem == null || canvas == null || windowRect == null) return;

        Vector2 mousePosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            windowRect,
            Input.mousePosition,
            canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera,
            out mousePosition);

        Vector2 windowSize = windowRect.rect.size;
        Vector2 windowPivot = windowRect.pivot;

        float windowLeft = -windowSize.x * windowPivot.x;
        float windowRight = windowSize.x * (1 - windowPivot.x);
        float windowBottom = -windowSize.y * windowPivot.y;
        float windowTop = windowSize.y * (1 - windowPivot.y);

        isMouseInsideWindow = mousePosition.x >= windowLeft &&
                              mousePosition.x <= windowRight &&
                              mousePosition.y >= windowBottom &&
                              mousePosition.y <= windowTop;

        isMouseInsideInteractiveArea = CheckMouseInsideInteractiveAreas();

        Vector2 targetLocalPosition = isMouseInsideWindow
            ? mousePosition
            : new Vector2(
                Mathf.Clamp(mousePosition.x, windowLeft, windowRight),
                Mathf.Clamp(mousePosition.y, windowBottom, windowTop)
            );

        draggedItem.transform.position = windowRect.TransformPoint(targetLocalPosition);
    }

    /// <summary>
    /// 判断鼠标是否处于交互区域中（如快捷栏、背包等）。
    /// </summary>
    /// <returns>如果鼠标在任意一个有效交互区域内，则返回 true；否则根据窗口判断结果返回。</returns>
    private bool CheckMouseInsideInteractiveAreas()
    {
        if (interactiveAreas == null || interactiveAreas.Length == 0)
        {
            return isMouseInsideWindow;
        }

        foreach (RectTransform area in interactiveAreas)
        {
            if (area == null || !area.gameObject.activeInHierarchy) continue;

            if (RectTransformUtility.RectangleContainsScreenPoint(area, Input.mousePosition, canvas.worldCamera))
            {
                return true;
            }
        }

        return false;
    }

    /// <summary>
    /// 开始拖动指定的物品项。
    /// </summary>
    /// <param name="item">要开始拖动的物品。</param>
    /// <param name="isLeftClick">是否是左键点击，默认为 true。</param>
    public void StartDrag(InventoryItem item, bool isLeftClick = true)
    {
        if (!isLeftClick && draggedItem == null)
        {
            StartRightClickDrag(item);
            return;
        }

        if (draggedItem != null)
        {
            if (draggedItem == item) return;

            if (item.slot != null)
            {
                TrySwapOrMerge(draggedItem, item);
                return;
            }
        }

        draggedItem = item;
        if (item.slot)
        {
            item.slot.item = null;
        }

        item.slot = null;
        justPickedUp = true;
    }

    /// <summary>
    /// 处理右键点击以拆分物品数量进行拖动。
    /// </summary>
    /// <param name="item">要处理的物品。</param>
    public void StartRightClickDrag(InventoryItem item)
    {
        if (draggedItem != null) return;
        if (item == null || item.slot == null) return;

        if (item.amount <= 1)
        {
            StartDrag(item, true);
            return;
        }

        int originalAmount = item.amount;
        int pickupAmount = (originalAmount + 1) / 2;
        int remainAmount = originalAmount - pickupAmount;

        InventorySlot originalSlot = item.slot;

        if (remainAmount <= 0)
        {
            StartDrag(item, true);
            return;
        }

        GameObject remainItemObj = Instantiate(item.gameObject, parent: item.transform.parent);
        InventoryItem remainItem = remainItemObj.GetComponent<InventoryItem>();

        // 确保 scriptableItem 被正确复制
        remainItem.scriptableItem = item.scriptableItem;
        remainItem.itemName = item.itemName;
        remainItem.icon = item.icon;

        if (item.amountText != null && remainItem.amountText != null &&
            item.amountText.GetInstanceID() == remainItem.amountText.GetInstanceID())
        {
            remainItem.amountText = remainItemObj.GetComponentInChildren<TextMeshProUGUI>();
        }

        AddItemTriggers(remainItem);

        remainItem.SetAmount(remainAmount);
        remainItem.transform.position = originalSlot.transform.position;
        remainItem.slot = originalSlot;
        remainItem.lastSlot = originalSlot;
        originalSlot.item = remainItem;

        item.SetAmount(pickupAmount);
        item.slot = null;
        item.lastSlot = originalSlot;

        draggedItem = item;
        justPickedUp = true;
    }

    /// <summary>
    /// 尝试交换或合并两个物品。
    /// </summary>
    /// <param name="dragged">正在被拖动的物品。</param>
    /// <param name="target">目标物品。</param>
    private void TrySwapOrMerge(InventoryItem dragged, InventoryItem target)
    {
        InventorySlot targetSlot = target.slot;

        if (dragged.itemName == target.itemName && target.amount + dragged.amount <= 64)
        {
            target.IncreaseAmount(dragged.amount);
            Destroy(dragged.gameObject);
            draggedItem = null;
        }
        else if (dragged.itemName == target.itemName && target.amount < 64)
        {
            int spaceLeft = 64 - target.amount;
            target.IncreaseAmount(spaceLeft);
            dragged.IncreaseAmount(-spaceLeft);
        }
        else
        {
            InventorySlot originalSlot = dragged.lastSlot;

            if (originalSlot != null)
            {
                target.transform.position = originalSlot.transform.position;
                target.slot = originalSlot;
                target.lastSlot = originalSlot;
                originalSlot.item = target;
            }

            dragged.transform.position = targetSlot.transform.position;
            dragged.slot = targetSlot;
            dragged.lastSlot = targetSlot;
            targetSlot.item = dragged;

            draggedItem = null;
        }
    }

    /// <summary>
    /// 在拖动物品过程中尝试将物品拆分为多个部分放置到其他槽位。
    /// </summary>
    private void TrySplitItem()
    {
        if (draggedItem == null) return;

        Vector2 mouseScreenPos = Input.mousePosition;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        float minDistance = float.MaxValue;
        InventorySlot targetSlot = null;

        InventorySlot[] validSlots = GetValidDropSlots();

        foreach (InventorySlot slot in validSlots)
        {
            if (slot == null) continue;

            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, slot.transform.position);
            float distance = Vector2.Distance(mouseScreenPos, slotScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                targetSlot = slot;
            }
        }

        if (targetSlot == null) return;

        if (targetSlot.item != null)
        {
            if (draggedItem.itemName != targetSlot.item.itemName)
            {
                Drop(draggedItem);
                return;
            }

            if (targetSlot.item.amount >= 64)
            {
                Drop(draggedItem);
                return;
            }

            if (draggedItem.amount > 1)
            {
                targetSlot.item.IncreaseAmount(1);
                draggedItem.IncreaseAmount(-1);
            }
            else
            {
                if (targetSlot.item.amount + 1 <= 64)
                {
                    targetSlot.item.IncreaseAmount(1);
                    Destroy(draggedItem.gameObject);
                    draggedItem = null;
                }
            }

            return;
        }

        // 目标槽位为空的情况
        if (draggedItem.amount <= 1)
        {
            // 只剩一个物品时，直接放置到目标槽位（和左键行为一致）
            draggedItem.transform.position = targetSlot.transform.position;
            draggedItem.slot = targetSlot;
            draggedItem.lastSlot = targetSlot;
            targetSlot.item = draggedItem;
            draggedItem = null;
            return;
        }

        GameObject newItemObj = Instantiate(draggedItem.gameObject, parent: draggedItem.transform.parent);
        InventoryItem newItem = newItemObj.GetComponent<InventoryItem>();

        // 确保 scriptableItem 被正确复制
        newItem.scriptableItem = draggedItem.scriptableItem;
        newItem.itemName = draggedItem.itemName;
        newItem.icon = draggedItem.icon;

        AddItemTriggers(newItem);
        newItem.SetAmount(1);
        draggedItem.IncreaseAmount(-1);

        newItem.transform.position = targetSlot.transform.position;
        newItem.slot = targetSlot;
        newItem.lastSlot = targetSlot;
        targetSlot.item = newItem;
    }

    /// <summary>
    /// 将物品丢弃到最近的有效槽位中。
    /// </summary>
    /// <param name="item">要丢弃的物品。</param>
    public void Drop(InventoryItem item)
    {
        // 获取鼠标在屏幕上的位置
        Vector2 mouseScreenPos = Input.mousePosition;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        float minDistance = float.MaxValue;
        InventorySlot targetSlot = null;

        InventorySlot[] validSlots = GetValidDropSlots();

        foreach (InventorySlot slot in validSlots)
        {
            if (slot == null) continue;

            // 使用鼠标位置与槽位的屏幕位置比较
            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, slot.transform.position);
            float distance = Vector2.Distance(mouseScreenPos, slotScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                targetSlot = slot;
            }
        }

        if (targetSlot == null)
        {
            return;
        }

        if (targetSlot.item)
        {
            if (item.itemName == targetSlot.item.itemName && targetSlot.item.amount + item.amount <= 64)
            {
                targetSlot.item.IncreaseAmount(item.amount);
                Destroy(item.gameObject);

                if (draggedItem == item)
                {
                    draggedItem = null;
                }
            }
            else if (item.itemName == targetSlot.item.itemName && targetSlot.item.amount < 64)
            {
                int spaceLeft = 64 - targetSlot.item.amount;
                targetSlot.item.IncreaseAmount(spaceLeft);
                item.IncreaseAmount(-spaceLeft);
            }
            else
            {
                InventoryItem targetItem = targetSlot.item;
                InventorySlot originalSlot = item.lastSlot;

                item.transform.position = targetSlot.transform.position;
                item.slot = targetSlot;
                item.lastSlot = targetSlot;
                targetSlot.item = item;

                targetItem.slot = null;
                targetItem.lastSlot = originalSlot;
                draggedItem = targetItem;
            }
        }
        else
        {
            item.transform.position = targetSlot.transform.position;
            item.slot = targetSlot;
            item.lastSlot = targetSlot;
            targetSlot.item = item;

            if (draggedItem == item)
            {
                draggedItem = null;
            }
        }
    }

    /// <summary>
    /// 清理当前拖动中的物品，将其放回原槽或其他可用槽位。
    /// </summary>
    private void ClearDraggedItem()
    {
        if (draggedItem == null) return;

        if (draggedItem.lastSlot != null)
        {
            InventorySlot targetSlot = draggedItem.lastSlot;

            if (targetSlot.item != null && targetSlot.item != draggedItem)
            {
                if (targetSlot.item.itemName == draggedItem.itemName &&
                    targetSlot.item.amount + draggedItem.amount <= 64)
                {
                    targetSlot.item.IncreaseAmount(draggedItem.amount);
                    Destroy(draggedItem.gameObject);
                    draggedItem = null;
                    return;
                }
                else
                {
                    InventorySlot emptySlot = FindEmptySlot();
                    if (emptySlot != null)
                    {
                        targetSlot = emptySlot;
                    }
                    else
                    {
                        Item itemData = GetItemDataByName(draggedItem.itemName);
                        if (itemData != null)
                        {
                            ThrowItem(itemData, draggedItem.amount);
                        }

                        Destroy(draggedItem.gameObject);
                        draggedItem = null;
                        return;
                    }
                }
            }

            draggedItem.transform.position = targetSlot.transform.position;
            draggedItem.slot = targetSlot;
            draggedItem.lastSlot = targetSlot;
            targetSlot.item = draggedItem;
        }
        else
        {
            InventorySlot emptySlot = FindEmptySlot();
            if (emptySlot != null)
            {
                draggedItem.transform.position = emptySlot.transform.position;
                draggedItem.slot = emptySlot;
                draggedItem.lastSlot = emptySlot;
                emptySlot.item = draggedItem;
            }
            else
            {
                Item itemData = GetItemDataByName(draggedItem.itemName);
                if (itemData != null)
                {
                    ThrowItem(itemData, draggedItem.amount);
                }

                Destroy(draggedItem.gameObject);
            }
        }

        draggedItem = null;
    }

    /// <summary>
    /// 查找第一个空闲的物品槽。
    /// </summary>
    /// <returns>第一个未使用的物品槽；如果没有找到则返回 null。</returns>
    private InventorySlot FindEmptySlot()
    {
        foreach (InventorySlot slot in hotbarSlots)
        {
            if (slot != null && slot.item == null)
                return slot;
        }

        foreach (InventorySlot slot in baggageSlots)
        {
            if (slot != null && slot.item == null)
                return slot;
        }

        return null;
    }

    /// <summary>
    /// 给物品添加事件触发器以便响应点击操作。
    /// </summary>
    /// <param name="item">需要添加事件监听的物品组件。</param>
    private void AddItemTriggers(InventoryItem item)
    {
        EventTrigger trigger = item.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = item.gameObject.AddComponent<EventTrigger>();
        }

        trigger.triggers.Clear();

        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((eventData) =>
        {
            PointerEventData pointerData = (PointerEventData)eventData;
            InventoryItem targetItem = item.GetComponent<InventoryItem>();

            if (pointerData.button == PointerEventData.InputButton.Left)
            {
                StartDrag(targetItem, true);
            }
            else if (pointerData.button == PointerEventData.InputButton.Right)
            {
                StartDrag(targetItem, false);
            }
        });
        trigger.triggers.Add(pointerDownEntry);
    }

    #endregion

    #region 丢弃系统

    /// <summary>
    /// 当鼠标左键点击在交互区域外时调用，用于丢弃当前拖拽的整个物品。
    /// </summary>
    private void OnLeftClickOutsideInteractiveArea()
    {
        // 如果没有正在拖拽的物品，则直接返回
        if (draggedItem == null) return;

        // 获取被拖拽物品的数据
        Item itemData = GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        // 计算要丢弃的数量并执行丢弃操作
        int amountToThrow = draggedItem.amount;
        ThrowItem(itemData, amountToThrow);

        // 销毁拖拽对象并清空引用
        Destroy(draggedItem.gameObject);
        draggedItem = null;
    }

    /// <summary>
    /// 当鼠标右键点击在交互区域外时调用，用于丢弃当前拖拽物品中的一个单位。
    /// </summary>
    private void OnRightClickOutsideInteractiveArea()
    {
        // 如果没有正在拖拽的物品，则直接返回
        if (draggedItem == null) return;

        // 获取被拖拽物品的数据
        Item itemData = GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        // 执行丢弃一个单位的操作
        ThrowItem(itemData, 1);

        // 更新数量或销毁对象
        if (draggedItem.amount > 1)
        {
            draggedItem.IncreaseAmount(-1);
        }
        else
        {
            Destroy(draggedItem.gameObject);
            draggedItem = null;
        }
    }

    /// <summary>
    /// 根据指定的物品数据和数量，在玩家前方生成掉落物实体。
    /// </summary>
    /// <param name="itemData">要丢弃的物品数据</param>
    /// <param name="amount">要丢弃的物品数量</param>
    private void ThrowItem(Item itemData, int amount)
    {
        // 检查必要组件是否存在
        if (player == null || dropPrefab == null) return;

        // 设置生成位置和目标位置
        Vector3 spawnPos = player.transform.position + Vector3.up * 1f;
        Vector3 targetPos = player.transform.position + player.transform.forward * throwDistance;
        targetPos.y = player.transform.position.y + 0.5f;

        // 实例化多个掉落物并投掷到目标位置附近
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

    /// <summary>
    /// 根据物品名称从Resources文件夹中加载对应的Item资源。
    /// </summary>
    /// <param name="itemName">需要查找的物品名称</param>
    /// <returns>找到的Item实例；如果未找到则返回null</returns>
    private Item GetItemDataByName(string itemName)
    {
        Item[] allItems = Resources.LoadAll<Item>("Items");
        foreach (Item item in allItems)
        {
            if (item.name == itemName)
            {
                return item;
            }
        }

        return null;
    }

    #endregion
}