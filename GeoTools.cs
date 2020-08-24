using System;

public static class GeoTools
{
    public const int TILESIZE = 256;
    public const float INITIAL_RESOLUTION = MathF.PI * 2f * 6378137 / TILESIZE;
    public const float ORIGIN = MathF.PI * 2f * 6378137 / 2f;
    public const float DEG_TO_RAD = 180 / MathF.PI;

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
