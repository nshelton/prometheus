using prometheus;
using System;
using System.Collections.Generic;
using System.Linq;
using h3 = H3Standard.H3;

public static class GeoTools
{
    public const int TILESIZE = 256;
    public const float INITIAL_RESOLUTION = MathF.PI * 2f * 6378137 / TILESIZE;
    public const float ORIGIN = MathF.PI * 2f * 6378137 / 2f;
    public const float DEG_TO_RAD = 180 / MathF.PI;

    public static HexCell[] GetHexes(OSMTile tile, int hexLevel)
    {
        float clat = (tile.minLat + tile.maxLat) / 2f;
        float clon = (tile.minLon + tile.maxLon) / 2f;

        ulong id = h3.GeoToH3(clat, clon, hexLevel);

        // Get radius to each corner, take max
        int maxRad = 0;
        var e0 = h3.H3Distance(id, h3.GeoToH3(tile.minLat, tile.minLon, hexLevel));
        maxRad = Math.Max(e0, maxRad);
        var e1 = h3.H3Distance(id, h3.GeoToH3(tile.minLat, tile.maxLon, hexLevel));
        maxRad = Math.Max(e1, maxRad);
        var e2 = h3.H3Distance(id, h3.GeoToH3(tile.maxLat, tile.minLon, hexLevel));
        maxRad = Math.Max(e2, maxRad);
        var e3 = h3.H3Distance(id, h3.GeoToH3(tile.maxLat, tile.maxLon, hexLevel));
        maxRad = Math.Max(e3, maxRad);


        ulong[] ring = h3.GetKRing(id, maxRad);

        if (e0 < 0 || e1 < 0 || e2 < 0 || e3 < 0)
        {
            {
                ulong idfucker = h3.GeoToH3(tile.minLat, tile.minLon, hexLevel);
                ulong[] ring2 = h3.GetKRing(idfucker, maxRad);
                ring = ring.Concat(ring2).ToArray();
            }
            {
                ulong idfucker = h3.GeoToH3(tile.minLat, tile.maxLon, hexLevel);
                ulong[] ring2 = h3.GetKRing(idfucker, maxRad);
                ring = ring.Concat(ring2).ToArray();
            }
            {
                ulong idfucker = h3.GeoToH3(tile.maxLat, tile.minLon, hexLevel);
                ulong[] ring2 = h3.GetKRing(idfucker, maxRad);
                ring = ring.Concat(ring2).ToArray();
            }
            {
                ulong idfucker = h3.GeoToH3(tile.maxLat, tile.maxLon, hexLevel);
                ulong[] ring2 = h3.GetKRing(idfucker, maxRad);
                ring = ring.Concat(ring2).ToArray();
            }
            ring = ring.Distinct().ToArray();
        }

        var subset = ring.Where(id => tile.Contains(id)).ToArray();

        List<HexCell> hexes = new List<HexCell>(subset.Length);

        for(int i = 0; i < subset.Length; i++)
        {
            hexes.Add(new HexCell(subset[i], -1f));
        }

        return hexes.ToArray();
    }
    public static float[] GetExtent(int zoom, int tx, int ty)
    {
        ty = (int)MathF.Pow(2, zoom) - 1 - ty;

        float res = INITIAL_RESOLUTION / MathF.Pow(2, zoom);
        float lon0 = (tx * TILESIZE * res - ORIGIN) / ORIGIN * 180.0f;
        float lat0 = (ty * TILESIZE * res - ORIGIN) / ORIGIN * 180.0f;
        lat0 = DEG_TO_RAD * (2 * MathF.Atan(MathF.Exp(lat0 * MathF.PI / 180)) - MathF.PI / 2f);

        float lon1 = ((tx + 1) * TILESIZE * res - ORIGIN) / ORIGIN * 180.0f;
        float lat1 = ((ty + 1) * TILESIZE * res - ORIGIN) / ORIGIN * 180.0f;
        lat1 = DEG_TO_RAD * (2 * MathF.Atan(MathF.Exp(lat1 * MathF.PI / 180)) - MathF.PI / 2f);

        return new float[] { lat0, lon0, lat1, lon1 };
    }

  
    public static GeoCoord[] GetPolygon(int zoom, int tx, int ty)
    {
        float[] c = GetExtent(zoom, tx, ty);

        return new GeoCoord[] {
            new GeoCoord(c[0], c[1]),
            new GeoCoord(c[0], c[2]),
            new GeoCoord(c[2], c[2]),
            new GeoCoord(c[2], c[1])
        };
    }
 

}
