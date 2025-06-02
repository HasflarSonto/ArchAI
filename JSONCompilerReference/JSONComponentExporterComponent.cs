using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Parameters;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GHUI
{
    public class JSONComponentExporterComponent : GH_Component
    {
        public JSONComponentExporterComponent()
            : base("Export to JSON", "ExportJSON",
                "Exports the current Grasshopper canvas to a JSON file",
                "UI", "Support")
        {
        }

        public override Guid ComponentGuid => new Guid("12345678-1234-1234-1234-123456789014");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Save Path", "path", "Optional path where to save the JSON file (e.g., C:\\path\\to\\file.json). If empty, saves as context.json in the same directory.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Refresh", "R", "Set to true to refresh and export", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "status", "Status of the export operation", GH_ParamAccess.item);
        }

        private JObject CreateComponentObject(IGH_DocumentObject obj)
        {
            var componentObj = new JObject();
            componentObj["id"] = obj.InstanceGuid.GetHashCode();
            componentObj["name"] = obj.Name;
            componentObj["type"] = obj.GetType().Name;
            componentObj["position"] = new JArray(obj.Attributes.Pivot.X, obj.Attributes.Pivot.Y);

            // Handle different component types
            if (obj is IGH_Component component)
            {
                var inputs = new JArray();
                componentObj["inputs"] = inputs;

                foreach (var param in component.Params.Input)
                {
                    var input = new JObject();
                    input["name"] = param.Name;

                    if (param.Sources.Count > 0)
                    {
                        var source = param.Sources[0];
                        var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                        if (sourceComponent != null)
                        {
                            input["source"] = new JObject
                            {
                                ["component"] = sourceComponent.Name,
                                ["output"] = source.Name
                            };
                        }
                    }
                    else if (param.VolatileData.DataCount > 0)
                    {
                        var data = param.VolatileData.AllData(true).FirstOrDefault();
                        if (data != null)
                        {
                            input["value"] = data.ToString();
                        }
                    }

                    inputs.Add(input);
                }
            }
            else if (obj is IGH_Param param)
            {
                // Handle parameters like number sliders
                if (param is Param_Point pointParam)
                {
                    // Get all points from the parameter
                    var points = new JArray();
                    foreach (var data in pointParam.VolatileData.AllData(true))
                    {
                        if (data is GH_Point ghPoint)
                        {
                            var point = ghPoint.Value;
                            points.Add(new JObject
                            {
                                ["x"] = point.X,
                                ["y"] = point.Y,
                                ["z"] = point.Z
                            });
                        }
                    }
                    componentObj["value"] = points;
                }
                else
                {
                    componentObj["value"] = param.VolatileData.AllData(true).FirstOrDefault()?.ToString();
                }
            }

            return componentObj;
        }

        private JObject CreateGroupObject(GH_Group group)
        {
            var groupObj = new JObject();
            groupObj["id"] = group.InstanceGuid.GetHashCode();
            groupObj["name"] = group.Name;
            groupObj["type"] = "Group";
            
            // Get group bounds through Attributes
            var bounds = group.Attributes.Bounds;
            groupObj["bounds"] = new JArray(
                bounds.X,
                bounds.Y,
                bounds.Width,
                bounds.Height
            );

            // Get group color if available
            if (group.Colour != null)
            {
                groupObj["color"] = new JArray(
                    group.Colour.R,
                    group.Colour.G,
                    group.Colour.B,
                    group.Colour.A
                );
            }

            return groupObj;
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string savePath = string.Empty;
            bool refresh = false;
            if (!DA.GetData(0, ref savePath)) return;
            if (!DA.GetData(1, ref refresh)) return;

            try
            {
                // If no path is provided, use context.json in the same directory
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = "context.json";
                }
                else if (Directory.Exists(savePath))
                {
                    // If the path is a directory, append context.json
                    savePath = Path.Combine(savePath, "context.json");
                }
                else if (!savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    // If it's not a directory and doesn't end with .json, append context.json
                    savePath = Path.Combine(Path.GetDirectoryName(savePath), "context.json");
                }

                // Get the directory path and ensure it exists
                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var doc = OnPingDocument();
                if (doc == null)
                {
                    DA.SetData(0, "Error: No active document");
                    return;
                }

                var root = new JObject();
                var groups = new JArray();
                root["groups"] = groups;

                // Get all groups and their components
                var allGroups = doc.Objects.OfType<GH_Group>().ToList();
                var ungroupedComponents = new List<IGH_DocumentObject>();

                // Create a group for ungrouped components
                var ungroupedGroup = new JObject();
                ungroupedGroup["id"] = "ungrouped";
                ungroupedGroup["label"] = "Ungrouped Components";
                var ungroupedComponentsArray = new JArray();
                ungroupedGroup["components"] = ungroupedComponentsArray;
                var ungroupedConnections = new JArray();
                ungroupedGroup["connections"] = ungroupedConnections;

                // Create a dictionary to store group hierarchy
                var groupHierarchy = new Dictionary<Guid, List<GH_Group>>();
                var topLevelGroups = new List<GH_Group>();

                // Build group hierarchy
                foreach (var group in allGroups)
                {
                    var parentGroup = group.Attributes.GetTopLevel as GH_Group;
                    if (parentGroup != null && parentGroup != group)
                    {
                        if (!groupHierarchy.ContainsKey(parentGroup.InstanceGuid))
                        {
                            groupHierarchy[parentGroup.InstanceGuid] = new List<GH_Group>();
                        }
                        groupHierarchy[parentGroup.InstanceGuid].Add(group);
                    }
                    else
                    {
                        topLevelGroups.Add(group);
                    }
                }

                // Process each top-level group and its nested groups
                foreach (var group in topLevelGroups)
                {
                    var groupObj = CreateGroupObject(group);
                    var components = new JArray();
                    groupObj["components"] = components;
                    var connections = new JArray();
                    groupObj["connections"] = connections;
                    var nestedGroups = new JArray();
                    groupObj["nestedGroups"] = nestedGroups;

                    // Get components in this group
                    var groupComponents = doc.Objects.Where(obj => 
                        obj.Attributes.GetTopLevel is GH_Group g && g.InstanceGuid == group.InstanceGuid).ToList();

                    // Add components to the group
                    foreach (var obj in groupComponents)
                    {
                        if (obj is IGH_Component || obj is IGH_Param)
                        {
                            components.Add(CreateComponentObject(obj));
                        }
                    }

                    // Add connections between components in this group
                    foreach (var obj in groupComponents)
                    {
                        if (obj is IGH_Component component)
                        {
                            foreach (var param in component.Params.Input)
                            {
                                foreach (var source in param.Sources)
                                {
                                    var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                                    if (sourceComponent != null && groupComponents.Contains(sourceComponent))
                                    {
                                        var connection = new JObject();
                                        connection["fromComponentId"] = sourceComponent.InstanceGuid.GetHashCode();
                                        connection["toComponentId"] = component.InstanceGuid.GetHashCode();
                                        connection["fromParameter"] = source.Name;
                                        connection["toParameter"] = param.Name;
                                        connections.Add(connection);
                                    }
                                }
                            }
                        }
                    }

                    // Process nested groups
                    if (groupHierarchy.ContainsKey(group.InstanceGuid))
                    {
                        foreach (var nestedGroup in groupHierarchy[group.InstanceGuid])
                        {
                            var nestedGroupObj = CreateGroupObject(nestedGroup);
                            var nestedComponents = new JArray();
                            nestedGroupObj["components"] = nestedComponents;
                            var nestedConnections = new JArray();
                            nestedGroupObj["connections"] = nestedConnections;

                            // Get components in nested group
                            var nestedGroupComponents = doc.Objects.Where(obj =>
                                obj.Attributes.GetTopLevel is GH_Group g && g.InstanceGuid == nestedGroup.InstanceGuid).ToList();

                            // Add components to nested group
                            foreach (var obj in nestedGroupComponents)
                            {
                                if (obj is IGH_Component || obj is IGH_Param)
                                {
                                    nestedComponents.Add(CreateComponentObject(obj));
                                }
                            }

                            // Add connections between components in nested group
                            foreach (var obj in nestedGroupComponents)
                            {
                                if (obj is IGH_Component component)
                                {
                                    foreach (var param in component.Params.Input)
                                    {
                                        foreach (var source in param.Sources)
                                        {
                                            var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                                            if (sourceComponent != null && nestedGroupComponents.Contains(sourceComponent))
                                            {
                                                var connection = new JObject();
                                                connection["fromComponentId"] = sourceComponent.InstanceGuid.GetHashCode();
                                                connection["toComponentId"] = component.InstanceGuid.GetHashCode();
                                                connection["fromParameter"] = source.Name;
                                                connection["toParameter"] = param.Name;
                                                nestedConnections.Add(connection);
                                            }
                                        }
                                    }
                                }
                            }

                            nestedGroups.Add(nestedGroupObj);
                        }
                    }

                    groups.Add(groupObj);
                }

                // Process ungrouped components
                foreach (var obj in doc.Objects)
                {
                    if ((obj is IGH_Component || obj is IGH_Param) && 
                        !allGroups.Any(g => g.InstanceGuid == obj.Attributes.GetTopLevel.InstanceGuid))
                    {
                        ungroupedComponents.Add(obj);
                        ungroupedComponentsArray.Add(CreateComponentObject(obj));
                    }
                }

                // Add connections between ungrouped components
                foreach (var obj in ungroupedComponents)
                {
                    if (obj is IGH_Component component)
                    {
                        foreach (var param in component.Params.Input)
                        {
                            foreach (var source in param.Sources)
                            {
                                var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                                if (sourceComponent != null && ungroupedComponents.Contains(sourceComponent))
                                {
                                    var connection = new JObject();
                                    connection["fromComponentId"] = sourceComponent.InstanceGuid.GetHashCode();
                                    connection["toComponentId"] = component.InstanceGuid.GetHashCode();
                                    connection["fromParameter"] = source.Name;
                                    connection["toParameter"] = param.Name;
                                    ungroupedConnections.Add(connection);
                                }
                            }
                        }
                    }
                }

                if (ungroupedComponentsArray.Count > 0)
                {
                    groups.Add(ungroupedGroup);
                }

                // Save to file
                File.WriteAllText(savePath, root.ToString(Formatting.Indented));
                DA.SetData(0, $"Components exported successfully to: {savePath}");
            }
            catch (Exception ex)
            {
                DA.SetData(0, $"Error exporting components: {ex.Message}\nPlease ensure the save path is valid and you have write permissions.");
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;
    }
} 