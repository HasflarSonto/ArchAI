using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class DataStructureConverter
    {
        public static GH_Structure<IGH_Goo> ConvertToGrasshopper(JObject pythonData)
        {
            var data = pythonData["data"] as JArray;
            var structure = pythonData["structure"] as JObject;

            if (data == null || structure == null)
            {
                return new GH_Structure<IGH_Goo>();
            }

            var type = structure["type"].ToString();
            var branchCount = (int)structure["branch_count"];
            var pathCounts = structure["path_count"] as JArray;

            var result = new GH_Structure<IGH_Goo>();

            switch (type)
            {
                case "tree":
                    return ConvertTreeStructure(data, pathCounts);
                case "list":
                    return ConvertListStructure(data);
                case "empty":
                    return new GH_Structure<IGH_Goo>();
                default:
                    throw new ArgumentException($"Unknown data structure type: {type}");
            }
        }

        private static GH_Structure<IGH_Goo> ConvertTreeStructure(JArray data, JArray pathCounts)
        {
            var result = new GH_Structure<IGH_Goo>();
            var currentIndex = 0;

            for (int i = 0; i < pathCounts.Count; i++)
            {
                var path = new GH_Path(i);
                var count = (int)pathCounts[i];

                for (int j = 0; j < count; j++)
                {
                    if (currentIndex < data.Count)
                    {
                        var value = ConvertValue(data[currentIndex]);
                        if (value != null)
                        {
                            result.Append(value, path);
                        }
                        currentIndex++;
                    }
                }
            }

            return result;
        }

        private static GH_Structure<IGH_Goo> ConvertListStructure(JArray data)
        {
            var result = new GH_Structure<IGH_Goo>();
            var path = new GH_Path(0);

            foreach (var item in data)
            {
                var value = ConvertValue(item);
                if (value != null)
                {
                    result.Append(value, path);
                }
            }

            return result;
        }

        private static IGH_Goo ConvertValue(JToken value)
        {
            if (value == null) return null;

            switch (value.Type)
            {
                case JTokenType.Integer:
                    return new GH_Integer((int)value);
                case JTokenType.Float:
                    return new GH_Number((double)value);
                case JTokenType.String:
                    return new GH_String(value.ToString());
                case JTokenType.Boolean:
                    return new GH_Boolean((bool)value);
                case JTokenType.Object:
                    return ConvertComplexValue(value as JObject);
                default:
                    return null;
            }
        }

        private static IGH_Goo ConvertComplexValue(JObject value)
        {
            // Handle complex types like points, vectors, etc.
            if (value["type"] != null)
            {
                var type = value["type"].ToString();
                switch (type)
                {
                    case "point":
                        var x = (double)value["x"];
                        var y = (double)value["y"];
                        var z = (double)value["z"];
                        return new GH_Point(new Rhino.Geometry.Point3d(x, y, z));
                    case "vector":
                        var vx = (double)value["x"];
                        var vy = (double)value["y"];
                        var vz = (double)value["z"];
                        return new GH_Vector(new Rhino.Geometry.Vector3d(vx, vy, vz));
                    // Add more complex type conversions as needed
                    default:
                        return null;
                }
            }

            return null;
        }

        public static JObject ConvertToPython(GH_Structure<IGH_Goo> data)
        {
            var result = new JObject
            {
                ["data"] = new JArray(),
                ["structure"] = new JObject()
            };

            if (data == null || data.DataCount == 0)
            {
                result["structure"]["type"] = "empty";
                result["structure"]["branch_count"] = 0;
                result["structure"]["path_count"] = new JArray();
                return result;
            }

            if (data.PathCount > 1)
            {
                // Tree structure
                result["structure"]["type"] = "tree";
                result["structure"]["branch_count"] = data.PathCount;
                var pathCounts = new JArray();
                var allData = new JArray();

                foreach (var path in data.Paths)
                {
                    var branch = data.get_Branch(path);
                    pathCounts.Add(branch.Count);

                    foreach (var item in branch)
                    {
                        if (item is IGH_Goo goo)
                            allData.Add(ConvertToPythonValue(goo));
                    }
                }

                result["structure"]["path_count"] = pathCounts;
                result["data"] = allData;
            }
            else
            {
                // List structure
                result["structure"]["type"] = "list";
                result["structure"]["branch_count"] = 1;
                result["structure"]["path_count"] = new JArray(data.DataCount);
                var allData = new JArray();

                foreach (var item in data.AllData(true))
                {
                    if (item is IGH_Goo goo)
                        allData.Add(ConvertToPythonValue(goo));
                }

                result["data"] = allData;
            }

            return result;
        }

        private static JToken ConvertToPythonValue(IGH_Goo value)
        {
            if (value == null) return null;

            if (value is GH_Integer intValue)
            {
                return intValue.Value;
            }
            else if (value is GH_Number numberValue)
            {
                return numberValue.Value;
            }
            else if (value is GH_String stringValue)
            {
                return stringValue.Value;
            }
            else if (value is GH_Boolean boolValue)
            {
                return boolValue.Value;
            }
            else if (value is GH_Point pointValue)
            {
                var point = pointValue.Value;
                return new JObject
                {
                    ["type"] = "point",
                    ["x"] = point.X,
                    ["y"] = point.Y,
                    ["z"] = point.Z
                };
            }
            else if (value is GH_Vector vectorValue)
            {
                var vector = vectorValue.Value;
                return new JObject
                {
                    ["type"] = "vector",
                    ["x"] = vector.X,
                    ["y"] = vector.Y,
                    ["z"] = vector.Z
                };
            }
            // Add more type conversions as needed

            return value.ToString();
        }
    }
} 