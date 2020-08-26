using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Linq;
using System.Text;
using BitMiracle.LibTiff.Classic;

namespace prometheus
{
    enum OrgMode
    {
        Tiled,
        Striped
    }
    class GeoTiff
    {
        public int Width = 0;
        public int Height = 0;
        public OrgMode Mode = OrgMode.Striped;

        public int BytesPerSample;
        public int SamplesPerPixel;
        public string SampleFormat;
        public Tiff Image;

        public int m_tileW;
        public int m_tileH;

        private float xTranslation;
        private float yTranslation;

        private float xScale;
        private float yScale;

        public float MaxValue;

        private int TileSize = -1;

        private int MAX_CACHE_LENGTH = 4096;

        private Dictionary<int, byte[]> m_scanlineCache = new Dictionary<int, byte[]>();
        private Dictionary<int, DateTime> m_lastAccessTime = new Dictionary<int, DateTime>();

        private byte[] m_scanLineBuffer;

        public GeoTiff(string path)
        {
            Console.ForegroundColor = ConsoleColor.DarkGray;

            Image = Tiff.Open(path, "r");

            Console.ForegroundColor = ConsoleColor.Green;

            if (Image == null)
            {
                Console.WriteLine("Could Not Load " + path);
                return;
            }

            Mode = Image.IsTiled() ? OrgMode.Tiled : OrgMode.Striped;

            foreach (var tag in Enum.GetValues(typeof(TiffTag)))
            {
                TiffTag t = (TiffTag)tag;
                if (Image.GetField(t) != null)
                {
                    var fieldValue0 = Image.GetField(t).GetValue(0);
                    Console.WriteLine(tag + "\t" + fieldValue0);
                }
            }

            Console.ForegroundColor = ConsoleColor.Green;

            Height = Image.GetField(TiffTag.IMAGELENGTH)[0].ToInt();
            Width = Image.GetField(TiffTag.IMAGEWIDTH)[0].ToInt();
            SamplesPerPixel = Image.GetField(TiffTag.SAMPLESPERPIXEL)[0].ToInt();
            SampleFormat = Image.GetField(TiffTag.SAMPLEFORMAT)[0].ToString();

            int bps = Image.GetField(TiffTag.BITSPERSAMPLE)[0].ToShort();
            Debug.Assert(bps % 8 == 0);
            BytesPerSample = (short)(bps / 8);

            Console.WriteLine("Mode:\t" + Mode);
            Console.WriteLine("Width:\t" + Width);
            Console.WriteLine("Height:\t" + Height);
            Console.WriteLine("SamplesPerPixel:\t" + SamplesPerPixel);
            Console.WriteLine("BytesPerSample:\t" + BytesPerSample);
            Console.WriteLine("SampleFormat:\t" + SampleFormat);

            Console.WriteLine("\tTileSize\t" + Image.TileSize());
            Console.WriteLine("\tNumberOfTiles\t" + Image.NumberOfTiles());
            Console.WriteLine("\tNumberOfStrips\t" + Image.NumberOfStrips());
            Console.WriteLine("Mode:\t" + Mode);

            TileSize = Image.TileSize();
            
            m_scanLineBuffer = new byte[TileSize];

            if (Mode == OrgMode.Tiled)
            {
                m_tileH = Image.GetField(TiffTag.TILEWIDTH)[0].ToInt();
                m_tileW = Image.GetField(TiffTag.TILELENGTH)[0].ToInt();

                Console.WriteLine($"Tiles are {m_tileW}x{m_tileH}");
                Debug.Assert(m_tileW * m_tileH * SamplesPerPixel * BytesPerSample == TileSize);

                float val = SampleFloatTiled(10240, 10240);

            }
            else
            {
                MaxValue = 468927.88f;// FindMaxValue();
                Console.WriteLine("MaxValue: {0}", MaxValue);
            }

            // Get Transformation Matrix
            Console.Write("GEOTIFF_MODELPIXELSCALETAG\t");
            FieldValue[] fval = Image.GetField(TiffTag.GEOTIFF_MODELPIXELSCALETAG);
            if ( fval != null)
            {
                var scales = fval[1].ToDoubleArray();

                foreach(var s in scales)
                {
                    Console.Write(s + " \t");
                }

                Console.WriteLine();

                if (scales.Length < 3)
                {
                    Console.WriteLine("ERROR: Expected at least 2 pixel scale values");
                    return;
                }
                else
                {
                    xScale = (float)scales[0];
                    yScale = (float)scales[1];
                }
            }
            else
            {
                Console.Write("NotFound\n");
            }

            Console.Write("GEOTIFF_MODELTIEPOINTTAG\t");
            fval = Image.GetField(TiffTag.GEOTIFF_MODELTIEPOINTTAG);
            if (fval != null)
            {
                var tiepoint = fval[1].ToDoubleArray();

                foreach (var s in tiepoint)
                {
                    Console.Write(s + " \t");
                }

                Console.WriteLine();

                if (tiepoint.Length < 6)
                {
                    Console.WriteLine("ERROR: Expected at least 6 tie point values");
                    return;
                }
                else
                {
                    xTranslation = (float)(tiepoint[3]);
                    yTranslation = (float)(tiepoint[4]);
                }
            }
            else
            {
                Console.Write("NotFound\n");
            }

            Console.Write("GEOTIFF_MODELTRANSFORMATIONTAG\t");
            fval = Image.GetField(TiffTag.GEOTIFF_MODELTRANSFORMATIONTAG);
            if (fval != null)
            {
                var scales = fval[1].ToDoubleArray();
                for (int i = 0; i < scales.Length; i++)
                    Console.Write(scales[i] + "\t");
                Console.WriteLine();
            }
            else
            {
                Console.Write("NotFound\n");
            }

            Console.WriteLine("Scale:{0}\t{1}", xScale, yScale);
            Console.WriteLine("Offse:{0}\t{1}", xTranslation, yTranslation);

        }

        public float SampleLatLonCached(double lat, double lon)
        {
            int px;
            int py;
            (px, py) = LatLonToPixel(lat, lon);

            lock (m_scanlineCache)
            {
                if (!m_scanlineCache.ContainsKey(py))
                {
                    m_scanlineCache[py] = new byte[TileSize];
                    Image.ReadScanline(m_scanlineCache[py], py);
                    m_lastAccessTime[py] = DateTime.Now;

                    if (m_scanlineCache.Count > MAX_CACHE_LENGTH)
                    {
                        DropOldestCacheEntry();
                    }
                }
            }

            //Image.ReadScanline(m_scanLineBuffer, py);

            int offset = SamplesPerPixel * BytesPerSample * px;
            float value = BitConverter.ToSingle(m_scanlineCache[py], offset);

            // TODO chaeck GDAL_NODATA value here
            if (value < 0)
                value = 0;

            value /= MaxValue;

            return value;
        }


        public (int, int) LatLonToPixel(double lat, double lon)
        {
            int px = (int)((lon + 180) / xScale);
            if (px < 0)
                px += Width;

            if (px > Width)
                px -= Width;

            int py = Height - (int)((lat + 90) / yScale) + 1;
            if (py < 0)
                py = 0;
            if (py >= Height)
                py = Height-1;

            return (px, py);
        
        }
        public (double, double) PixelToLatLon(int px, int py)
        {
            double lon = px * xScale - 180;
            double lat = (Height - (py - 1)) * yScale - 90;
            return (lat, lon);
        }

        public float SampleLatLon(double lat, double lon)
        {

            int px;
            int py;
            (px, py) = LatLonToPixel(lat, lon);

            Image.ReadScanline(m_scanLineBuffer, py);

            int offset = SamplesPerPixel * BytesPerSample * px;
            float value = BitConverter.ToSingle(m_scanLineBuffer, offset);

            // TODO chaeck GDAL_NODATA value here
            if (value < 0)
                value = 0;

            value /= MaxValue;

            return value;
        }

        private void DropOldestCacheEntry()
        {

            double maxAge = 0;
            int oldestKey = -1;
            var now = DateTime.Now;
            foreach(var kvp in m_lastAccessTime)
            {
                double duration = (now - kvp.Value).TotalMilliseconds;
                if (duration > maxAge)
                {
                    maxAge = duration;
                    oldestKey = kvp.Key;
                }
            }
            m_lastAccessTime.Remove(oldestKey);
            m_scanlineCache.Remove(oldestKey);
        }

        private float FindMaxValue()
        {
            int scanlineWidth = Width * SamplesPerPixel * BytesPerSample;
            var byteBuffer = new byte[scanlineWidth];
            int numRows = Image.NumberOfStrips();

            Debug.Assert(Height == numRows);

            int goodSamples = 0;
            int totalSamples = 0;
            float maxValue = 0;

            for (int rowNum = 0; rowNum<numRows; rowNum++)
            {
                Image.ReadScanline(byteBuffer, rowNum);
                for (int i = 0; i<byteBuffer.Length; i += BytesPerSample)
                {
                    float f = BitConverter.ToSingle(byteBuffer, i);

                    if (f > 0)
                        goodSamples++;

                    maxValue = MathF.Max(maxValue, f);
                    totalSamples++;
                }
            }

            Console.WriteLine("good Samples" + goodSamples);
            Console.WriteLine("total Samples" + totalSamples);

            return maxValue;
        }


        private float SampleFloatTiled(int x, int y)
        {
            /*     
            int tileNum = Image.ComputeTile(x, y, 0, 0);
            Image.ReadEncodedTile(tileNum, m_byteBuffer, 0, m_byteBuffer.Length);

            int nTilesX = (int)MathF.Ceiling(Width / m_tileW);
            int nTilesY = (int)MathF.Ceiling(Height / m_tileH);

            int fullwidth = nTilesX * m_tileW;
            int fullHeight = nTilesY * m_tileH;

            int tx = (int)MathF.Floor((float)x / m_tileW);
            int ty = (int)MathF.Floor((float)y / m_tileH);

            int computed = (tx) + ty * (nTilesX );
            Console.WriteLine("lib says " + tileNum);
            Console.WriteLine("me syas " + computed);
            */

            return 0f;
        }
        private void ReadAllTiled()
        {
            /*
            int tileSize = Image.TileSize();
            int numTiles = Image.NumberOfTiles();

            Console.WriteLine("NumTiles:" + numTiles);

            int goodSamples = 0;
            int totalSamples = 0;

            for (int x = 0; x < Width; x++)
            {
                for (int y = 0; y < Height; y++)
                {

                    int tileNum = Image.ComputeTile(x, y, 0, 0);
                  //  Image.ReadEncodedTile(tileNum, m_byteBuffer, 0, tileSize);
                }
            }
            for (int i = 0; i < byteBuffer.Length; i += BytesPerSample)
            {
                float f = BitConverter.ToSingle(byteBuffer, i);
                if (f > 0)
                    goodSamples++;
                totalSamples++;
            }
            //        Console.WriteLine("good Samples" + goodSamples);
            //        Console.WriteLine("total Samples" + totalSamples);
            */

        }

        private void ParseGDALMetadata(Tiff image)
        {
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
            if (meta != null)
            {
                Console.WriteLine(Encoding.ASCII.GetString((byte[])meta[1].Value));
                meta = image.GetField(GDAL_NODATA);
                Console.WriteLine(BitConverter.ToDouble((byte[])meta[1].Value, 0));
                Console.WriteLine(BitConverter.ToSingle((byte[])meta[1].Value, 0));
            }


            Console.WriteLine("=== All tags");

            foreach (var t in tags)
            {
                Console.WriteLine("CustomTag \t" + t);

                FieldValue[] values = image.GetField(t);

                foreach (var val in values)
                {
                    var array = val.ToFloatArray();

                    if (array != null)
                    {
                        Console.WriteLine("\t ToFloatArray is:");
                        foreach (var element in array)
                        {
                            Console.WriteLine("\t\t" + element);
                        }
                    }
                    else
                        Console.WriteLine("\t ToFloatArray was null");
                    Console.WriteLine("\t " + val);

                }

            }



        }
    }
}
