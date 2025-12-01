using System;
using System.IO;
using UnityEngine;

public class Screenshot : MonoBehaviour
{
    private void Update()
    {
        if (Input.GetKeyDown(KeyCode.F2))
        {
            // 获取截图保存路径
            string screenshotPath = GetScreenshotPath();
            
            // 确保目录存在
            string directory = Path.GetDirectoryName(screenshotPath);
            if (!Directory.Exists(directory))
            {
                Directory.CreateDirectory(directory);
            }
            
            // 截图
            // ScreenCapture.CaptureScreenshot(screenshotPath, 4);
            ScreenCapture.CaptureScreenshot(screenshotPath);
            
            Debug.Log($"Screenshot saved to: {screenshotPath}");
        }
    }
    
    /// <summary>
    /// 获取截图保存路径，根据不同平台返回相应的路径
    /// </summary>
    /// <returns>截图文件的完整路径</returns>
    private string GetScreenshotPath()
    {
        // 生成文件名
        string fileName = "screenshot-" + DateTime.Now.ToString("yyyy-MM-dd-HH-mm-ss") + ".png";
        
#if UNITY_EDITOR
        // 编辑器模式下保存在 Assets/Screenshots 文件夹内
        string screenshotsFolder = Path.Combine(Application.dataPath, "Screenshots");
#else
        // 发布版本保存在游戏根目录下的 Screenshots 文件夹
        string screenshotsFolder = Path.Combine(Application.persistentDataPath, "Screenshots");
#endif
        
        return Path.Combine(screenshotsFolder, fileName);
    }
}
