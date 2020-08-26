using System;
using System.Collections.Generic;
using System.IO;
using System.Text;
using h3 = H3Standard.H3;

namespace prometheus
{
    public class HexCell
    {
        public float Value;
        public ulong id;

        public HexCell(ulong id, float val)
        {
            this.Value = val;
            this.id = id;
        }
    }

    class HexTile :IDisposable
    {
        public int Tx;
        public int Ty;
        public int Level;
        public HexCell[] Hexes;

        private string outputPath(string outFolder)
        {
           return $@"D:\sanctuary\web\hex\{outFolder}\{Level}_{Tx}_{Ty}.csv";
        }


        public HexTile(OSMTile tile)
        {
            Tx = tile.tx;
            Ty = tile.ty;
            //Level = (int)(tile.level * 0.8f) + 1;
            Level = tile.level;
        }


        public void LoadHexData(string inFolder)
        {
            string inputPath = $@"D:\sanctuary\web\hex\{inFolder}\{Level}_{Tx}_{Ty}.csv";

            var dataFile = File.ReadAllLines(inputPath);

            List<HexCell> hexes = new List<HexCell>();

            foreach (var line in dataFile)
            {
                string[] fields = line.Split(",");
                string id = fields[0];

                while(id.Length < 15)
                {
                    id += "f";
                }

                ulong hId = h3.StringToH3(id);
                float valueDecoded = BitConverter.ToSingle(Convert.FromBase64String(fields[1]));

                HexCell h = new HexCell(hId, valueDecoded);
                hexes.Add(h);
            }

            this.Hexes = hexes.ToArray();
        }

        public int CollectAllPixels(GeoTiff raster, string outFolder, Dictionary<string, float> progress, string threadname)
        {
            var hexes = GeoTools.GetHexes(new OSMTile(Tx, Ty, Level), Level);
          //  Console.WriteLine($"{Level},{Tx},{Ty}");

            //lat0, lon0, lat1, lon1
            float[] extent = GeoTools.GetExtent(Level, Tx, Ty);

            int px0, py0, px1, py1;

            (px0, py0) = raster.LatLonToPixel(extent[0], extent[1]);
            (px1, py1) = raster.LatLonToPixel(extent[2], extent[3]);

            int pxMin, pyMin, pxMax, pyMax;
            pxMin = Math.Min(px0, px1);
            pxMax = Math.Max(px0, px1);
            pyMin = Math.Min(py0, py1);
            pyMax = Math.Max(py0, py1);
         //   Console.WriteLine($"\tPixel\t{pxMin},{pyMin}x{pxMax},{pyMax}\t{pxMax - pxMin}x{pyMax - pyMin}");

            int padding = 10;
            double lat, lon;
            ulong h3ID;

            Dictionary<ulong, int> counts = new Dictionary<ulong, int>();
            Dictionary<ulong, float> sums = new Dictionary<ulong, float>();
            for (int i = 0; i < hexes.Length; i++)
            {
                sums[hexes[i].id] = 0;
                counts[hexes[i].id] = 0;
            }

            int numpixels = ((pyMax + padding) - (pyMin - padding)) * ((pxMax + padding) - (pxMin - padding));
            int pp = 0;

            for (int py = pyMin - padding; py < pyMax + padding; py++)
            {
                for (int px = pxMin - padding; px < pxMax + padding; px++)
                {
                    if ( px >= 0 && py >= 0 && px < raster.Width && py < raster.Height)
                    {
                        (lat, lon) = raster.PixelToLatLon(px, py);
                        h3ID = h3.GeoToH3(lat, lon, Level);

                        if (counts.ContainsKey(h3ID))
                        {
                            counts[h3ID]++;
                            sums[h3ID] += raster.SampleLatLonCached(lat, lon);
                        }
                    }
                    pp++;

                }

                lock (progress)
                {
                    progress[threadname] = (float)pp / (float)numpixels;
                }
            }

            var lines = new List<string>();
            for (int i = 0; i < hexes.Length; i++)
            {
                ulong h = hexes[i].id;

                if (sums[h] > 0 && counts[h]> 0)
                {
                    float val = sums[h] / (float)counts[h];
                    var valueEncoded = Convert.ToBase64String(BitConverter.GetBytes(val));
                    string id = h3.H3ToString(h).TrimEnd('f');
                    lines.Add($"{id},{valueEncoded}");
                }
            }

            File.WriteAllLines(outputPath(outFolder), lines);

            return hexes.Length;
        }


        public int SampleCentersAndWrite(GeoTiff raster, string outFolder)
        {
            var hexes = GeoTools.GetHexes(new OSMTile(Tx,Ty,Level), Level);

            var lines = new List<string>();
            for (int i = 0; i < hexes.Length; i++)
            {
                ulong h = hexes[i].id;
                (double lat, double lon) = h3.GetCenter(h);
                float val = raster.SampleLatLonCached(lat, lon);

                if (val > 0)
                {
                    var valueEncoded = Convert.ToBase64String(BitConverter.GetBytes(val));
                    string id = h3.H3ToString(h).TrimEnd('f');
                    lines.Add($"{id},{valueEncoded}");
                }
            }

            File.WriteAllLines(outputPath(outFolder), lines);

            return hexes.Length;
        }

        public void SampleCenters(GeoTiff raster)
        {
            for (int i = 0; i < Hexes.Length; i ++)
            {
                ulong h = Hexes[i].id;
                (double lat, double lon) = h3.GetCenter(h);
                Hexes[i].Value = raster.SampleLatLon(lat, lon);
            }
        }

        public void WriteValues(string outFolder)
        {
            var lines = new List<string>();

            for (int i = 0; i < Hexes.Length; i++)
            {
                float value = Hexes[i].Value;
                ulong h = Hexes[i].id;

                if (value > 0)
                {
                    var valueEncoded = Convert.ToBase64String(BitConverter.GetBytes(value));
                    string id = h3.H3ToString(h).TrimEnd('f');
                    lines.Add($"{id},{valueEncoded}");
                }
            }

            File.WriteAllLines(outputPath(outFolder), lines);
        }


        public void ReconstructFromChildren(string childName)
        {
            List<HexCell> childrenWPadding = new List<HexCell>();

            int dim = (int)Math.Pow(2, Level + 1);

            for (int tx = Tx * 2 - 1; tx < Tx * 2 + 4; tx++)
            {
                for (int ty = Ty * 2 - 1; ty < Ty * 2 + 4; ty++)
                {
                    if ( tx >= 0 && tx < dim && ty >= 0 && ty < dim)
                    {
                        var child = new HexTile(new OSMTile(tx, ty, Level + 1));
                        childrenWPadding.AddRange(child.Hexes);
                    }
                }
            }

            var targetCellsSum = new Dictionary<ulong, float>();
            var targetCellsCount = new Dictionary<ulong, int>();

            foreach (var h in Hexes)
            {
                targetCellsSum[h.id] = 0f;
                targetCellsCount[h.id] = 0;
            }

            foreach(var c in childrenWPadding)
            {
                ulong parent = h3.H3ToParent(c.id);

                if (targetCellsSum.ContainsKey(parent))
                {
                    targetCellsSum[parent] += c.Value;
                    targetCellsCount[parent] += 1;
                }

            }

            var hexList = new List<HexCell>();

            foreach(var kvp in targetCellsSum)
            {
                var total = targetCellsCount[kvp.Key];

                hexList.Add(new HexCell(kvp.Key, kvp.Value / total));
            }

            this.Hexes = hexList.ToArray();
            //   h3.GetChildren()
            // LoadHexes()
        }

        #region IDisposable Support
        private bool disposedValue = false; // To detect redundant calls

        protected virtual void Dispose(bool disposing)
        {
            if (!disposedValue)
            {
                if (disposing)
                {
                    // TODO: dispose managed state (managed objects).
                    Hexes = null;
                }

                // TODO: free unmanaged resources (unmanaged objects) and override a finalizer below.
                // TODO: set large fields to null.

                disposedValue = true;
            }
        }

        // TODO: override a finalizer only if Dispose(bool disposing) above has code to free unmanaged resources.
        // ~HexTile()
        // {
        //   // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
        //   Dispose(false);
        // }

        // This code added to correctly implement the disposable pattern.
        public void Dispose()
        {
            // Do not change this code. Put cleanup code in Dispose(bool disposing) above.
            Dispose(true);
            // TODO: uncomment the following line if the finalizer is overridden above.
            // GC.SuppressFinalize(this);
        }
        #endregion
    }
}
