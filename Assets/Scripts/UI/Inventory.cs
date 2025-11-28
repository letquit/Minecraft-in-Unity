using TMPro;
using UnityEngine.EventSystems;
using UnityEngine.UI;
using UnityEngine;

/// <summary>
/// 库存系统管理器，用于控制玩家物品栏的显示、隐藏以及物品拖拽逻辑。
/// </summary>
public class Inventory : MonoBehaviour
{
    /// <summary>
    /// UI画布组件引用。
    /// </summary>
    public Canvas canvas;

    /// <summary>
    /// 物品栏窗口图像组件，用于控制其激活状态。
    /// </summary>
    public Image window;

    /// <summary>
    /// 所有库存槽位数组。
    /// </summary>
    public InventorySlot[] slots;

    /// <summary>
    /// 背包槽位数组（用于存放拾取的物品）。
    /// </summary>
    [Header("Baggage Slots")] public InventorySlot[] baggageSlots;

    /// <summary>
    /// 物品项预制体，用于创建新物品时实例化。
    /// </summary>
    public InventoryItem itemPrefab;

    /// <summary>
    /// 存放物品项的父级Transform。
    /// </summary>
    public Transform itemParent;

    /// <summary>
    /// 静态变量表示当前是否打开了物品栏界面。
    /// </summary>
    public static bool open;

    /// <summary>
    /// 当前正在被拖动的物品项。
    /// </summary>
    private InventoryItem draggedItem;

    /// <summary>
    /// 窗口的RectTransform组件，用于边界检测。
    /// </summary>
    private RectTransform windowRect;

    /// <summary>
    /// 标记鼠标是否在窗口内。
    /// </summary>
    private bool isMouseInsideWindow;

    /// <summary>
    /// 标记是否刚刚拾取了物品，用于防止同一帧内拾取后立即放下。
    /// </summary>
    private bool justPickedUp;

    /// <summary>
    /// 所有可交互区域的RectTransform数组（如Inventory窗口、Hotbar等）。
    /// </summary>
    [Header("Interactive Areas")] public RectTransform[] interactiveAreas;

    /// <summary>
    /// 标记鼠标是否在任意可交互区域内。
    /// </summary>
    private bool isMouseInsideInteractiveArea;

    /// <summary>
    /// 当前高亮的槽位。
    /// </summary>
    private InventorySlot currentHighlightedSlot;

    /// <summary>
    /// 槽位高亮检测的最大距离阈值。
    /// </summary>
    [Header("Highlight Settings")] public float highlightDistanceThreshold = 60f;

    /// <summary>
    /// 快捷栏槽位数组（下方9个，优先放入）
    /// </summary>
    [Header("Hotbar Slots")] public InventorySlot[] hotbarSlots;

    [Header("Throw")] [Tooltip("掉落物预制体")] public Drop dropPrefab;

    [Tooltip("丢弃距离")] public float throwDistance = 3f;

    /// <summary>
    /// 玩家引用
    /// </summary>
    private Player player;

    /// <summary>
    /// 初始化方法。
    /// </summary>
    private void Start()
    {
        windowRect = canvas.GetComponent<RectTransform>();
        player = FindFirstObjectByType<Player>();

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
        // 检测 E 键按下，切换背包显示状态
        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleInventory();
        }

        // 更新选中槽位的高亮显示
        UpdateSlotHighlight();

        // 处理物品拖拽逻辑
        if (draggedItem)
        {
            // 将拖拽物品置顶显示
            draggedItem.transform.SetSiblingIndex(draggedItem.transform.parent.childCount - 1);

            // 更新拖拽物品的位置
            UpdateDraggedItemPosition();

            // 处理鼠标右键点击事件
            if (Input.GetButtonDown("Fire2"))
            {
                // 在交互区域内且不是刚拾取的物品时，尝试分割物品
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    TrySplitItem();
                }
                // 在交互区域外点击时，执行右键区域外点击处理
                else if (!isMouseInsideInteractiveArea)
                {
                    OnRightClickOutsideInteractiveArea();
                }
            }

            // 处理鼠标左键点击事件
            if (Input.GetButtonDown("Fire1"))
            {
                // 在交互区域内且不是刚拾取的物品时，丢弃物品
                if (isMouseInsideInteractiveArea && !justPickedUp)
                {
                    Drop(draggedItem);
                }
                // 在交互区域外点击时，执行左键区域外点击处理
                else if (!isMouseInsideInteractiveArea)
                {
                    OnLeftClickOutsideInteractiveArea();
                }
            }
        }

        // 重置刚拾取标记
        justPickedUp = false;
    }

    /// <summary>
    /// 更新槽位高亮效果。
    /// </summary>
    private void UpdateSlotHighlight()
    {
        if (!open)
        {
            ClearHighlight();
            return;
        }

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
    /// 获取距离鼠标最近的槽位。
    /// </summary>
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
    /// 清除当前的槽位高亮。
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
    /// 尝试拆分当前拖拽的物品。
    /// </summary>
    private void TrySplitItem()
    {
        if (draggedItem == null) return;

        float minDistance = 1000;
        InventorySlot targetSlot = null;
        foreach (InventorySlot slot in slots)
        {
            float distance = Vector2.Distance(draggedItem.transform.position, slot.transform.position);
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

        if (draggedItem.amount <= 1)
        {
            return;
        }

        GameObject newItemObj = Instantiate(draggedItem.gameObject, parent: draggedItem.transform.parent);
        InventoryItem newItem = newItemObj.GetComponent<InventoryItem>();
        AddItemTriggers(newItem);
        newItem.SetAmount(1);
        draggedItem.IncreaseAmount(-1);

        newItem.transform.position = targetSlot.transform.position;
        newItem.slot = targetSlot;
        newItem.lastSlot = targetSlot;
        targetSlot.item = newItem;
    }

    /// <summary>
    /// 当鼠标在可交互区域外点击左键时调用，丢弃所有选中的物品。
    /// </summary>
    private void OnLeftClickOutsideInteractiveArea()
    {
        if (draggedItem == null) return;

        // 获取物品信息
        Item itemData = GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        int amountToThrow = draggedItem.amount;

        // 生成掉落物
        ThrowItem(itemData, amountToThrow);

        // 销毁手中的物品
        Destroy(draggedItem.gameObject);
        draggedItem = null;
    }

    /// <summary>
    /// 当鼠标在可交互区域外点击右键时调用，丢弃一个选中的物品。
    /// </summary>
    private void OnRightClickOutsideInteractiveArea()
    {
        if (draggedItem == null) return;

        // 获取物品信息
        Item itemData = GetItemDataByName(draggedItem.itemName);
        if (itemData == null) return;

        // 生成一个掉落物
        ThrowItem(itemData, 1);

        // 减少手中物品数量
        if (draggedItem.amount > 1)
        {
            draggedItem.IncreaseAmount(-1);
        }
        else
        {
            // 如果只剩一个，销毁并清空引用
            Destroy(draggedItem.gameObject);
            draggedItem = null;
        }
    }

    /// <summary>
    /// 抛出物品，生成掉落物并播放抛出动画
    /// </summary>
    /// <param name="itemData">物品数据</param>
    /// <param name="amount">数量</param>
    private void ThrowItem(Item itemData, int amount)
    {
        if (player == null || dropPrefab == null) return;

        // 计算生成位置：玩家身体一半高度
        Vector3 spawnPos = player.transform.position + Vector3.up * 1f;

        // 计算目标位置
        Vector3 targetPos = CalculateThrowTargetPosition();

        // 为每个物品生成掉落物
        for (int i = 0; i < amount; i++)
        {
            // 添加一点随机偏移，避免完全重叠
            Vector3 randomOffset = new Vector3(
                Random.Range(-0.3f, 0.3f),
                0,
                Random.Range(-0.3f, 0.3f)
            );

            Drop drop = Instantiate(dropPrefab, spawnPos, Quaternion.identity);
            drop.modelPrefab = itemData.model;
            drop.item = itemData;
            drop.Init();

            // 播放抛出动画
            drop.ThrowTo(targetPos + randomOffset);
        }
    }

    /// <summary>
    /// 计算丢弃物品的目标位置，处理墙壁碰撞
    /// </summary>
    private Vector3 CalculateThrowTargetPosition()
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = player.transform.forward;
        LayerMask groundLayer = player.groundLayer;

        // 首选目标：玩家前方
        Vector3 primaryTarget = playerPos + playerForward * throwDistance;
        primaryTarget.y = playerPos.y + 0.5f;

        // 检测前方是否有墙
        if (TryGetValidDropPosition(playerPos, playerForward, throwDistance, groundLayer, out Vector3 validPos))
        {
            return validPos;
        }

        // 前方被阻挡，尝试两侧
        Vector3 playerRight = player.transform.right;
        Vector3 playerLeft = -player.transform.right;

        // 尝试右侧
        if (TryGetValidDropPosition(playerPos, playerRight, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        // 尝试左侧
        if (TryGetValidDropPosition(playerPos, playerLeft, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        // 尝试后方
        if (TryGetValidDropPosition(playerPos, -playerForward, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        // 四面都被阻挡，落在玩家脚下
        return playerPos + Vector3.up * 0.5f;
    }

    /// <summary>
    /// 尝试获取有效的掉落位置
    /// </summary>
    /// <param name="startPos">起始位置</param>
    /// <param name="direction">方向</param>
    /// <param name="distance">距离</param>
    /// <param name="groundLayer">地面层</param>
    /// <param name="validPosition">输出的有效位置</param>
    /// <returns>是否找到有效位置</returns>
    private bool TryGetValidDropPosition(Vector3 startPos, Vector3 direction, float distance, LayerMask groundLayer,
        out Vector3 validPosition)
    {
        Vector3 rayStart = startPos + Vector3.up * 1f; // 从玩家身体高度发射射线
        Vector3 targetPos = startPos + direction * distance;
        targetPos.y = startPos.y + 0.5f;

        // 检测前方是否有墙壁
        if (Physics.Raycast(rayStart, direction, out RaycastHit wallHit, distance, groundLayer))
        {
            // 检测到墙壁，计算墙的高度
            float wallTopY = GetWallTopY(wallHit.point, groundLayer);
            float heightDifference = wallTopY - startPos.y;

            // 如果墙高度在1格以内，落在墙顶
            if (heightDifference <= 1.5f)
            {
                validPosition = new Vector3(wallHit.point.x, wallTopY + 0.5f, wallHit.point.z);
                // 稍微往墙内偏移，确保在墙顶上
                validPosition += direction * 0.3f;
                return true;
            }

            // 墙太高（超过2格），这个方向不可用
            if (heightDifference > 2f)
            {
                validPosition = Vector3.zero;
                return false;
            }
        }

        // 没有墙壁阻挡，使用原始目标位置
        validPosition = targetPos;
        return true;
    }

    /// <summary>
    /// 获取墙壁的顶部Y坐标
    /// </summary>
    private float GetWallTopY(Vector3 wallPoint, LayerMask groundLayer)
    {
        // 从墙壁碰撞点上方向下发射射线，找到墙顶
        Vector3 rayStart = wallPoint + Vector3.up * 10f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 15f, groundLayer))
        {
            return hit.point.y;
        }

        return wallPoint.y;
    }

    /// <summary>
    /// 根据物品名称获取物品数据
    /// </summary>
    private Item GetItemDataByName(string itemName)
    {
        // 从所有槽位中查找相同名称的物品来获取Item引用
        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null && slot.item.itemName == itemName)
            {
                // 需要通过其他方式获取Item数据
            }
        }

        // 从Resources加载或使用物品注册表
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

    /// <summary>
    /// 更新被拖拽物品的位置。
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

        Vector2 targetLocalPosition;

        if (isMouseInsideWindow)
        {
            targetLocalPosition = mousePosition;
        }
        else
        {
            targetLocalPosition = new Vector2(
                Mathf.Clamp(mousePosition.x, windowLeft, windowRight),
                Mathf.Clamp(mousePosition.y, windowBottom, windowTop)
            );
        }

        draggedItem.transform.position = windowRect.TransformPoint(targetLocalPosition);
    }

    /// <summary>
    /// 检查鼠标是否在任意可交互区域内。
    /// </summary>
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
    /// 切换物品栏窗口的开启与关闭状态。
    /// </summary>
    public void ToggleInventory()
    {
        // 每次切换时，先清除鼠标选取状态
        ClearDraggedItem();

        bool enabled = !window.gameObject.activeSelf;
        window.gameObject.SetActive(enabled);
        Cursor.visible = enabled;
        open = enabled;

        if (!enabled)
        {
            ClearHighlight();
        }

        if (enabled)
        {
            Cursor.lockState = CursorLockMode.None;
        }
        else
        {
            Cursor.lockState = CursorLockMode.Locked;
        }
    }

    /// <summary>
    /// 清除当前拖拽的物品，将其放回原来的槽位
    /// </summary>
    private void ClearDraggedItem()
    {
        if (draggedItem == null) return;

        // 如果有记录的上一个槽位，放回去
        if (draggedItem.lastSlot != null)
        {
            InventorySlot targetSlot = draggedItem.lastSlot;

            // 检查原槽位是否已有物品
            if (targetSlot.item != null && targetSlot.item != draggedItem)
            {
                // 原槽位有其他物品，尝试合并
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
                    // 无法合并，找一个空槽位
                    InventorySlot emptySlot = FindEmptySlot();
                    if (emptySlot != null)
                    {
                        targetSlot = emptySlot;
                    }
                    else
                    {
                        // 没有空槽位，丢弃物品
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

            // 放回槽位
            draggedItem.transform.position = targetSlot.transform.position;
            draggedItem.slot = targetSlot;
            draggedItem.lastSlot = targetSlot;
            targetSlot.item = draggedItem;
        }
        else
        {
            // 没有记录的槽位，尝试找一个空槽位
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
                // 没有空槽位，丢弃物品
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
    /// 查找一个空的槽位
    /// </summary>
    private InventorySlot FindEmptySlot()
    {
        // 优先在 Hotbar 找
        foreach (InventorySlot slot in hotbarSlots)
        {
            if (slot != null && slot.item == null)
            {
                return slot;
            }
        }

        // 然后在背包找
        foreach (InventorySlot slot in baggageSlots)
        {
            if (slot != null && slot.item == null)
            {
                return slot;
            }
        }

        return null;
    }

    /// <summary>
    /// 开始拖动指定的物品项。
    /// </summary>
    /// <param name="item">要开始拖动的物品</param>
    /// <param name="isLeftClick">是否是左键点击</param>
    public void StartDrag(InventoryItem item, bool isLeftClick = true)
    {
        if (!isLeftClick && draggedItem == null)
        {
            StartRightClickDrag(item);
            return;
        }

        if (draggedItem != null)
        {
            if (draggedItem == item)
            {
                return;
            }

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
    /// 尝试交换或合并两个物品。
    /// </summary>
    /// <param name="dragged">正在拖拽的物品</param>
    /// <param name="target">目标物品</param>
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
    /// 在拖动过程中根据鼠标指针的位置实时更新物品的位置。
    /// </summary>
    /// <param name="data">事件数据</param>
    public void Drag(BaseEventData data)
    {
        // 位置更新由UpdateDraggedItemPosition处理
    }

    /// <summary>
    /// 停止拖动并决定将物品放入哪个槽位中。
    /// </summary>
    /// <param name="item">停止拖动的物品</param>
    public void Drop(InventoryItem item)
    {
        float minDistance = 1000;
        InventorySlot targetSlot = null;
        foreach (InventorySlot slot in slots)
        {
            if (Vector2.Distance(item.transform.position, slot.transform.position) >= minDistance)
            {
                continue;
            }

            minDistance = Vector2.Distance(item.transform.position, slot.transform.position);
            targetSlot = slot;
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
            targetSlot.item = item;

            if (draggedItem == item)
            {
                draggedItem = null;
            }
        }
    }

    /// <summary>
    /// 给新创建的物品添加必要的UI交互触发器。
    /// </summary>
    /// <param name="item">需要添加触发器的物品</param>
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

    /// <summary>
    /// 右键点击槽内物品时的处理逻辑。
    /// </summary>
    /// <param name="item">右键点击的物品</param>
    public void StartRightClickDrag(InventoryItem item)
    {
        if (draggedItem != null)
        {
            return;
        }

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
    /// 获取物品并添加到背包槽位中。
    /// 优先级：Hotbar现有堆叠 > Inventory现有堆叠 > Hotbar空槽 > Inventory空槽
    /// </summary>
    /// <param name="item">要添加的物品</param>
    public void GetItem(Item item)
    {
        // 第一优先级：尝试在Hotbar的现有堆叠中添加
        if (TryAddToExistingStack(hotbarSlots, item))
        {
            return;
        }

        // 第二优先级：尝试在Inventory的现有堆叠中添加
        if (TryAddToExistingStack(baggageSlots, item))
        {
            return;
        }

        // 第三优先级：尝试在Hotbar的空槽位创建新堆叠
        if (TryCreateNewStack(hotbarSlots, item))
        {
            return;
        }

        // 第四优先级：尝试在Inventory的空槽位创建新堆叠
        if (TryCreateNewStack(baggageSlots, item))
        {
            return;
        }

        // 背包已满，无法拾取
        Debug.LogWarning($"[Inventory] 背包已满，无法拾取物品: {item.name}");
    }

    /// <summary>
    /// 尝试将物品添加到现有堆叠中
    /// </summary>
    /// <param name="slots">槽位数组</param>
    /// <param name="item">要添加的物品</param>
    /// <returns>是否成功添加</returns>
    private bool TryAddToExistingStack(InventorySlot[] slots, Item item)
    {
        if (slots == null) return false;

        foreach (InventorySlot slot in slots)
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
    /// <param name="slots">槽位数组</param>
    /// <param name="item">要创建的新物品</param>
    /// <returns>是否成功创建</returns>
    private bool TryCreateNewStack(InventorySlot[] slots, Item item)
    {
        if (slots == null) return false;

        foreach (InventorySlot slot in slots)
        {
            if (slot == null) continue;
            if (slot.item != null) continue;

            // 确定正确的父物体
            Transform actualParent = itemParent;

            if (actualParent == null)
            {
                foreach (InventorySlot checkSlot in baggageSlots)
                {
                    if (checkSlot != null && checkSlot.item != null)
                    {
                        actualParent = checkSlot.item.transform.parent;
                        break;
                    }
                }

                if (actualParent == null)
                {
                    foreach (InventorySlot checkSlot in hotbarSlots)
                    {
                        if (checkSlot != null && checkSlot.item != null)
                        {
                            actualParent = checkSlot.item.transform.parent;
                            break;
                        }
                    }
                }
            }

            if (actualParent == null)
            {
                Debug.LogError("[Inventory] 无法找到物品的父物体！");
                return false;
            }

            // 创建新物品
            InventoryItem inventoryItem = Instantiate(itemPrefab, actualParent);

            inventoryItem.transform.position = slot.transform.position;
            inventoryItem.itemName = item.name;
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
}