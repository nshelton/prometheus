using System;
using System.Collections.Generic;
using  h3 = H3Standard.H3;
namespace prometheus
{
    static class Reconstruction
    {

        public static string GetInputPath(OSMTile tile)
        {
            return $@"D:\sanctuary\web\hex\test4\{tile.level}_{tile.tx}_{tile.ty}.csv";
        }

        public static string GetOutputPath(OSMTile tile)
        {
            return $@"D:\sanctuary\web\hex\testReconstruct\{tile.level}_{tile.tx}_{tile.ty}.csv";
        }
    }
}
