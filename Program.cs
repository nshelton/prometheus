using System;
using System.IO;
using h3 = H3Standard.H3;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Threading;

namespace prometheus
{

    class Program
    {
        public static long[] HEX_COUNTS = new long[]{
            122,
            842,
            5882,
            41162,
            288122,
            2016842,
            14117882,
            98825162,
            691776122,
            4842432842,
            33897029882};
        
        private static void ClearConsoleLine()
        {
            Console.Write($"                                                                  \r");
        }

        private static Dictionary<string, float> s_threadProgress = new Dictionary<string, float>();

        private static string GetDatasetName()
        {
            return "testFullMultithread";
        }

        static GeoTiff m_raster;
        static void Main(string[] args)
        {
            m_raster = new GeoTiff("D:\\sanctuary\\rasters\\populationData\\2010_30s.tif");
            //m_raster = new GeoTiff("D:\\sanctuary\\rasters\\populationData\\2010_1.5m.tif");
            //GeoTiff tiledPopulation = new GeoTiff("D:\\sanctuary\\rasters\\ppp_2020_1km_Aggregated.tif");
            // m_raster = new GeoTiff("D:\\sanctuary\\rasters\\DEU_power-density_10m.tif");

            Console.ForegroundColor = ConsoleColor.Yellow;

            for (int level = 2; level < 9; level++)
            {
                EnumerateTilesAndSample(level, GetDatasetName());
            }
        }

        private static void ProcessTile(OSMTile tile)
        {
            lock (s_threadProgress)
            {
                s_threadProgress[$"{tile.tx},{tile.ty},{tile.level}"] = 0;
            }

            var startTime = DateTime.Now;
            int hexesProcessed = 0;
            long totalHexes = HEX_COUNTS[tile.level];
            using (var hexTile = new HexTile(tile))
            {
                string name = GetDatasetName();
                string threadName = $"{tile.tx},{tile.ty},{tile.level}";
                hexesProcessed += hexTile.CollectAllPixels(m_raster, name, s_threadProgress, threadName);
            }

            lock (s_threadProgress)
            {
                s_threadProgress[$"{tile.tx},{tile.ty},{tile.level}"] = 1;
            }
        }

        private static float GetProgress(int dim)
        {
            float total = dim * dim;
            float totalProgress = 0;

            lock (s_threadProgress)
            {
                foreach (var kvp in s_threadProgress)
                {
                    totalProgress += kvp.Value;
                }
            }
            return totalProgress / total;
        }

        private static void EnumerateTilesAndSample(int level, string name)
        {
            var startTime = DateTime.Now;
            int dim = (int)MathF.Pow(2, level);
            List<Task> theTasks = new List<Task>();

            for (int ty = 0; ty < dim; ty++)
            {
                for (int tx = 0; tx < dim; tx++)
                { 
                    OSMTile t = new OSMTile(tx, ty, level);
                    theTasks.Add(Task.Factory.StartNew(() => ProcessTile(t)));
                }
            }

            var taskArray = theTasks.ToArray();
            float progress = 0;

            while (progress < 100)
            {
                Thread.Sleep(100);

                progress= GetProgress(dim) * 100;

                ClearConsoleLine();
                Console.Write($"LEVEL{level}:\t{progress:f2}% \t elapsed:{(DateTime.Now - startTime).TotalSeconds:f2}s\r");
            }
            long numTiles = HEX_COUNTS[level];
            Console.WriteLine();
            Console.WriteLine($"LEVEL{level} \t WROTE\t {numTiles} hexagons into {dim} tile files");

        }

    }
}