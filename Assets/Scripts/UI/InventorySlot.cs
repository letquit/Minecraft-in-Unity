using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// 物品槽类，用于表示背包系统中的单个物品槽位。
/// </summary>
public class InventorySlot : MonoBehaviour
{
    /// <summary>
    /// 当前物品槽中存储的物品对象。
    /// 可以在Unity编辑器中直接赋值或通过代码设置。
    /// </summary>
    public InventoryItem item;
    
    /// <summary>
    /// 槽位高亮显示的Image组件。
    /// 当鼠标悬停在槽位上时显示。
    /// </summary>
    [Header("Highlight Settings")]
    public Image highlightImage;
    
    /// <summary>
    /// 槽位边框的Outline组件。
    /// 当鼠标悬停在槽位上时显示。
    /// </summary>
    public Outline highlightOutline;
    
    /// <summary>
    /// 初始化时隐藏高亮效果。
    /// </summary>
    private void Start()
    {
        SetHighlight(false);
    }
    
    /// <summary>
    /// 设置槽位高亮效果的显示状态。
    /// 通过改变透明度（1为显示，0为隐藏）来控制Image和Outline的显示/隐藏。
    /// </summary>
    /// <param name="show">是否显示高亮效果。</param>
    public void SetHighlight(bool show)
    {
        float alpha = show ? 1f : 0f;
        
        // 通过透明度控制Image显示
        if (highlightImage != null)
        {
            Color imageColor = highlightImage.color;
            highlightImage.color = new Color(imageColor.r, imageColor.g, imageColor.b, alpha);
        }
        
        // 通过透明度控制Outline显示
        if (highlightOutline != null)
        {
            Color outlineColor = highlightOutline.effectColor;
            highlightOutline.effectColor = new Color(outlineColor.r, outlineColor.g, outlineColor.b, alpha);
        }
    }
}