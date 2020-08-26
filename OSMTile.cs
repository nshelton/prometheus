using System;
using h3 = H3Standard.H3;

public class OSMTile
{
    public int tx;
    public int ty;
    public int level;
    //public List<H3Hex> hexes = new List<H3Hex>();

    public float minLat;
    public float minLon;
    public float maxLat;
    public float maxLon;
    public OSMTile(int tx, int ty, int level)
    {
        this.tx = tx;
        this.ty = ty;
        this.level = level;

        float[] extent = GeoTools.GetExtent(level, tx, ty);
        this.minLat = MathF.Min(extent[0], extent[2]);
        this.maxLat = MathF.Max(extent[0], extent[2]);

        this.minLon = MathF.Min(extent[1], extent[3]);
        this.maxLon = MathF.Max(extent[1], extent[3]);
    }

/*      public bool Contains(H3Hex hex)
    {
        return
            hex.center.latitude > this.minLat &&
            hex.center.latitude < this.maxLat &&
            hex.center.longitude < this.maxLon &&
            hex.center.longitude > this.minLon;
    }
    */

    public bool Contains(ulong hex)
    {
        (double latitude, double longitude) = h3.GetCenter(hex);

        return
            latitude > this.minLat &&
            latitude < this.maxLat &&
            longitude < this.maxLon &&
            longitude > this.minLon;
    }
}
