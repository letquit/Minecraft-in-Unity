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
    /// 用于检测鼠标是否在有效操作区域内。
    /// </summary>
    [Header("Interactive Areas")]
    public RectTransform[] interactiveAreas;
    
    /// <summary>
    /// 标记鼠标是否在任意可交互区域内。
    /// </summary>
    private bool isMouseInsideInteractiveArea;
    
    /// <summary>
    /// 当前高亮的槽位，用于跟踪并在切换时关闭上一个高亮。
    /// </summary>
    private InventorySlot currentHighlightedSlot;
    
    /// <summary>
    /// 槽位高亮检测的最大距离阈值。
    /// </summary>
    [Header("Highlight Settings")]
    public float highlightDistanceThreshold = 60f;
    
    /// <summary>
    /// 初始化方法，获取游戏窗口的RectTransform组件。
    /// </summary>
    private void Start()
    {
        windowRect = canvas.GetComponent<RectTransform>();
        
        foreach (InventorySlot slot in slots)
        {
            if (slot.item != null)
            {
                AddItemTriggers(slot.item);
            }
        }
    }
    
    /// <summary>
    /// 每帧更新方法，处理按键输入和物品拖拽逻辑。
    /// </summary>
    private void Update()
    {
        // E键切换物品栏开关
        if (Input.GetKeyDown(KeyCode.E))
        {
            ToggleInventory();
        }
        
        // 更新槽位高亮效果
        UpdateSlotHighlight();

        // 处理物品拖拽相关操作
        if (draggedItem) 
        {
            // 将拖动物品置于层级最上层以保证渲染顺序正确
            draggedItem.transform.SetSiblingIndex(draggedItem.transform.parent.childCount - 1);
            
            // 持续更新物品位置跟随鼠标，同时更新isMouseInsideWindow和isMouseInsideInteractiveArea状态
            UpdateDraggedItemPosition();

            // 右键点击时尝试拆分堆叠数量大于1的物品
            // 只有在可交互区域内才能执行拆分操作
            // 使用justPickedUp标志防止右键拾取后同一帧内又执行拆分
            if (Input.GetButtonDown("Fire2") && isMouseInsideInteractiveArea && !justPickedUp) 
            {
                TrySplitItem();
            }

            // 左键点击时放置物品（点击式拖拽：再次点击放下）
            // 只有在可交互区域内才能执行放置操作
            // 使用justPickedUp标志防止拾取和放置在同一帧发生
            if (Input.GetButtonDown("Fire1")&& isMouseInsideInteractiveArea && !justPickedUp )
            {
                Drop(draggedItem);
            }
        }
        
        // 在帧末重置拾取标志，确保下一帧可以正常放置
        justPickedUp = false;
    }
    
    /// <summary>
    /// 更新槽位高亮效果。
    /// 每帧检测鼠标位置，找到最近的槽位并显示高亮。
    /// 确保同一时间只有一个槽位显示高亮。
    /// </summary>
    private void UpdateSlotHighlight()
    {
        // 如果物品栏未打开，清除高亮并返回
        if (!open)
        {
            ClearHighlight();
            return;
        }
        
        // 查找鼠标下方最近的槽位
        InventorySlot nearestSlot = GetNearestSlotToMouse();
        
        // 如果最近的槽位和当前高亮的槽位不同，切换高亮
        if (nearestSlot != currentHighlightedSlot)
        {
            // 关闭上一个槽位的高亮
            if (currentHighlightedSlot != null)
            {
                currentHighlightedSlot.SetHighlight(false);
            }
            
            // 显示新槽位的高亮
            if (nearestSlot != null)
            {
                nearestSlot.SetHighlight(true);
            }
            
            // 更新当前高亮槽位引用
            currentHighlightedSlot = nearestSlot;
        }
    }
    
    /// <summary>
    /// 获取距离鼠标最近的槽位。
    /// </summary>
    /// <returns>距离鼠标最近且在阈值范围内的槽位，如果没有则返回null。</returns>
    private InventorySlot GetNearestSlotToMouse()
    {
        float minDistance = float.MaxValue;
        InventorySlot nearestSlot = null;
        
        foreach (InventorySlot slot in slots)
        {
            if (slot == null) continue;
            
            // 获取槽位的屏幕位置
            RectTransform slotRect = slot.GetComponent<RectTransform>();
            if (slotRect == null) continue;
            
            // 检查鼠标是否在槽位的矩形范围内
            if (RectTransformUtility.RectangleContainsScreenPoint(slotRect, Input.mousePosition, canvas.worldCamera))
            {
                // 鼠标在槽位内，直接返回该槽位
                return slot;
            }
            
            // 计算鼠标到槽位中心的距离
            Vector2 slotScreenPos = RectTransformUtility.WorldToScreenPoint(canvas.worldCamera, slot.transform.position);
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
    /// 尝试拆分当前拖拽的物品或向目标槽位放入单个物品。
    /// 右键点击逻辑：
    /// - 对着不同物品：直接交换（与左键行为一致）
    /// - 对着相同且未满的物品：向槽位放入1个
    /// - 对着相同且已满的物品：直接交换
    /// - 对着空槽位：拆分1个直接放入槽位
    /// </summary>
    private void TrySplitItem()
    {
        if (draggedItem == null) return;
        
        // 寻找最近的槽位
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
        
        // 目标槽位有物品
        if (targetSlot.item != null)
        {
            // 对着不同物品右键：直接交换（与左键行为一致）
            if (draggedItem.itemName != targetSlot.item.itemName)
            {
                Drop(draggedItem);
                return;
            }
            
            // 对着相同物品且已满（64个）：直接交换
            if (targetSlot.item.amount >= 64)
            {
                Drop(draggedItem);
                return;
            }
            
            // 对着相同物品且未满：向槽位放入1个（需要手上数量 > 1）
            if (draggedItem.amount > 1)
            {
                targetSlot.item.IncreaseAmount(1);
                draggedItem.IncreaseAmount(-1);
            }
            else
            {
                // 手上只有1个，尝试合并到槽位
                if (targetSlot.item.amount + 1 <= 64)
                {
                    targetSlot.item.IncreaseAmount(1);
                    Destroy(draggedItem.gameObject);
                    draggedItem = null;
                }
            }
            return;
        }
        
        // 目标槽位为空：拆分1个直接放入槽位（需要手上数量 > 1）
        if (draggedItem.amount <= 1)
        {
            return;
        }
        
        GameObject newItemObj = Instantiate(draggedItem.gameObject, parent: draggedItem.transform.parent);
        InventoryItem newItem = newItemObj.GetComponent<InventoryItem>();
        AddItemTriggers(newItem);
        newItem.SetAmount(1);
        draggedItem.IncreaseAmount(-1);
        
        // 直接放入空槽位（不调用Drop，避免位置判断问题）
        newItem.transform.position = targetSlot.transform.position;
        newItem.slot = targetSlot;
        newItem.lastSlot = targetSlot;
        targetSlot.item = newItem;
    }
    
    /// <summary>
    /// 当鼠标在可交互区域外点击左键时调用。
    /// 预留方法，可用于实现丢弃所有物品功能。
    /// </summary>
    private void OnLeftClickOutsideInteractiveArea()
    {
        // 预留方法：可用于实现所有物品丢弃功能
    }
    
    /// <summary>
    /// 当鼠标在可交互区域外点击右键时调用。
    /// 预留方法，可用于实现丢弃单个物品功能。
    /// </summary>
    private void OnRightClickOutsideInteractiveArea()
    {
        // 预留方法：可用于实现单个物品丢弃功能
    }
    
    /// <summary>
    /// 更新被拖拽物品的位置，使其跟随鼠标移动。
    /// 当鼠标移出窗口时，物品会停留在窗口边缘；
    /// 当鼠标重新进入窗口时，物品会立即跳转到鼠标位置。
    /// 同时更新isMouseInsideWindow和isMouseInsideInteractiveArea状态。
    /// </summary>
    private void UpdateDraggedItemPosition()
    {
        if (draggedItem == null || canvas == null || windowRect == null) return;
        
        // 获取鼠标在Canvas中的本地坐标
        Vector2 mouseLocalPosition;
        RectTransformUtility.ScreenPointToLocalPointInRectangle(
            (RectTransform)canvas.transform, 
            Input.mousePosition, 
            canvas.worldCamera, 
            out mouseLocalPosition);
        
        // 获取窗口在Canvas中的本地坐标和尺寸
        Vector2 windowLocalPosition = windowRect.localPosition;
        Vector2 windowSize = windowRect.rect.size;
        Vector2 windowPivot = windowRect.pivot;
        
        // 计算窗口边界（考虑pivot偏移）
        float windowLeft = windowLocalPosition.x - windowSize.x * windowPivot.x;
        float windowRight = windowLocalPosition.x + windowSize.x * (1 - windowPivot.x);
        float windowBottom = windowLocalPosition.y - windowSize.y * windowPivot.y;
        float windowTop = windowLocalPosition.y + windowSize.y * (1 - windowPivot.y);
        
        // 检查鼠标是否在窗口内（用于限制物品位置）
        isMouseInsideWindow = mouseLocalPosition.x >= windowLeft && 
                              mouseLocalPosition.x <= windowRight &&
                              mouseLocalPosition.y >= windowBottom && 
                              mouseLocalPosition.y <= windowTop;
        
        // 检查鼠标是否在任意可交互区域内（用于限制操作）
        isMouseInsideInteractiveArea = CheckMouseInsideInteractiveAreas();
        
        // 计算物品的目标位置
        Vector2 targetLocalPosition;
        
        if (isMouseInsideWindow)
        {
            // 鼠标在窗口内，物品直接跟随鼠标
            targetLocalPosition = mouseLocalPosition;
        }
        else
        {
            // 鼠标在窗口外，将物品位置限制在窗口边缘
            targetLocalPosition = new Vector2(
                Mathf.Clamp(mouseLocalPosition.x, windowLeft, windowRight),
                Mathf.Clamp(mouseLocalPosition.y, windowBottom, windowTop)
            );
        }
        
        // 应用位置
        draggedItem.transform.position = canvas.transform.TransformPoint(targetLocalPosition);
    }
    
    /// <summary>
    /// 检查鼠标是否在任意可交互区域内。
    /// </summary>
    /// <returns>如果鼠标在任意可交互区域内返回true，否则返回false。</returns>
    private bool CheckMouseInsideInteractiveAreas()
    {
        // 如果没有设置可交互区域，默认使用isMouseInsideWindow
        if (interactiveAreas == null || interactiveAreas.Length == 0)
        {
            return isMouseInsideWindow;
        }
        
        // 检查鼠标是否在任意可交互区域内
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
    /// 切换物品栏窗口的开启与关闭状态，并同步设置光标可见性和锁定模式。
    /// </summary>
    public void ToggleInventory() 
    {
        bool enabled = !window.gameObject.activeSelf;
        window.gameObject.SetActive(enabled);
        Cursor.visible = enabled;
        open = enabled;
        
        // 关闭物品栏时，如果有正在拖拽的物品，放回原位
        if (!enabled && draggedItem != null)
        {
            Drop(draggedItem);
        }
        
        // 关闭物品栏时清除高亮
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
    /// 开始拖动指定的物品项（左键点击拾取全部）。
    /// 如果已经有物品在拖拽中，则尝试与目标物品交换或合并。
    /// </summary>
    /// <param name="item">要开始拖动的物品实例。</param>
    /// <param name="isLeftClick">是否是左键点击，用于区分左右键行为。</param>
    public void StartDrag(InventoryItem item, bool isLeftClick = true) 
    {
        // 如果是右键点击且手上没有物品，应该走右键拾取逻辑
        if (!isLeftClick && draggedItem == null)
        {
            StartRightClickDrag(item);
            return;
        }
        
        // 如果已经有物品在拖拽
        if (draggedItem != null)
        {
            // 如果点击的是同一个物品，忽略（放置由Update中的逻辑处理）
            if (draggedItem == item)
            {
                return;
            }
            
            // 尝试与点击的槽位物品交换或合并
            if (item.slot != null)
            {
                TrySwapOrMerge(draggedItem, item);
                return;
            }
        }
        
        // 拾取新物品（全部拾取）
        draggedItem = item;
        if (item.slot)
        {
            item.slot.item = null;
        }
        item.slot = null;
        
        // 设置拾取标志，防止同一帧内立即放下
        justPickedUp = true;
    }
    
    /// <summary>
    /// 尝试交换或合并两个物品。
    /// </summary>
    /// <param name="dragged">当前拖拽的物品。</param>
    /// <param name="target">目标槽位中的物品。</param>
    private void TrySwapOrMerge(InventoryItem dragged, InventoryItem target)
    {
        InventorySlot targetSlot = target.slot;
        
        // 检查是否可以合并（相同物品且未满堆叠）
        if (dragged.itemName == target.itemName && target.amount + dragged.amount <= 64)
        {
            // 合并物品
            target.IncreaseAmount(dragged.amount);
            Destroy(dragged.gameObject);
            draggedItem = null;
        }
        else if (dragged.itemName == target.itemName && target.amount < 64)
        {
            // 部分合并
            int spaceLeft = 64 - target.amount;
            target.IncreaseAmount(spaceLeft);
            dragged.IncreaseAmount(-spaceLeft);
            // 继续拖拽剩余的
        }
        else
        {
            // 交换物品
            InventorySlot originalSlot = dragged.lastSlot;
            
            // 将目标物品放到拖拽物品的原位置
            if (originalSlot != null)
            {
                target.transform.position = originalSlot.transform.position;
                target.slot = originalSlot;
                target.lastSlot = originalSlot;
                originalSlot.item = target;
            }
            
            // 将拖拽物品放到目标槽位
            dragged.transform.position = targetSlot.transform.position;
            dragged.slot = targetSlot;
            dragged.lastSlot = targetSlot;
            targetSlot.item = dragged;
            
            draggedItem = null;
        }
    }

    /// <summary>
    /// 在拖动过程中根据鼠标指针的位置实时更新物品的位置。
    /// 注意：在点击拾取模式下，此方法仅作为EventTrigger的回调保留，
    /// 实际位置更新由UpdateDraggedItemPosition方法在Update中处理。
    /// </summary>
    /// <param name="data">事件数据对象，包含当前指针信息。</param>
    public void Drag(BaseEventData data) 
    {
        // 在点击拾取模式下，位置更新由UpdateDraggedItemPosition处理
        // 此方法保留以兼容EventTrigger系统
    }

    /// <summary>
    /// 停止拖动并决定将物品放入哪个槽位中。
    /// 若目标槽为空则放入；若目标槽有相同物品则合并；若目标槽有不同物品则交换（手上物品与槽内物品互换）。
    /// </summary>
    /// <param name="item">停止拖动的物品实例。</param>
    public void Drop(InventoryItem item) 
    {
        // 寻找最近的有效槽位
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
        
        // 若目标槽位有物品
        if (targetSlot.item)
        {
            // 相同物品：合并（总数量不超过上限）
            if (item.itemName == targetSlot.item.itemName && targetSlot.item.amount + item.amount <= 64) 
            {
                targetSlot.item.IncreaseAmount(item.amount);
                Destroy(item.gameObject);
                
                // 合并后手上没有物品了，清除拖拽引用
                if (draggedItem == item)
                {
                    draggedItem = null;
                }
            } 
            // 相同物品：部分合并（目标槽位未满但放入后会超过上限）
            else if (item.itemName == targetSlot.item.itemName && targetSlot.item.amount < 64)
            {
                int spaceLeft = 64 - targetSlot.item.amount;
                targetSlot.item.IncreaseAmount(spaceLeft);
                item.IncreaseAmount(-spaceLeft);
                
                // 部分合并后手上还有剩余物品，继续拖拽
                // draggedItem 保持不变
            }
            // 不同物品或相同物品但目标已满：执行交换（手上物品与槽内物品互换）
            else 
            {
                // 获取目标槽位中的物品
                InventoryItem targetItem = targetSlot.item;
                // 获取拖拽物品的原槽位
                InventorySlot originalSlot = item.lastSlot;
                
                // 将手上的物品放入目标槽位
                item.transform.position = targetSlot.transform.position;
                item.slot = targetSlot;
                item.lastSlot = targetSlot;
                targetSlot.item = item;
                
                // 将原槽内的物品变成手上拿着的（继续拖拽）
                targetItem.slot = null;
                targetItem.lastSlot = originalSlot; // 记录原槽位，用于之后放回
                draggedItem = targetItem; // 将原槽内物品设为当前拖拽物品
            }
        }
        else
        {
            // 目标槽空时直接放入
            item.transform.position = targetSlot.transform.position;
            item.slot = targetSlot;
            targetSlot.item = item;
            
            // 放入后手上没有物品了，清除拖拽引用
            if (draggedItem == item)
            {
                draggedItem = null;
            }
        }
    }

    /// <summary>
    /// 给新创建的物品添加必要的UI交互触发器。
    /// 包括左键点击（拾取全部）和右键点击（拾取一半）事件。
    /// </summary>
    /// <param name="item">需要绑定事件的新物品实例。</param>
    private void AddItemTriggers(InventoryItem item) 
    {
        EventTrigger trigger = item.GetComponent<EventTrigger>();
        if (trigger == null)
        {
            trigger = item.gameObject.AddComponent<EventTrigger>();
        }
        trigger.triggers.Clear();
        
        // PointerDown事件：区分左键和右键
        EventTrigger.Entry pointerDownEntry = new EventTrigger.Entry();
        pointerDownEntry.eventID = EventTriggerType.PointerDown;
        pointerDownEntry.callback.AddListener((eventData) =>
        {
            PointerEventData pointerData = (PointerEventData)eventData;
            InventoryItem targetItem = item.GetComponent<InventoryItem>();
            
            if (pointerData.button == PointerEventData.InputButton.Left)
            {
                // 左键：拾取全部或交换/合并
                StartDrag(targetItem, true);
            }
            else if (pointerData.button == PointerEventData.InputButton.Right)
            {
                // 右键：拾取一半或放置一个
                StartDrag(targetItem, false);
            }
        });
        trigger.triggers.Add(pointerDownEntry);
    }
    
    /// <summary>
    /// 右键点击槽内物品时的处理逻辑。
    /// 当手上没有物品时，拾取槽内物品的一半数量（向上取整）。
    /// </summary>
    /// <param name="item">被右键点击的物品实例。</param>
    public void StartRightClickDrag(InventoryItem item)
    {
        // 如果手上已经有物品，不处理拾取逻辑（由Update中的TrySplitItem处理）
        if (draggedItem != null)
        {
            return;
        }
        
        // 手上没有物品，执行右键拾取逻辑
        if (item == null || item.slot == null) return;
        
        // 物品数量为1时，直接全部拾取（与左键相同）
        if (item.amount <= 1)
        {
            StartDrag(item, true);
            return;
        }
        
        // 计算拾取数量：拾取一半（向上取整）
        int originalAmount = item.amount;
        int pickupAmount = (originalAmount + 1) / 2; // 向上取整
        int remainAmount = originalAmount - pickupAmount; // 槽内剩余数量
        
        // 记录原槽位
        InventorySlot originalSlot = item.slot;
        
        // 如果剩余数量为0，直接拾取全部（不需要创建新物品）
        if (remainAmount <= 0)
        {
            StartDrag(item, true);
            return;
        }
        
        // 先创建新物品（在修改原物品数量之前），复制当前的完整数量
        GameObject remainItemObj = Instantiate(item.gameObject, parent: item.transform.parent);
        InventoryItem remainItem = remainItemObj.GetComponent<InventoryItem>();
        
        // 检查amountText是否被正确复制（应该是不同的实例）
        if (item.amountText != null && remainItem.amountText != null && 
            item.amountText.GetInstanceID() == remainItem.amountText.GetInstanceID())
        {
            // 尝试从子对象中找到正确的 TextMeshProUGUI
            remainItem.amountText = remainItemObj.GetComponentInChildren<TextMeshProUGUI>();
        }
        
        // 重新绑定事件
        AddItemTriggers(remainItem);
        
        // 设置槽内剩余物品（新创建的）
        remainItem.SetAmount(remainAmount);
        remainItem.transform.position = originalSlot.transform.position;
        remainItem.slot = originalSlot;
        remainItem.lastSlot = originalSlot;
        originalSlot.item = remainItem;
        
        // 设置手上拿的物品（原物品）
        item.SetAmount(pickupAmount);
        item.slot = null;
        item.lastSlot = originalSlot;
        
        // 设置为当前拖拽物品
        draggedItem = item;
        
        // 设置拾取标志，防止同一帧内立即放下
        justPickedUp = true;
    }
}