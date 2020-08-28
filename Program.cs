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
            return "Tiled3";
        }

        static GeoTiff m_raster;
        static void Main(string[] args)
        {
           // m_raster = new GeoTiff("D:\\sanctuary\\rasters\\populationData\\2010_30s.tif");
           // m_raster = new GeoTiff("D:\\sanctuary\\rasters\\populationData\\2010_1.5m.tif");
          //  m_raster = new GeoTiff("D:\\sanctuary\\rasters\\ppp_2020_1km_Aggregated.tif");
            //m_raster = new GeoTiff("D:\\sanctuary\\rasters\\DEU_power-density_10m.tif");
            m_raster = new GeoTiff("D:\\sanctuary\\rasters\\gebco_2020_geotiff\\gebco_2020_n90.0_s0.0_w-180.0_e-90.0.tif");

            Console.ForegroundColor = ConsoleColor.DarkCyan;

               for (int level = 4; level <5; level++)
                {
                    EnumerateTilesAndSample(level, GetDatasetName());
                    //EnumerateTilesAndSampleSingleThreaded(level, GetDatasetName());
                }
        
               
               /*
                //TEST 
            OSMTile t = new OSMTile(8, 7, 4);
            
            var hexTile = new HexTile(t);

          //hexTile.CollectAllPixels(m_raster, GetDatasetName(), s_threadProgress, "--");
          hexTile.SampleCentersAndWrite(m_raster, GetDatasetName());

           //hexTile.WriteOnes(GetDatasetName());

            Console.Write($"Wrote 4.10.10");
               */
        }

        private static void ProcessTile(OSMTile tile)
        {
            string threadName = $"{tile.tx},{tile.ty},{tile.level}";

            lock (s_threadProgress)
            {
                s_threadProgress[threadName] = 0;
            }

            var startTime = DateTime.Now;
            int hexesProcessed = 0;
            long totalHexes = HEX_COUNTS[tile.level];
            using (var hexTile = new HexTile(tile))
            {
                string name = GetDatasetName();
                //hexesProcessed += hexTile.CollectAllPixels(m_raster, name, s_threadProgress, threadName);
                hexesProcessed += hexTile.SampleCentersAndWrite(m_raster, name);
            }

            lock (s_threadProgress)
            {
                s_threadProgress[threadName] = 1;
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
            Task[] taskArray = new Task[dim * dim];

            int i = 0;
            for (int ty = dim/4; ty < dim; ty++)
            {
                for (int tx = 0; tx < dim; tx++)
                { 
                    OSMTile t = new OSMTile(tx, ty, level);
                    taskArray[i] = Task.Factory.StartNew(() => ProcessTile(t));
                    i++;
                }
            }

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
            Console.WriteLine($"LEVEL{level} \t WROTE\t {numTiles:N} hexagons into {dim * dim} tile files");

        }
         

        private static void EnumerateTilesAndSampleSingleThreaded(int level, string name)
        {
            var startTime = DateTime.Now;
            int dim = (int)MathF.Pow(2, level);

            int i = 0;

            for (int ty = 0; ty < dim; ty++)
            {
                for (int tx = 0; tx < dim; tx++)
                {
                    OSMTile t = new OSMTile(tx, ty, level);
                    ProcessTile(t);

                    i++;
                    Console.Write($"LEVEL{level}:\t{100f * (float)i / (float)(dim * dim):f2}% \t elapsed:{(DateTime.Now - startTime).TotalSeconds:f2}s\r");
                }
            }
        }

    }
}