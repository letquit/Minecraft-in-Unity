using UnityEngine;

/// <summary>
/// 物品工具类，提供物品查找、丢弃位置计算等通用功能
/// </summary>
public static class ItemUtils
{
    /// <summary>
    /// 根据物品名称获取物品数据
    /// </summary>
    /// <param name="itemName">要查找的物品名称</param>
    /// <returns>找到的物品数据，如果未找到则返回null</returns>
    public static Item GetItemDataByName(string itemName)
    {
        if (string.IsNullOrEmpty(itemName))
        {
            return null;
        }

        Item[] allItems = Resources.LoadAll<Item>("Items");

        foreach (Item item in allItems)
        {
            if (item.name == itemName ||
                item.name.Equals(itemName, System.StringComparison.OrdinalIgnoreCase) ||
                item.name.Replace(" ", "") == itemName.Replace(" ", ""))
            {
                return item;
            }
        }

        return null;
    }

    /// <summary>
    /// 计算丢弃物品的目标位置，处理墙壁碰撞
    /// </summary>
    /// <param name="player">玩家对象</param>
    /// <param name="throwDistance">丢弃距离</param>
    /// <returns>计算后的目标位置</returns>
    public static Vector3 CalculateThrowTargetPosition(Player player, float throwDistance)
    {
        Vector3 playerPos = player.transform.position;
        Vector3 playerForward = player.transform.forward;
        LayerMask groundLayer = player.groundLayer;

        Vector3 primaryTarget = playerPos + playerForward * throwDistance;
        primaryTarget.y = playerPos.y + 0.5f;

        if (TryGetValidDropPosition(playerPos, playerForward, throwDistance, groundLayer, out Vector3 validPos))
        {
            return validPos;
        }

        Vector3 playerRight = player.transform.right;
        Vector3 playerLeft = -player.transform.right;

        if (TryGetValidDropPosition(playerPos, playerRight, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        if (TryGetValidDropPosition(playerPos, playerLeft, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        if (TryGetValidDropPosition(playerPos, -playerForward, throwDistance, groundLayer, out validPos))
        {
            return validPos;
        }

        return playerPos + Vector3.up * 0.5f;
    }

    /// <summary>
    /// 尝试获取有效的掉落位置
    /// </summary>
    /// <param name="startPos">起始位置</param>
    /// <param name="direction">方向</param>
    /// <param name="distance">距离</param>
    /// <param name="groundLayer">地面层</param>
    /// <param name="validPosition">输出的有效位置</param>
    /// <returns>是否找到有效位置</returns>
    private static bool TryGetValidDropPosition(Vector3 startPos, Vector3 direction, float distance,
        LayerMask groundLayer,
        out Vector3 validPosition)
    {
        Vector3 rayStart = startPos + Vector3.up * 1f;
        Vector3 targetPos = startPos + direction * distance;
        targetPos.y = startPos.y + 0.5f;

        if (Physics.Raycast(rayStart, direction, out RaycastHit wallHit, distance, groundLayer))
        {
            float wallTopY = GetWallTopY(wallHit.point, groundLayer);
            float heightDifference = wallTopY - startPos.y;

            if (heightDifference <= 1.5f)
            {
                validPosition = new Vector3(wallHit.point.x, wallTopY + 0.5f, wallHit.point.z);
                validPosition += direction * 0.3f;
                return true;
            }

            if (heightDifference > 2f)
            {
                validPosition = Vector3.zero;
                return false;
            }
        }

        validPosition = targetPos;
        return true;
    }

    /// <summary>
    /// 获取墙壁的顶部Y坐标
    /// </summary>
    private static float GetWallTopY(Vector3 wallPoint, LayerMask groundLayer)
    {
        Vector3 rayStart = wallPoint + Vector3.up * 10f;

        if (Physics.Raycast(rayStart, Vector3.down, out RaycastHit hit, 15f, groundLayer))
        {
            return hit.point.y;
        }

        return wallPoint.y;
    }
}