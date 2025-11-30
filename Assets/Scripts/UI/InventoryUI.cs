using TMPro;
using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 库存UI基类，包含物品拖拽、放置、高亮等共享逻辑。
/// Inventory 和 CraftingTable 都继承自此类。
/// </summary>
public abstract class InventoryUI : UI
{
    #region 字段定义

    /// <summary>
    /// UI画布组件引用
    /// </summary>
    public Canvas canvas;

    /// <summary>
    /// 所有库存槽位数组
    /// </summary>
    public InventorySlot[] slots;

    /// <summary>
    /// 背包槽位数组（用于存放拾取的物品）
    /// </summary>
    [Header("Baggage Slots")] public InventorySlot[] baggageSlots;

    /// <summary>
    /// 快捷栏槽位数组（下方9个，优先放入）
    /// </summary>
    [Header("Hotbar Slots")] public InventorySlot[] hotbarSlots;

    /// <summary>
    /// 物品项预制体，用于创建新物品时实例化
    /// </summary>
    public InventoryItem itemPrefab;

    /// <summary>
    /// 存放物品项的父级Transform
    /// </summary>
    public Transform itemParent;

    /// <summary>
    /// 所有可交互区域的RectTransform数组（如Inventory窗口、Hotbar等）
    /// </summary>
    [Header("Interactive Areas")] public RectTransform[] interactiveAreas;

    /// <summary>
    /// 槽位高亮检测的最大距离阈值
    /// </summary>
    [Header("Highlight Settings")] public float highlightDistanceThreshold = 60f;

    /// <summary>
    /// 掉落物预制体
    /// </summary>
    [Header("Throw")] [Tooltip("掉落物预制体")] public Drop dropPrefab;

    /// <summary>
    /// 丢弃距离
    /// </summary>
    [Tooltip("丢弃距离")] public float throwDistance = 3f;

    /// <summary>
    /// 当前正在被拖动的物品项
    /// </summary>
    protected InventoryItem draggedItem;

    /// <summary>
    /// 窗口的RectTransform组件，用于边界检测
    /// </summary>
    protected RectTransform windowRect;

    /// <summary>
    /// 标记鼠标是否在窗口内
    /// </summary>
    protected bool isMouseInsideWindow;

    /// <summary>
    /// 标记鼠标是否在任意可交互区域内
    /// </summary>
    protected bool isMouseInsideInteractiveArea;

    /// <summary>
    /// 标记是否刚刚拾取了物品，用于防止同一帧内拾取后立即放下
    /// </summary>
    protected bool justPickedUp;

    /// <summary>
    /// 当前高亮的槽位
    /// </summary>
    protected InventorySlot currentHighlightedSlot;

    /// <summary>
    /// 玩家引用
    /// </summary>
    protected Player player;

    #endregion

    #region 初始化

    /// <summary>
    /// 初始化基础组件引用
    /// </summary>
    protected virtual void InitializeBase()
    {
        windowRect = canvas.GetComponent<RectTransform>();
        player = FindFirstObjectByType<Player>();
    }

    #endregion

    #region Update 循环

    /// <summary>
    /// 处理拖拽交互的Update逻辑
    /// </summary>
    protected virtual void HandleDragUpdate()
    {
        if (!isOpen) return;

        UpdateSlotHighlight();

        if (draggedItem)
        {
            // 将拖拽物品置顶显示
            draggedItem.transform.SetSiblingIndex(draggedItem.transform.parent.childCount - 1);
            UpdateDraggedItemPosition();

            // 处理鼠标右键点击事件
            if (Input.GetButtonDown("Fire2"))
            {
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    // 检查是否点击在输出槽区域
                    if (IsClickingOnOutputSlot())
                    {
                        if (ShouldAllowOutputSlotInteraction())
                        {
                            // 允许交互，主动调用输出槽交互逻辑
                            HandleOutputSlotInteraction();
                        }
                    }
                    else
                    {
                        TrySplitItem();
                    }
                }
                else if (!isMouseInsideInteractiveArea)
                {
                    OnRightClickOutsideInteractiveArea();
                }
            }

            // 处理鼠标左键点击事件
            if (Input.GetButtonDown("Fire1"))
            {
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    // 检查是否点击在输出槽区域
                    if (IsClickingOnOutputSlot())
                    {
                        if (ShouldAllowOutputSlotInteraction())
                        {
                            // 允许交互，主动调用输出槽交互逻辑
                            HandleOutputSlotInteraction();
                        }
                    }
                    else
                    {
                        Drop(draggedItem);
                    }
                }
                else if (!isMouseInsideInteractiveArea)
                {
                    OnLeftClickOutsideInteractiveArea();
                }
            }
        }

        justPickedUp = false;
    }

    #endregion

    #region 槽位高亮

    /// <summary>
    /// 更新槽位高亮效果
    /// </summary>
    protected virtual void UpdateSlotHighlight()
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
    /// 获取距离鼠标最近的槽位
    /// </summary>
    /// <returns>距离鼠标最近的物品槽；如果没有找到则返回 null</returns>
    protected virtual InventorySlot GetNearestSlotToMouse()
    {
        float minDistance = float.MaxValue;
        InventorySlot nearestSlot = null;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        foreach (InventorySlot slot in slots)
        {
            if (slot == null) continue;

            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect == null) continue;

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
    /// 清除当前的槽位高亮
    /// </summary>
    protected virtual void ClearHighlight()
    {
        if (currentHighlightedSlot != null)
        {
            currentHighlightedSlot.SetHighlight(false);
            currentHighlightedSlot = null;
        }
    }

    #endregion

    #region 拖拽系统

    /// <summary>
    /// 更新被拖拽物品的位置
    /// </summary>
    protected virtual void UpdateDraggedItemPosition()
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
    /// 检查鼠标是否在任意可交互区域内
    /// </summary>
    /// <returns>如果鼠标在任意一个有效交互区域内，则返回 true</returns>
    protected virtual bool CheckMouseInsideInteractiveAreas()
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
    /// 开始拖动指定的物品项
    /// </summary>
    /// <param name="item">要开始拖动的物品</param>
    /// <param name="isLeftClick">是否是左键点击</param>
    public virtual void StartDrag(InventoryItem item, bool isLeftClick = true)
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
    /// 右键点击槽内物品时的处理逻辑：拆分物品数量进行拖动
    /// </summary>
    /// <param name="item">右键点击的物品</param>
    public virtual void StartRightClickDrag(InventoryItem item)
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
    /// 尝试交换或合并两个物品
    /// </summary>
    /// <param name="dragged">正在拖拽的物品</param>
    /// <param name="target">目标物品</param>
    protected virtual void TrySwapOrMerge(InventoryItem dragged, InventoryItem target)
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
    /// 尝试拆分当前正在拖拽的物品，并将其部分数量放置到最近的有效库存槽位中。
    /// 如果目标槽位已有相同类型的物品且未满，则增加其数量；
    /// 否则，在该槽位创建一个新的物品实例并减少原物品的数量。
    /// 当物品只剩1个时，直接执行放置操作。
    /// </summary>
    protected virtual void TrySplitItem()
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

            // 跳过输出槽 - 不能手动放入物品
            if (IsOutputSlot(slot)) continue;

            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(eventCamera, slot.transform.position);
            float distance = Vector2.Distance(mouseScreenPos, slotScreenPos);

            if (distance < minDistance)
            {
                minDistance = distance;
                targetSlot = slot;
            }
        }

        if (targetSlot == null) return;

        // 目标槽位已经有物品的情况处理
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

        // 在目标槽位创建新的物品实例以完成拆分逻辑
        GameObject newItemObj = Instantiate(draggedItem.gameObject, parent: draggedItem.transform.parent);
        InventoryItem newItem = newItemObj.GetComponent<InventoryItem>();

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
    /// 停止拖动并决定将物品放入哪个槽位中。
    /// 根据鼠标的当前位置，计算与有效槽位的距离，并选择最近的槽位进行放置或交换操作。
    /// 支持堆叠相同物品、部分堆叠以及完全交换逻辑。
    /// </summary>
    /// <param name="item">停止拖动的物品对象</param>
    public virtual void Drop(InventoryItem item)
    {
        Vector2 mouseScreenPos = Input.mousePosition;
        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;

        float minDistance = float.MaxValue;
        InventorySlot targetSlot = null;

        InventorySlot[] validSlots = GetValidDropSlots();

        foreach (InventorySlot slot in validSlots)
        {
            if (slot == null) continue;

            // 跳过输出槽 - 不能手动放入物品
            if (IsOutputSlot(slot)) continue;

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
                // 交换物品
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
    /// 清除当前拖拽的物品，将其放回原来的槽位
    /// </summary>
    protected virtual void ClearDraggedItem()
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
                        Item itemData = ItemUtils.GetItemDataByName(draggedItem.itemName);
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
                Item itemData = ItemUtils.GetItemDataByName(draggedItem.itemName);
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
    /// 查找第一个空闲的物品槽
    /// </summary>
    /// <returns>第一个未使用的物品槽；如果没有找到则返回 null</returns>
    protected virtual InventorySlot FindEmptySlot()
    {
        if (hotbarSlots != null)
        {
            foreach (InventorySlot slot in hotbarSlots)
            {
                if (slot != null && slot.item == null)
                    return slot;
            }
        }

        if (baggageSlots != null)
        {
            foreach (InventorySlot slot in baggageSlots)
            {
                if (slot != null && slot.item == null)
                    return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// 获取有效的放置槽位（子类可重写）
    /// </summary>
    /// <returns>有效槽位数组</returns>
    protected abstract InventorySlot[] GetValidDropSlots();

    /// <summary>
    /// 给物品添加事件触发器以便响应点击操作
    /// </summary>
    /// <param name="item">需要添加事件监听的物品组件</param>
    public virtual void AddItemTriggers(InventoryItem item)
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

    #region 合成系统支持

    /// <summary>
    /// 获取输出槽位（子类重写）
    /// </summary>
    protected virtual InventorySlot GetOutputSlot()
    {
        return null;
    }

    /// <summary>
    /// 获取合成系统实例（子类重写）
    /// </summary>
    /// <returns>合成系统实例，如果没有则返回null</returns>
    protected virtual CraftingSystem GetCraftingSystem()
    {
        return null;
    }

    /// <summary>
    /// 检查指定槽位是否是输出槽
    /// </summary>
    /// <param name="slot">要检查的槽位</param>
    /// <returns>如果是输出槽则返回true</returns>
    protected bool IsOutputSlot(InventorySlot slot)
    {
        InventorySlot output = GetOutputSlot();
        return output != null && slot == output;
    }

    /// <summary>
    /// 检查当前鼠标是否点击在输出槽区域
    /// </summary>
    /// <returns>如果鼠标在输出槽区域内则返回true</returns>
    protected virtual bool IsClickingOnOutputSlot()
    {
        InventorySlot output = GetOutputSlot();
        if (output == null) return false;

        Camera eventCamera = canvas.renderMode == RenderMode.ScreenSpaceOverlay ? null : canvas.worldCamera;
        RectTransform outputRect = output.GetComponent<RectTransform>();

        if (outputRect == null) return false;

        return RectTransformUtility.RectangleContainsScreenPoint(outputRect, Input.mousePosition, eventCamera);
    }

    /// <summary>
    /// 检查是否应该允许与输出槽交互
    /// 只有当手持物品与输出槽物品相同，且堆叠后不超过64时才允许
    /// </summary>
    /// <returns>是否允许交互</returns>
    protected virtual bool ShouldAllowOutputSlotInteraction()
    {
        InventorySlot output = GetOutputSlot();

        // 如果没有输出槽，不允许
        if (output == null) return false;

        // 如果输出槽没有物品，不允许（没有东西可拿，也不能放入）
        if (output.item == null) return false;

        // 如果手中没有物品，允许（空手拾取）- 但这种情况不会进入这个方法，因为draggedItem为null时不会执行
        if (draggedItem == null) return true;

        // 如果手持物品与输出槽物品不同，不允许
        if (draggedItem.itemName != output.item.itemName) return false;

        // 如果堆叠后超过64，不允许
        if (draggedItem.amount + output.item.amount > 64) return false;

        // 允许交互
        return true;
    }

    /// <summary>
    /// 处理输出槽交互逻辑（通用实现）
    /// 当手持相同物品点击输出槽时，堆叠物品并消耗材料
    /// </summary>
    protected virtual void HandleOutputSlotInteraction()
    {
        InventorySlot output = GetOutputSlot();
        if (output == null || output.item == null) return;
        if (draggedItem == null) return;

        InventoryItem outputItem = output.item;

        // 再次验证条件（防御性编程）
        if (draggedItem.itemName != outputItem.itemName)
            return;

        if (draggedItem.amount + outputItem.amount > 64)
            return;

        // 堆叠物品
        draggedItem.IncreaseAmount(outputItem.amount);

        // 消耗合成材料
        GetCraftingSystem()?.ConsumeMaterials();

        // 销毁输出槽物品
        Destroy(outputItem.gameObject);
        output.item = null;
    }

    /// <summary>
    /// 开始拖拽输出槽中的合成物品，并消耗合成材料。（通用实现）
    /// - 空手点击：正常拾取合成物品
    /// - 手持相同物品：堆叠到手中（不超过64）
    /// - 手持不同物品：忽略点击（什么都不做）
    /// </summary>
    /// <param name="item">要开始拖拽的合成物品</param>
    protected virtual void StartOutputDrag(InventoryItem item)
    {
        InventorySlot output = GetOutputSlot();
        if (output == null) return;

        // 检查手中是否已有物品
        if (draggedItem != null)
        {
            // 手中有不同物品，忽略点击
            if (draggedItem.itemName != item.itemName)
                return;

            // 手中有相同物品，尝试堆叠
            // 检查堆叠后是否超过64
            if (draggedItem.amount + item.amount > 64)
                return;

            // 可以堆叠，增加数量
            draggedItem.IncreaseAmount(item.amount);

            // 消耗合成材料
            GetCraftingSystem()?.ConsumeMaterials();

            // 销毁输出槽物品（已合并到手中）
            Destroy(item.gameObject);
            output.item = null;

            return;
        }

        // 空手拾取合成物品
        item.justCrafted = false;

        EventTrigger trigger = item.GetComponent<EventTrigger>();
        if (trigger != null)
        {
            trigger.triggers.Clear();
        }

        AddItemTriggers(item);

        // 消耗合成材料
        GetCraftingSystem()?.ConsumeMaterials();

        StartDrag(item, true);
    }

    /// <summary>
    /// 给输出槽中的合成物品添加点击事件触发器，用于启动拖拽操作。（通用实现）
    /// </summary>
    /// <param name="item">需要添加触发器的合成物品</param>
    protected virtual void AddOutputItemTriggers(InventoryItem item)
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

    #endregion

    #region 丢弃系统

    /// <summary>
    /// 当鼠标左键点击在交互区域外时调用，丢弃所有选中的物品
    /// </summary>
    protected virtual void OnLeftClickOutsideInteractiveArea()
    {
        if (draggedItem == null) return;

        Item itemData = ItemUtils.GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        int amountToThrow = draggedItem.amount;
        ThrowItem(itemData, amountToThrow);

        Destroy(draggedItem.gameObject);
        draggedItem = null;
    }

    /// <summary>
    /// 当鼠标右键点击在交互区域外时调用，丢弃一个选中的物品
    /// </summary>
    protected virtual void OnRightClickOutsideInteractiveArea()
    {
        if (draggedItem == null) return;

        Item itemData = ItemUtils.GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        ThrowItem(itemData, 1);

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
    /// 抛出物品，生成掉落物并播放抛出动画
    /// </summary>
    /// <param name="itemData">物品数据</param>
    /// <param name="amount">数量</param>
    protected virtual void ThrowItem(Item itemData, int amount)
    {
        if (player == null || dropPrefab == null) return;

        Vector3 spawnPos = player.transform.position + Vector3.up * 1f;
        Vector3 targetPos = player.transform.position + player.transform.forward * throwDistance;
        targetPos.y = player.transform.position.y + 0.5f;

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

    #endregion
}