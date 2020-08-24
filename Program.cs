using System;
using System.IO;
using h3 =  H3Standard.H3;
using System.Collections.Generic;
using System.Linq;

using System.Drawing;
using BitMiracle.LibTiff.Classic;
using System.Linq.Expressions;
using System.Text;
using System.Diagnostics;

namespace prometheus
{

    class Program
    {

        static Random rand = new Random();
        static void Main(string[] args)
        {

            string path = "D:\\sanctuary\\rasters\\populationData\\2010_1.5m.tif";
         //  string path = "D:\\sanctuary\\rasters\\populationData\\2010_30s.tif";

           /// string path = "D:\\sanctuary\\rasters\\N - deposition1860.tif";



            RasterData raster = new RasterData(path);

            return;

            for(int level = 1; level < 16; level++)
            {
                int hexlLevel = (int)(level * 0.7f) + 1;

                //int hexlLevel = level;

                int dim = (int)MathF.Pow(2, level);

                int minHexes = (int)1e6;
                int maxHexes = 0;

                var startTime = DateTime.Now;

                for (int ty = 0; ty < dim; ty++)
                {
                    for (int tx = 0; tx < dim; tx++)
                    {
                        var tile = new OSMTile(tx, ty, level);
                        var hexes = GetHexes(tile, hexlLevel);
                        ProcessTile(tile, hexes);
                        minHexes = Math.Min(minHexes, hexes.Length);
                        maxHexes = Math.Max(maxHexes, hexes.Length);
                    }
                }
                var length = DateTime.Now - startTime;

                Console.WriteLine($"LEVEL {level}\t{hexlLevel}\t{minHexes}\t{maxHexes}\t{length.TotalSeconds}");

            }
        }

        private static void ProcessTile(OSMTile tile, ulong[] hexes)
        {

            var outputPath = $@"D:\sanctuary\web\hex\test3\{tile.level}_{tile.tx}_{tile.ty}.csv";

            var lines = new List<string>();
            float value = (float)rand.NextDouble();

            value = value - MathF.Floor(value);


            var valueEncoded = Convert.ToBase64String(BitConverter.GetBytes(value));

            foreach (ulong h in hexes)
            {
                string id = h3.H3ToString(h).TrimEnd('f') ;
                lines.Add($"{id},{valueEncoded}");
            }

            File.WriteAllLines(outputPath, lines);

        }

        private static ulong[] GetHexes(OSMTile tile, int hexLevel)
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

            if ( e0 < 0 || e1 < 0 || e2 < 0 || e3 < 0)
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
            return subset;
        }

        private static List<OSMTile> EnumerateTiles(int level)
        {
            int dim =  (int)MathF.Pow(2, level);
            List<OSMTile> tiles = new List<OSMTile>() ;

            for(int tx = 0; tx < dim; tx++)
            {
                for (int ty = 0; ty < dim; ty++)
                {
                    OSMTile t = new OSMTile(tx, ty, level);
                    tiles.Add(t);
                     

                }
            }

            return tiles;
        }

        private static List<H3Hex> EnumerateHexes(int level)
        {
            string fileName = @"D:\sanctuary\pythonTools\" + level.ToString() + ".hexbin";
            List<H3Hex> hexes = new List<H3Hex>();

            using (BinaryReader reader = new BinaryReader(File.Open(fileName, FileMode.Open)))
            {
                int i = 0;

                while (true)
                {
                    try
                    {
                        var hexID = reader.ReadInt64();
                        var lon = reader.ReadSingle();
                        var lat = reader.ReadSingle();

                        H3Hex h = new H3Hex(hexID, new GeoCoord(lat, lon));
                        hexes.Add(h);



                    }
                    catch
                    {
                        break;
                    }

                }
            }
            return hexes;
        }
    }



    class RasterData
    {

        private static Color getSample(int x, int y, int[] raster, int width, int height)
        {
            int index = (height - y - 1) * width + x;
        //    int red = Tiff.GetR(raster[index]);
       //     int green = Tiff.GetG(raster[index]);
       //     int blue = Tiff.GetB(raster[index]);
            return Color.FromArgb(0, 0, 0);
        }

        public RasterData(string path)
        {

            using (Tiff image = Tiff.Open(path, "r"))
            {
                FieldValue[] value = image.GetField(TiffTag.IMAGEWIDTH);
                int width = value[0].ToInt();

                value = image.GetField(TiffTag.IMAGELENGTH);
                int height = value[0].ToInt();

                Console.WriteLine($"Width = {width}, Height = {height}");

                foreach(var tag in Enum.GetValues(typeof(TiffTag)))
                {
                    TiffTag t = (TiffTag)tag;
                    if (image.GetField(t) != null)
                    {
                        var fieldValue0 = image.GetField(t).GetValue(0);
                        Console.WriteLine(tag + "\t" + fieldValue0);
                    }
                }

                Console.WriteLine("CUSTOM TAGS: ");

                TiffTag customTag0 = (TiffTag)33550; //ModelPixelScaleTag
                TiffTag customTag1 = (TiffTag)33922; //ModelTiepointTag
                TiffTag customTag2 = (TiffTag)34735; //GeoKeyDirectoryTag
                TiffTag customTag3 = (TiffTag)34736; // GeoDoubleParamsTag
                TiffTag customTag4 = (TiffTag)34737; //	GeoAsciiParamsTag
                TiffTag GDAL_METADATA = (TiffTag)42112; //GDAL_METADATA
                TiffTag GDAL_NODATA = (TiffTag)42113; // GDAL_NODATA

                var tags = new TiffTag[]
                {
                    customTag0,customTag1, customTag2, customTag3, customTag4, GDAL_METADATA, GDAL_NODATA
                };

                Console.WriteLine("=== GDAL_METADATA");

                FieldValue[] meta = image.GetField(GDAL_METADATA);
                Console.WriteLine(Encoding.ASCII.GetString((byte[])meta[1].Value));

                meta = image.GetField(GDAL_NODATA);
                Console.WriteLine(BitConverter.ToDouble((byte[])meta[1].Value, 0));
                Console.WriteLine(BitConverter.ToSingle((byte[])meta[1].Value, 0));

                Console.WriteLine("=== All tags");

                foreach (var t in tags)
                {
                    Console.WriteLine("CustomTag \t" + t);

                    FieldValue[] values = image.GetField(t);

                    foreach(var val in values)
                    {
                        var array = val.ToFloatArray();

                        if (array != null)
                        {
                            Console.WriteLine("\t ToFloatArray is:");
                            foreach(var element in array)
                            {
                                Console.WriteLine("\t\t" + element);
                            }
                        }
                        else
                            Console.WriteLine("\t ToFloatArray was null");
                            Console.WriteLine("\t " + val);

                    }

                }



                Console.ForegroundColor = ConsoleColor.Green;

                byte[] tilebuf = new byte[image.TileSize()];

                FieldValue[] result;

                result = image.GetField(TiffTag.IMAGELENGTH);
                int imagelength = result[0].ToInt();

                result = image.GetField(TiffTag.IMAGEWIDTH);
                int imagewidth = result[0].ToInt();

                result = image.GetField(TiffTag.BITSPERSAMPLE);
                short bps = result[0].ToShort();
                Debug.Assert(bps % 8 == 0);

                short bytes_per_sample = (short)(bps / 8);
                Console.WriteLine(bytes_per_sample + "bytesPerSample");
                int imagew = image.RasterScanlineSize();
                int tilew = image.TileRowSize();

                int spp = 1;
                 




                var byteBuffer = new byte[tilew];
                int numRows = image.NumberOfStrips();
                int goodSamples = 0;
                int totalSamples = 0;

                for (int rowNum = 0; rowNum < numRows; rowNum++)
                {
                    image.ReadScanline(byteBuffer, rowNum);
                    for (int i = 0; i < byteBuffer.Length; i += bytes_per_sample)
                    {
                        float f = BitConverter.ToSingle(byteBuffer, i);

                        if (f > 0)
                            goodSamples++;
                        totalSamples++;
                    }
                }

                Console.WriteLine("good Samples" + goodSamples);
                Console.WriteLine("total Samples" + totalSamples);
              

            }
        }
    }

    internal class OSMTile 
    {
        public int tx;
        public int ty;
        public int level;
        public List<H3Hex> hexes = new List<H3Hex>();

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

        public bool Contains(H3Hex hex) 
        {
            return
                hex.center.latitude > this.minLat &&
                hex.center.latitude < this.maxLat &&
                hex.center.longitude < this.maxLon &&
                hex.center.longitude > this.minLon;
        }

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

    internal class H3Hex
    {
        public long id;
        public GeoCoord center;
        public OSMTile tile;

        public H3Hex(long hexID, GeoCoord geoCoord)
        {
            this.id = hexID;
            this.center = geoCoord;
        }
    }
}