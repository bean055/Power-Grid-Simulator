using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PowerGridSim.Models
{
    public static class MapPaths
    {
        public static string DefaultMap =>
            Path.Combine(AppContext.BaseDirectory, "Data", "Map.xml");

        public static string UserMap => Path.Combine(
            Environment.GetFolderPath(Environment.SpecialFolder.LocalApplicationData),
            "PowerGridSim", "Map.xml");

        public static string LoadPath => File.Exists(UserMap) ? UserMap : DefaultMap;
    }
}
