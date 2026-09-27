using System.Collections.Generic;
using UnityEngine;
using UnityEngine.Tilemaps;

// The shape is authored once; only its existing border sprite changes with neighbours.
public sealed class PlatformBorderTile : TileBase
{
    public Tile[] variants = new Tile[32];
    public Sprite shape;
    public bool square;

    private static readonly Vector2[] Directions = { Vector2.up, Vector2.right, Vector2.down, Vector2.left };
    private static readonly Dictionary<Sprite, Vector2[][]> Polygons = new Dictionary<Sprite, Vector2[][]>();

    public static void ClearGeometryCache() => Polygons.Clear();

    public override void GetTileData(Vector3Int position, ITilemap tilemap, ref TileData data)
    {
        var map = tilemap.GetComponent<Tilemap>();
        int partial;
        int mask = map == null ? 31 : ResolveMask(map, position, out partial);
        var tile = variants[mask];
        if (tile == null) return;
        data.sprite = tile.sprite;
        data.color = Color.white;
        data.transform = square ? tile.transform : Matrix4x4.identity;
        data.flags = square ? TileFlags.LockTransform : TileFlags.None;
        data.colliderType = Tile.ColliderType.Sprite;
    }

    public override void RefreshTile(Vector3Int position, ITilemap tilemap)
    {
        for (int y = -1; y <= 1; y++)
            for (int x = -1; x <= 1; x++)
                tilemap.RefreshTile(position + new Vector3Int(x, y, 0));
    }

    public int ResolveMask(Tilemap map, Vector3Int position, out int partialEdges)
    {
        int mask = 31;
        partialEdges = 0;
        var matrix = CellMatrix(map, position, square);
        var maps = ConnectedMaps(map);
        for (int direction = 0; direction < 4; direction++)
        {
            var normal = Directions[direction];
            var tangent = new Vector2(-normal.y, normal.x);
            int own = 0, covered = 0;
            for (int sample = 0; sample < 32; sample++)
            {
                var edge = normal * .5f + tangent * ((sample + .5f) / 32f - .5f);
                if (!Contains(shape, edge - normal * .002f)) continue;
                own++;
                Vector3 outside = matrix.MultiplyPoint3x4(edge + normal * .002f);
                if (Occupied(maps, map, position, outside)) covered++;
            }
            if (own > 0 && covered == own) mask &= ~(1 << direction);
            else if (covered > 0) partialEdges++;
        }
        return mask;
    }

    public static Tilemap[] ConnectedMaps(Tilemap map)
    {
        var grid = map.layoutGrid;
        if (grid == null || map.GetComponent<TilemapCollider2D>() == null) return new[] { map };
        var result = new List<Tilemap>();
        foreach (var other in grid.GetComponentsInChildren<Tilemap>())
        {
            var collider = other.GetComponent<TilemapCollider2D>();
            if (other == map || (collider != null && collider.enabled && other.gameObject.activeInHierarchy))
                result.Add(other);
        }
        return result.ToArray();
    }

    private static bool Occupied(Tilemap[] maps, Tilemap owner, Vector3Int ownerCell, Vector3 point)
    {
        foreach (var map in maps)
        {
            var cell = map.WorldToCell(point);
            if (map == owner && cell == ownerCell) continue;
            var tile = map.GetTile(cell);
            var automatic = tile as PlatformBorderTile;
            var normal = tile as Tile;
            var sprite = automatic != null ? automatic.shape : normal != null ? normal.sprite : null;
            if (sprite == null) continue;
            var local = CellMatrix(map, cell, automatic != null && automatic.square).inverse.MultiplyPoint3x4(point);
            if (Contains(sprite, local)) return true;
        }
        return false;
    }

    private static Matrix4x4 CellMatrix(Tilemap map, Vector3Int cell, bool ignoreRotation)
    {
        return map.transform.localToWorldMatrix
            * Matrix4x4.Translate(map.CellToLocalInterpolated((Vector3)cell + map.tileAnchor))
            * (ignoreRotation ? Matrix4x4.identity : map.GetTransformMatrix(cell));
    }

    public static bool Contains(Sprite sprite, Vector2 point)
    {
        if (sprite == null) return false;
        Vector2[][] paths;
        if (!Polygons.TryGetValue(sprite, out paths))
        {
            paths = new Vector2[sprite.GetPhysicsShapeCount()][];
            var vertices = new List<Vector2>();
            for (int i = 0; i < paths.Length; i++)
            {
                sprite.GetPhysicsShape(i, vertices);
                paths[i] = vertices.ToArray();
            }
            Polygons[sprite] = paths;
        }
        foreach (var path in paths)
        {
            bool inside = false;
            for (int i = 0, j = path.Length - 1; i < path.Length; j = i++)
            {
                var a = path[i]; var b = path[j];
                if ((a.y > point.y) != (b.y > point.y)
                    && point.x < (b.x - a.x) * (point.y - a.y) / (b.y - a.y) + a.x)
                    inside = !inside;
            }
            if (inside) return true;
        }
        return false;
    }
}
