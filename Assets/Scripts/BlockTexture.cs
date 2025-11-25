using UnityEngine;

[ExecuteInEditMode]
public class BlockTexture : MonoBehaviour
{
    MeshFilter meshFilter;
    Mesh mesh;
    
    void Start() {
        meshFilter = GetComponent<MeshFilter>();
        mesh = meshFilter.sharedMesh;
        Vector2[] uv = mesh.uv;

        // -------------------------------------------------------
        // Front Face (前) - 纹理位置：第3行中间列 (Y:0.25~0.5)
        // 直接映射：UV与纹理坐标方向一致
        // -------------------------------------------------------
        uv[0] = new Vector2(.333f, .25f); // Mesh Bottom-Left → Texture Bottom-Left (X:1/3, Y:1/4)
        uv[1] = new Vector2(.666f, .25f); // Mesh Bottom-Right → Texture Bottom-Right (X:2/3, Y:1/4)
        uv[2] = new Vector2(.333f, .5f);  // Mesh Top-Left → Texture Top-Left (X:1/3, Y:1/2)
        uv[3] = new Vector2(.666f, .5f);  // Mesh Top-Right → Texture Top-Right (X:2/3, Y:1/2)

        // -------------------------------------------------------
        // Top Face (顶) - 纹理位置：第4行中间列 (Y:0.5~0.75)
        // 顶面需上下翻转：Mesh Top → Texture Bottom
        // -------------------------------------------------------
        uv[4] = new Vector2(.333f, .75f); // Mesh Top-Left → Texture Top-Left (X:1/3, Y:3/4)
        uv[5] = new Vector2(.666f, .75f); // Mesh Top-Right → Texture Top-Right (X:2/3, Y:3/4)
        uv[8] = new Vector2(.333f, .5f);  // Mesh Bottom-Left → Texture Bottom-Left (X:1/3, Y:1/2)
        uv[9] = new Vector2(.666f, .5f);  // Mesh Bottom-Right → Texture Bottom-Right (X:2/3, Y:1/2)
        
        // -------------------------------------------------------
        // Back Face (后) - 纹理位置：第5行中间列 (Y:0.75~1.0)
        // 垂直翻转修正：使草皮在上（原UV导致草皮在下）
        // -------------------------------------------------------
        uv[6] = new Vector2(.666f, 1f);   // Mesh Bottom-Right → Texture Top-Right (Dirt at Y:1.0)
        uv[7] = new Vector2(.333f, 1f);   // Mesh Bottom-Left → Texture Top-Left (Dirt at Y:1.0)
        uv[10] = new Vector2(.666f, .75f); // Mesh Top-Right → Texture Bottom-Right (Grass at Y:0.75)
        uv[11] = new Vector2(.333f, .75f); // Mesh Top-Left → Texture Bottom-Left (Grass at Y:0.75)
        
        // -------------------------------------------------------
        // Bottom Face (底) - 纹理位置：第1行中间列 (Y:0~0.25)
        // 直接映射：注意Y轴方向（Mesh Bottom → Texture Bottom）
        // -------------------------------------------------------
        uv[12] = new Vector2(.333f, 0f);   // Mesh Bottom-Left → Texture Bottom-Left (X:1/3, Y:0)
        uv[13] = new Vector2(.333f, .25f); // Mesh Top-Left → Texture Top-Left (X:1/3, Y:1/4)
        uv[14] = new Vector2(.666f, .25f); // Mesh Top-Right → Texture Top-Right (X:2/3, Y:1/4)
        uv[15] = new Vector2(.666f, 0f);   // Mesh Bottom-Right → Texture Bottom-Right (X:2/3, Y:0)

        // -------------------------------------------------------
        // Left Face (左) - 纹理位置：第4行第1列 (X:0~0.333, Y:0.5~0.75)
        // 90°顺时针旋转：使纹理右侧（草皮）对应立方体顶部
        // -------------------------------------------------------
        uv[16] = new Vector2(0f, .75f);     // Mesh Bottom-Left → Texture Bottom-Left (X:0, Y:0.75)
        uv[17] = new Vector2(.333f, .75f);  // Mesh Top-Left → Texture Top-Left (X:1/3, Y:0.75)
        uv[18] = new Vector2(.333f, .5f);   // Mesh Top-Right → Texture Top-Right (X:1/3, Y:0.5)
        uv[19] = new Vector2(0f, .5f);      // Mesh Bottom-Right → Texture Bottom-Right (X:0, Y:0.5)

        // -------------------------------------------------------
        // Right Face (右) - 纹理位置：第4行第3列 (X:0.666~1, Y:0.5~0.75)
        // 90°逆时针旋转：使纹理左侧（草皮）对应立方体顶部
        // -------------------------------------------------------
        uv[20] = new Vector2(1f, .5f);      // Mesh Bottom-Left → Texture Bottom-Left (X:1, Y:0.5)
        uv[21] = new Vector2(.666f, .5f);   // Mesh Top-Left → Texture Top-Left (X:2/3, Y:0.5)
        uv[22] = new Vector2(.666f, .75f);  // Mesh Top-Right → Texture Top-Right (X:2/3, Y:0.75)
        uv[23] = new Vector2(1f, .75f);     // Mesh Bottom-Right → Texture Bottom-Right (X:1, Y:0.75)

        mesh.uv = uv;
    }
}