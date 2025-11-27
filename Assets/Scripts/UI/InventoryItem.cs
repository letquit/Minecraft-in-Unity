using TMPro;
using UnityEngine;

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
    
    [HideInInspector]
    public InventorySlot lastSlot;
    
    /// <summary>
    /// 每帧更新方法，用于记录物品当前所在的槽位。
    /// </summary>
    private void Update() 
    {
        // 如果当前槽位不为空，则更新最后槽位记录
        if (slot != null)
        {
            lastSlot = slot;
        }
    }

    /// <summary>
    /// 设置物品数量并更新显示文本。
    /// </summary>
    /// <param name="newAmount">要设置的物品数量。</param>
    public void SetAmount(int newAmount) 
    {
        this.amount = Mathf.Max(0, newAmount);
        
        // 当物品数量小于等于0时，从槽位中移除并销毁对象
        if (this.amount <= 0)
        {
            // 数量为0时销毁物品
            if (slot != null)
            {
                slot.item = null;
            }
            Destroy(gameObject);
            return;
        }
        
        // 当数量为1时隐藏数量显示，否则显示具体数量
        if (amountText != null)
        {
            amountText.text = this.amount == 1 ? "" : this.amount.ToString();
        }
    }
    
    /// <summary>
    /// 增加物品数量。
    /// </summary>
    /// <param name="increment">要增加的数量值。</param>
    public void IncreaseAmount(int increment) 
    {
        SetAmount(amount + increment);
    }
}