using UnityEngine;
using UnityEngine.EventSystems;

/// <summary>
/// 合成系统类，处理配方检测、合成物品生成等逻辑
/// </summary>
public class CraftingSystem
{
    private InventorySlot[] craftingSlots;
    private InventorySlot outputSlot;
    private Item[] craftableItems;
    private InventoryItem itemPrefab;
    private Transform itemParent;

    /// <summary>
    /// 构造函数
    /// </summary>
    /// <param name="craftingSlots">合成槽位数组</param>
    /// <param name="outputSlot">输出槽位</param>
    /// <param name="craftableItems">可合成物品列表</param>
    /// <param name="itemPrefab">物品预制体</param>
    /// <param name="itemParent">物品父级Transform</param>
    public CraftingSystem(InventorySlot[] craftingSlots, InventorySlot outputSlot, Item[] craftableItems,
        InventoryItem itemPrefab, Transform itemParent)
    {
        this.craftingSlots = craftingSlots;
        this.outputSlot = outputSlot;
        this.craftableItems = craftableItems;
        this.itemPrefab = itemPrefab;
        this.itemParent = itemParent;
    }

    /// <summary>
    /// 检查当前合成格子中的物品是否能组成某个配方。
    /// 若存在匹配的配方，则在输出槽中显示对应的结果物品；否则清除输出槽中刚合成但尚未取出的物品。
    /// </summary>
    /// <param name="addOutputTriggers">添加输出槽物品触发器的回调</param>
    public void CheckRecipes(System.Action<InventoryItem> addOutputTriggers)
    {
        if (craftingSlots == null || craftingSlots.Length == 0 || outputSlot == null)
            return;

        Recipe currentRecipe = NormalizeRecipe(GridToRecipe());
        bool recipeFound = false;

        foreach (Item item in craftableItems)
        {
            if (item == null || item.recipe.IsEmpty())
                continue;

            Recipe normalizedItemRecipe = NormalizeRecipe(item.recipe);

            if (currentRecipe == normalizedItemRecipe)
            {
                recipeFound = true;

                if (outputSlot.item != null)
                    break;

                InventoryItem outputItem = InstantiateCraftingItem(item, outputSlot);
                addOutputTriggers?.Invoke(outputItem);
                outputItem.justCrafted = true;

                break;
            }
        }

        if (!recipeFound && outputSlot.item != null)
        {
            if (outputSlot.item.justCrafted)
            {
                Object.Destroy(outputSlot.item.gameObject);
                outputSlot.item = null;
            }
        }
    }

    /// <summary>
    /// 对给定的配方进行归一化处理，将其内容向左上角对齐以消除位置差异带来的影响
    /// </summary>
    /// <param name="recipe">需要被归一化的原始配方</param>
    /// <returns>经过归一化处理的新配方对象</returns>
    public static Recipe NormalizeRecipe(Recipe recipe)
    {
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

        if (minRow == 3 || minCol == 3)
        {
            return new Recipe();
        }

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
    /// 根据当前合成格子的内容构建一个Recipe对象。
    /// 支持两种尺寸：2x2 和 3x3 的合成网格。
    /// </summary>
    /// <returns>表示当前合成格子布局的Recipe对象</returns>
    public Recipe GridToRecipe()
    {
        Recipe recipe = new Recipe();

        if (craftingSlots.Length == 4)
        {
            if (craftingSlots[0].item) recipe.topLeft = craftingSlots[0].item.scriptableItem;
            if (craftingSlots[1].item) recipe.topCenter = craftingSlots[1].item.scriptableItem;
            if (craftingSlots[2].item) recipe.middleLeft = craftingSlots[2].item.scriptableItem;
            if (craftingSlots[3].item) recipe.middleCenter = craftingSlots[3].item.scriptableItem;
        }
        else if (craftingSlots.Length == 9)
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
    /// 在指定槽位中实例化一个新的合成结果物品
    /// </summary>
    /// <param name="item">用于创建新物品的数据源</param>
    /// <param name="slot">目标槽位</param>
    /// <returns>新创建的物品实例</returns>
    private InventoryItem InstantiateCraftingItem(Item item, InventorySlot slot)
    {
        Transform actualParent = itemParent ?? slot.transform.parent;

        InventoryItem inventoryItem = Object.Instantiate(itemPrefab, actualParent);
        inventoryItem.transform.position = slot.transform.position;
        inventoryItem.itemName = item.name;
        inventoryItem.scriptableItem = item;
        inventoryItem.SetSprite(item.sprite);

        // 使用配方定义的产出数量
        inventoryItem.SetAmount(item.craftAmount);

        inventoryItem.slot = slot;
        inventoryItem.lastSlot = slot;
        slot.item = inventoryItem;

        return inventoryItem;
    }

    /// <summary>
    /// 消耗合成材料（每个材料减少1个）
    /// </summary>
    public void ConsumeMaterials()
    {
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
                Object.Destroy(craftingSlot.item.gameObject);
                craftingSlot.item = null;
            }
        }
    }

    /// <summary>
    /// 清除所有合成槽中的物品，并将其返还给玩家背包。
    /// 如果输出槽有刚合成的物品也会被清除。
    /// </summary>
    /// <param name="inventory">玩家背包引用</param>
    public void ClearCraftingSlots(Inventory inventory)
    {
        if (craftingSlots == null) return;

        foreach (InventorySlot slot in craftingSlots)
        {
            if (slot == null) continue;

            if (slot.item != null)
            {
                Item itemData = slot.item.scriptableItem;
                int amount = slot.item.amount;
                string itemName = slot.item.itemName;

                if (itemData == null)
                {
                    itemData = ItemUtils.GetItemDataByName(itemName);
                }

                if (itemData != null && inventory != null)
                {
                    for (int i = 0; i < amount; i++)
                    {
                        inventory.GetItem(itemData);
                    }
                }

                Object.Destroy(slot.item.gameObject);
                slot.item = null;
            }
        }

        if (outputSlot != null && outputSlot.item != null && outputSlot.item.justCrafted)
        {
            Object.Destroy(outputSlot.item.gameObject);
            outputSlot.item = null;
        }
    }
}