using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// HUD快捷栏管理器，负责游戏界面下方快捷栏的显示和交互
/// </summary>
public class HUD : MonoBehaviour
{
    [Header("UI References")] [Tooltip("快捷栏窗口")]
    public GameObject window;

    [Tooltip("快捷栏槽位数组（9个）")] public InventorySlot[] hotbarSlots;

    [Tooltip("物品预制体")] public InventoryItem itemPrefab;

    [Tooltip("物品父级Transform")] public Transform itemParent;

    [Tooltip("选取框Image")] public Image selector;

    [Header("References")] [Tooltip("玩家背包引用")]
    public Inventory playerInventory;

    [Tooltip("玩家引用")] public Player player;

    /// <summary>
    /// 当前选中的槽位索引
    /// </summary>
    private int selectedSlotIndex = 0;

    /// <summary>
    /// 初始化
    /// </summary>
    private void Start()
    {
        // 初始时显示快捷栏
        if (window != null)
        {
            window.SetActive(true);
        }

        // 初始同步一次
        SyncFromInventory();

        // 延迟一帧初始化选取框位置，确保UI布局完成
        StartCoroutine(InitializeSelectorPosition());
    }

    /// <summary>
    /// 延迟初始化选取框位置
    /// </summary>
    private System.Collections.IEnumerator InitializeSelectorPosition()
    {
        // 等待一帧，确保UI布局完成
        yield return null;

        UpdateSelectorPosition();
        UpdatePlayerActiveBlock();
    }

    /// <summary>
    /// 每帧更新
    /// </summary>
    private void Update()
    {
        // 检查UI是否打开
        bool uiOpen = IsAnyUIOpen();

        // 控制快捷栏显示/隐藏
        if (window != null)
        {
            window.SetActive(!uiOpen);
        }

        // 如果UI打开，不处理快捷栏逻辑
        if (uiOpen) return;

        // 同步背包数据
        SyncFromInventory();

        // 处理滚轮选择
        HandleScrollSelection();

        // 处理数字键选择
        HandleNumberKeySelection();

        // 更新玩家的activeBlock
        UpdatePlayerActiveBlock();
    }

    /// <summary>
    /// 检查是否有任何UI界面打开
    /// </summary>
    /// <returns>如果有UI打开则返回true</returns>
    private bool IsAnyUIOpen()
    {
        if (Inventory.open) return true;

        if (player != null && player.uis != null)
        {
            foreach (UI ui in player.uis)
            {
                if (ui != null && ui.isOpen)
                {
                    return true;
                }
            }
        }

        return false;
    }

    /// <summary>
    /// 从玩家背包同步物品到HUD快捷栏
    /// </summary>
    private void SyncFromInventory()
    {
        if (playerInventory == null || playerInventory.hotbarSlots == null) return;

        // 清除当前显示
        ClearHotbarDisplay();

        // 从背包同步物品
        for (int i = 0; i < hotbarSlots.Length && i < playerInventory.hotbarSlots.Length; i++)
        {
            InventorySlot sourceSlot = playerInventory.hotbarSlots[i];
            InventorySlot targetSlot = hotbarSlots[i];

            if (sourceSlot == null || targetSlot == null) continue;

            if (sourceSlot.item != null)
            {
                // 在HUD槽位创建物品显示副本
                InventoryItem newItem = Instantiate(itemPrefab, itemParent);
                newItem.transform.position = targetSlot.transform.position;
                newItem.itemName = sourceSlot.item.itemName;
                newItem.scriptableItem = sourceSlot.item.scriptableItem;
                newItem.SetSprite(sourceSlot.item.icon);
                newItem.SetAmount(sourceSlot.item.amount);

                newItem.slot = targetSlot;
                newItem.lastSlot = targetSlot;
                targetSlot.item = newItem;
            }
        }
    }

    /// <summary>
    /// 清除HUD快捷栏显示的物品
    /// </summary>
    private void ClearHotbarDisplay()
    {
        if (hotbarSlots == null) return;

        foreach (InventorySlot slot in hotbarSlots)
        {
            if (slot == null) continue;

            if (slot.item != null)
            {
                Destroy(slot.item.gameObject);
                slot.item = null;
            }
        }
    }

    /// <summary>
    /// 处理滚轮选择
    /// </summary>
    private void HandleScrollSelection()
    {
        float scroll = Input.mouseScrollDelta.y;

        if (scroll > 0)
        {
            // 向上滚动，索引减少
            selectedSlotIndex--;
            if (selectedSlotIndex < 0)
            {
                selectedSlotIndex = hotbarSlots.Length - 1;
            }

            UpdateSelectorPosition();
        }
        else if (scroll < 0)
        {
            // 向下滚动，索引增加
            selectedSlotIndex++;
            if (selectedSlotIndex >= hotbarSlots.Length)
            {
                selectedSlotIndex = 0;
            }

            UpdateSelectorPosition();
        }
    }

    /// <summary>
    /// 处理数字键选择（1-9）
    /// </summary>
    private void HandleNumberKeySelection()
    {
        for (int i = 0; i < 9; i++)
        {
            if (Input.GetKeyDown(KeyCode.Alpha1 + i))
            {
                selectedSlotIndex = i;
                UpdateSelectorPosition();
                break;
            }
        }
    }

    /// <summary>
    /// 更新选取框位置
    /// </summary>
    private void UpdateSelectorPosition()
    {
        if (selector == null || hotbarSlots == null) return;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < hotbarSlots.Length)
        {
            selector.transform.position = hotbarSlots[selectedSlotIndex].transform.position;
        }
    }

    /// <summary>
    /// 更新玩家的activeBlock
    /// </summary>
    private void UpdatePlayerActiveBlock()
    {
        if (player == null || playerInventory == null) return;

        // 检查背包中对应槽位是否有物品
        if (selectedSlotIndex >= 0 && selectedSlotIndex < playerInventory.hotbarSlots.Length)
        {
            InventorySlot slot = playerInventory.hotbarSlots[selectedSlotIndex];

            if (slot != null && slot.item != null && slot.item.scriptableItem != null)
            {
                // 获取物品对应的方块
                Item item = slot.item.scriptableItem;

                // 尝试通过物品名称查找对应的方块
                Block block = GetBlockFromItem(item);
                player.activeBlock = block;
            }
            else
            {
                // 槽位为空，清除activeBlock
                player.activeBlock = null;
            }
        }
    }

    /// <summary>
    /// 从物品获取对应的方块
    /// </summary>
    /// <param name="item">物品数据</param>
    /// <returns>对应的方块组件，如果没有则返回null</returns>
    private Block GetBlockFromItem(Item item)
    {
        if (item == null) return null;

        // 优先使用 Item 中直接配置的 block 字段
        if (item.block != null)
        {
            return item.block;
        }

        // 如果没有配置 block 字段，尝试从 model 获取（兼容旧数据）
        if (item.model != null)
        {
            Block block = item.model.GetComponent<Block>();
            return block;
        }

        return null;
    }

    /// <summary>
    /// 减少当前选中槽位的物品数量
    /// </summary>
    /// <param name="decrement">减少的数量，默认为1</param>
    public void DecreaseSelectedItem(int decrement = 1)
    {
        if (playerInventory == null) return;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < playerInventory.hotbarSlots.Length)
        {
            InventorySlot slot = playerInventory.hotbarSlots[selectedSlotIndex];

            if (slot != null && slot.item != null)
            {
                slot.item.IncreaseAmount(-decrement);

                // 如果物品数量为0，IncreaseAmount会自动销毁物品
                // 需要清除玩家的activeBlock
                if (slot.item == null || slot.item.amount <= 0)
                {
                    player.activeBlock = null;
                }
            }
        }
    }

    /// <summary>
    /// 获取当前选中的物品
    /// </summary>
    /// <returns>当前选中的物品，如果没有则返回null</returns>
    public InventoryItem GetSelectedItem()
    {
        if (playerInventory == null) return null;

        if (selectedSlotIndex >= 0 && selectedSlotIndex < playerInventory.hotbarSlots.Length)
        {
            InventorySlot slot = playerInventory.hotbarSlots[selectedSlotIndex];
            return slot?.item;
        }

        return null;
    }

    /// <summary>
    /// 检查当前选中槽位是否有物品
    /// </summary>
    /// <returns>如果有物品则返回true</returns>
    public bool HasSelectedItem()
    {
        return GetSelectedItem() != null;
    }
}