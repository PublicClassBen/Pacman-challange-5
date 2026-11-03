using Godot;

// The two side pipes connect the end cells of this maze row.
public static class MazeTunnel
{
    public const int TunnelRow = 17;

    public static Vector2I WrapCell(TileMapLayer map, Vector2I cell)
    {
        Rect2I bounds = map.GetUsedRect();
        if (cell.Y != TunnelRow || bounds.Size.X <= 0)
            return cell;

        int left = bounds.Position.X;
        int right = bounds.End.X;
        if (cell.X < left)
            cell.X = right - 1;
        else if (cell.X >= right)
            cell.X = left;
        return cell;
    }

    public static Vector2 WrapPosition(TileMapLayer map, Vector2 worldPosition)
    {
        Vector2I cell = map.LocalToMap(map.ToLocal(worldPosition));
        Vector2I wrapped = WrapCell(map, cell);
        return wrapped == cell ? worldPosition : map.ToGlobal(map.MapToLocal(wrapped));
    }

    public static Vector2I DirectionToNeighbor(TileMapLayer map, Vector2I from, Vector2I to)
    {
        Vector2I direction = to - from;
        // A portal edge is one horizontal step, even though the cells are far apart.
        if (from.Y == TunnelRow && to.Y == TunnelRow && Mathf.Abs(direction.X) > 1)
            direction.X = direction.X > 0 ? -1 : 1;
        return direction;
    }
}
