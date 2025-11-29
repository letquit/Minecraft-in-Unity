using TMPro;
using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 库存物品类，用于管理游戏中的物品信息和显示。
/// </summary>
public class InventoryItem : MonoBehaviour
{
    public string itemName;
    public Sprite icon;
    public InventorySlot slot;
    public TextMeshProUGUI amountText;
    public int amount;

    [HideInInspector] public InventorySlot lastSlot;
    
    [HideInInspector] public Item scriptableItem;
    
    [HideInInspector] public bool justCrafted;

    /// <summary>
    /// 每帧更新方法，用于记录物品当前所在的槽位。
    /// </summary>
    private void Update()
    {
        // 如果当前槽位不为空，则更新最后所在槽位的引用
        if (slot != null)
        {
            lastSlot = slot;
        }
    }

    /// <summary>
    /// 设置物品数量并更新显示文本。
    /// 当数量小于等于0时会销毁该物品对象，并清空所在槽位的引用。
    /// </summary>
    /// <param name="newAmount">要设置的物品数量。</param>
    public void SetAmount(int newAmount)
    {
        // 确保数量不会小于0
        this.amount = Mathf.Max(0, newAmount);

        // 如果数量为0或更少，清理槽位引用并销毁自身对象
        if (this.amount <= 0)
        {
            if (slot != null)
            {
                slot.item = null;
            }

            Destroy(gameObject);
            return;
        }

        // 更新界面上的数量文本显示（数量为1时不显示）
        if (amountText != null)
        {
            amountText.text = this.amount == 1 ? "" : this.amount.ToString();
        }
    }

    /// <summary>
    /// 增加物品数量。
    /// 调用SetAmount方法来实际修改数量并处理相关逻辑。
    /// </summary>
    /// <param name="increment">要增加的数量值。</param>
    public void IncreaseAmount(int increment)
    {
        SetAmount(amount + increment);
    }

    /// <summary>
    /// 设置物品的图标精灵。
    /// 同时更新组件上的Image组件显示图像。
    /// </summary>
    /// <param name="sprite">要设置的精灵图像。</param>
    public void SetSprite(Sprite sprite)
    {
        icon = sprite;

        Image image = GetComponent<Image>();
        if (image != null)
        {
            image.sprite = sprite;
        }
    }
}