using UnityEngine;
using UnityEditor;
#if UNITY_EDITOR

/// <summary>
/// 自定义编辑器类，用于在Unity编辑器中为World组件提供自定义的Inspector界面
/// </summary>
[CustomEditor(typeof(World))]
public class WorldCustomEditor : Editor
{
    /// <summary>
    /// 重写Inspector GUI绘制方法，用于自定义World组件在Inspector面板中的显示内容
    /// </summary>
    public override void OnInspectorGUI() 
    {
        // 绘制默认的Inspector界面
        DrawDefaultInspector();
        
        // 添加一个生成按钮，点击后调用World实例的Generate方法
        if (GUILayout.Button("Generate (Long Lag)")) 
        {
            if (World.instance)
            {
                World.instance.Generate();
                
            }
            else
            {
                Debug.LogWarning("Please reload the scene");
            }
        }
    }
}

#endif
