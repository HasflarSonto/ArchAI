using System;
using System.Collections.Generic;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Newtonsoft.Json.Linq;

namespace RhinoMCP.Functions.Grasshopper.Conversion
{
    public class DataStructureAnalyzer
    {
        public static JObject AnalyzeDataStructure(IGH_DataAccess data)
        {
            // IGH_DataAccess does not provide direct structure info, so just return empty for now
            return new JObject
            {
                ["type"] = "empty",
                ["structure"] = new JObject
                {
                    ["branch_count"] = 0,
                    ["path_count"] = new JArray()
                }
            };
        }

        private static JObject AnalyzeTreeStructure(GH_Structure<IGH_Goo> structure)
        {
            var branches = structure.Branches;
            var pathCounts = new JArray();

            foreach (var branch in branches)
            {
                pathCounts.Add(branch.Count);
            }

            return new JObject
            {
                ["type"] = "tree",
                ["structure"] = new JObject
                {
                    ["branch_count"] = branches.Count,
                    ["path_count"] = pathCounts
                }
            };
        }

        private static JObject AnalyzeListStructure(IGH_Structure structure)
        {
            return new JObject
            {
                ["type"] = "list",
                ["structure"] = new JObject
                {
                    ["branch_count"] = 1,
                    ["path_count"] = new JArray(structure.DataCount)
                }
            };
        }

        public static object TransformData(object data, string[] operations)
        {
            var result = data;

            foreach (var operation in operations)
            {
                result = ApplyOperation(result, operation);
            }

            return result;
        }

        private static object ApplyOperation(object data, string operation)
        {
            switch (operation.ToLower())
            {
                case "graft":
                    return Graft(data);
                case "flatten":
                    return Flatten(data);
                case "simplify":
                    return Simplify(data);
                case "reverse":
                    return Reverse(data);
                default:
                    return data;
            }
        }

        private static object Graft(object data)
        {
            if (data is IList<object> list)
            {
                var result = new List<object>();
                foreach (var item in list)
                {
                    if (item is IList<object> subList)
                    {
                        result.AddRange(subList);
                    }
                    else
                    {
                        result.Add(item);
                    }
                }
                return result;
            }
            return data;
        }

        private static object Flatten(object data)
        {
            if (data is IList<object> list)
            {
                var result = new List<object>();
                FlattenRecursive(list, result);
                return result;
            }
            return data;
        }

        private static void FlattenRecursive(IList<object> list, List<object> result)
        {
            foreach (var item in list)
            {
                if (item is IList<object> subList)
                {
                    FlattenRecursive(subList, result);
                }
                else
                {
                    result.Add(item);
                }
            }
        }

        private static object Simplify(object data)
        {
            if (data is IList<object> list && list.Count == 1)
            {
                return list[0];
            }
            return data;
        }

        private static object Reverse(object data)
        {
            if (data is IList<object> list)
            {
                var result = new List<object>(list);
                result.Reverse();
                return result;
            }
            return data;
        }
    }
} 