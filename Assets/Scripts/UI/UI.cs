using UnityEngine;
using UnityEngine.UI;

/// <summary>
/// UI界面基类，用于管理各种UI窗口的显示和隐藏
/// </summary>
public class UI : MonoBehaviour
{
    /// <summary>
    /// UI窗口图像组件
    /// </summary>
    public Image window;
    
    /// <summary>
    /// 标记UI是否处于打开状态
    /// </summary>
    [HideInInspector]
    public bool isOpen;
    
    /// <summary>
    /// 切换UI的显示状态
    /// 控制UI窗口的显示与隐藏，并同步设置鼠标光标的可见性和锁定状态
    /// </summary>
    public virtual void Toggle()
    {
        bool enabled = !window.gameObject.activeSelf;
        window.gameObject.SetActive(enabled);
        Cursor.visible = enabled;
        isOpen = enabled;
        
        // 根据UI状态设置鼠标锁定模式
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
    /// 打开UI
    /// 当UI当前处于关闭状态时，调用Toggle方法打开UI
    /// </summary>
    public virtual void Open()
    {
        if (!isOpen)
        {
            Toggle();
        }
    }
    
    /// <summary>
    /// 关闭UI
    /// 当UI当前处于打开状态时，调用Toggle方法关闭UI
    /// </summary>
    public virtual void Close()
    {
        if (isOpen)
        {
            Toggle();
        }
    }
}