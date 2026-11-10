using System;
using System.Collections.Generic;
using System.Text;
using System.IO;
using System.Xml.Serialization;

namespace PowerGridSim.Models
{
    public static class XmlMapLoader
    {
        public static PowerGridBlueprint Load(string path)
        {
            var serializer = new XmlSerializer(typeof(PowerGridBlueprint));
            using var reader = new StreamReader(path);
            var blueprint = (PowerGridBlueprint?)serializer.Deserialize(reader)
                ?? throw new InvalidDataException("Map file is missing");

            var errors = blueprint.Validate();
            if (errors.Count > 0)
                throw new InvalidDataException(string.Join("\n", errors));

            return blueprint;
        }

        public static void Save(PowerGridBlueprint blueprint, string path)
        {
            Directory.CreateDirectory(Path.GetDirectoryName(path)!);
            var serializer = new XmlSerializer(typeof(PowerGridBlueprint));
            using var writer = new StreamWriter(path);
            serializer.Serialize(writer, blueprint);
            System.Diagnostics.Debug.WriteLine($"saved at {path}");
        }
    }
}
