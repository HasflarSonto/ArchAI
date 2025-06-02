using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Special;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;

namespace GHUI
{
    public class JSONSelectedComponentsExporterComponent : GH_Component
    {
        public JSONSelectedComponentsExporterComponent()
            : base("Export Selected to JSON", "ExportSelectedJSON",
                "Exports the currently selected Grasshopper components to a JSON file",
                "UI", "Support")
        {
        }

        public override Guid ComponentGuid => new Guid("12345678-1234-1234-1234-123456789015");

        protected override void RegisterInputParams(GH_InputParamManager pManager)
        {
            pManager.AddTextParameter("Save Path", "path", "Optional path where to save the JSON file (e.g., C:\\path\\to\\file.json). If empty, saves as selectedcontext.json in the same directory.", GH_ParamAccess.item);
            pManager.AddBooleanParameter("Refresh", "R", "Set to true to refresh and export", GH_ParamAccess.item, false);
        }

        protected override void RegisterOutputParams(GH_OutputParamManager pManager)
        {
            pManager.AddTextParameter("Status", "status", "Status of the export operation", GH_ParamAccess.item);
            pManager.AddIntegerParameter("Selected Count", "count", "Number of selected components exported", GH_ParamAccess.item);
        }

        private JObject CreateComponentObject(IGH_DocumentObject obj)
        {
            var componentObj = new JObject();
            componentObj["type"] = obj.GetType().Name;
            componentObj["name"] = obj.Name;
            componentObj["position"] = new JArray(obj.Attributes.Pivot.X, obj.Attributes.Pivot.Y);

            if (obj is IGH_Component component)
            {
                if (component.GetType().Name == "Python3Component" || component.GetType().Name == "PythonComponent")
                {
                    try
                    {
                        string script = null;
                        var codeProperty = component.GetType().GetProperty("Code", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.NonPublic);
                        if (codeProperty != null)
                        {
                            script = codeProperty.GetValue(component) as string;
                        }
                        if (string.IsNullOrEmpty(script))
                        {
                            var codeField = component.GetType().GetField("_code", System.Reflection.BindingFlags.Instance | System.Reflection.BindingFlags.NonPublic);
                            if (codeField != null)
                            {
                                script = codeField.GetValue(component) as string;
                            }
                        }
                        if (!string.IsNullOrEmpty(script))
                        {
                            componentObj["script"] = script;
                        }
                    }
                    catch (Exception ex)
                    {
                        System.Diagnostics.Debug.WriteLine($"Error accessing Python component script: {ex.Message}");
                    }
                }

                var inputs = new JArray();
                var outputs = new JArray();
                componentObj["inputs"] = inputs;
                componentObj["outputs"] = outputs;

                foreach (var param in component.Params.Input)
                {
                    inputs.Add(param.Name);
                }
                foreach (var param in component.Params.Output)
                {
                    outputs.Add(param.Name);
                }
            }
            else if (obj is IGH_Param param)
            {
                var values = new JArray();
                foreach (var data in param.VolatileData.AllData(true))
                {
                    values.Add(data.ToString());
                }
                componentObj["values"] = values;
                if (param is GH_NumberSlider slider)
                {
                    componentObj["min"] = slider.Slider.Minimum;
                    componentObj["max"] = slider.Slider.Maximum;
                }
            }

            return componentObj;
        }

        private JObject CreateGroupObject(GH_Group group, List<GH_Group> allGroups)
        {
            var groupObj = new JObject();
            groupObj["name"] = group.NickName;
            groupObj["type"] = "group";
            
            // Find the direct parent group
            var parentGroup = allGroups.FirstOrDefault(g => g.Objects().Contains(group));
            groupObj["parent"] = parentGroup != null ? parentGroup.InstanceGuid.GetHashCode().ToString() : "canvas";
            
            // Find all child groups
            var children = new JArray();
            foreach (var childGroup in allGroups)
            {
                if (group.Objects().Contains(childGroup))
                {
                    children.Add(childGroup.InstanceGuid.GetHashCode().ToString());
                }
            }
            groupObj["children"] = children;
            
            groupObj["position"] = new JArray(group.Attributes.Pivot.X, group.Attributes.Pivot.Y);
            groupObj["bounds"] = new JArray(
                group.Attributes.Bounds.Left,
                group.Attributes.Bounds.Top,
                group.Attributes.Bounds.Right,
                group.Attributes.Bounds.Bottom
            );
            var groupColor = group.Attributes.Selected ? GetSelectedColor(group.Attributes) : GetUnselectedColor(group.Attributes);
            groupObj["color"] = new JArray(
                groupColor.R,
                groupColor.G,
                groupColor.B,
                groupColor.A
            );

            // Get all components in this group
            var components = new JObject();
            foreach (var obj in group.Objects())
            {
                if (obj is IGH_Component component)
                {
                    components[obj.InstanceGuid.GetHashCode().ToString()] = CreateComponentObject(component);
                }
            }
            groupObj["components"] = components;

            // Get all connections in this group
            var connections = new JArray();
            foreach (var obj in group.Objects())
            {
                if (obj is IGH_Component component)
                {
                    foreach (var param in component.Params.Input)
                    {
                        foreach (var source in param.Sources)
                        {
                            var sourceComponent = source.Attributes.GetTopLevel.DocObject as IGH_Component;
                            if (sourceComponent != null)
                            {
                                // Get the groups for both components
                                var fromGroup = allGroups.FirstOrDefault(g => g.Objects().Contains(sourceComponent));
                                var toGroup = allGroups.FirstOrDefault(g => g.Objects().Contains(component));

                                // If both components are in the same group
                                if (fromGroup == toGroup && fromGroup == group)
                                {
                                    var connection = new JObject();
                                    connection["from"] = $"{sourceComponent.InstanceGuid.GetHashCode()}:{source.Name}";
                                    connection["to"] = $"{component.InstanceGuid.GetHashCode()}:{param.Name}";
                                    connections.Add(connection);
                                }
                                // If components are in different groups, find common ancestor
                                else if (fromGroup != toGroup)
                                {
                                    var groupValues = ((IDictionary<Guid, GH_Group>)allGroups).Values.ToList();
                                    var commonAncestor = FindCommonAncestor(fromGroup, toGroup, groupValues);
                                    if (commonAncestor != null)
                                    {
                                        var connection = new JObject();
                                        connection["from"] = $"{sourceComponent.InstanceGuid.GetHashCode()}:{source.Name}";
                                        connection["to"] = $"{component.InstanceGuid.GetHashCode()}:{param.Name}";
                                        connections.Add(connection);
                                    }
                                }
                            }
                        }
                    }
                }
            }
            groupObj["connections"] = connections;

            return groupObj;
        }

        private List<GH_Group> GetAncestors(GH_Group group, Dictionary<Guid, GH_Group> allGroups)
        {
            var ancestors = new List<GH_Group>();
            var current = group;
            
            // If no group, this is a canvas-level component
            if (current == null)
            {
                return ancestors;
            }

            // Add the current group and all its ancestors
            while (current != null)
            {
                ancestors.Add(current);
                current = allGroups.Values.FirstOrDefault(g => g.Objects().Contains(current));
            }
            
            return ancestors;
        }

        private bool IsLowestCommonAncestor(GH_Group group, List<GH_Group> fromAncestors, List<GH_Group> toAncestors)
        {
            // If either component has no ancestors (canvas-level components)
            if (fromAncestors.Count == 0 || toAncestors.Count == 0)
            {
                // This connection belongs to canvas
                return group == null;
            }

            // Find common ancestors
            var commonAncestors = fromAncestors.Intersect(toAncestors).ToList();
            if (commonAncestors.Count == 0)
            {
                // If no common ancestors, this belongs to canvas
                return group == null;
            }
            
            // Get the lowest common ancestor
            var lowestCommonAncestor = commonAncestors.Last();
            
            // If this is the lowest common ancestor, it should own the connection
            if (lowestCommonAncestor == group)
            {
                return true;
            }
            
            // If this group is a parent of the lowest common ancestor, it should not own the connection
            if (fromAncestors.Contains(group) && toAncestors.Contains(group))
            {
                return false;
            }
            
            return false;
        }

        private bool IsConnectionInGroup(GH_Group group, IGH_DocumentObject fromComponent, IGH_DocumentObject toComponent, Dictionary<Guid, GH_Group> allGroups)
        {
            // Get the groups for both components
            var fromGroup = fromComponent.Attributes.GetTopLevel as GH_Group;
            var toGroup = toComponent.Attributes.GetTopLevel as GH_Group;

            // If both components are in this group or its children, check if this is the most specific group
            if (IsComponentInGroupOrChildren(group, fromComponent, allGroups) && 
                IsComponentInGroupOrChildren(group, toComponent, allGroups))
            {
                // Find the lowest common ancestor of both components
                var fromAncestors = GetAncestors(fromGroup, allGroups);
                var toAncestors = GetAncestors(toGroup, allGroups);
                
                // If this group is the lowest common ancestor, it should claim the connection
                if (IsLowestCommonAncestor(group, fromAncestors, toAncestors))
                {
                    return true;
                }
            }

            return false;
        }

        private bool IsComponentInGroupOrChildren(GH_Group group, IGH_DocumentObject component, Dictionary<Guid, GH_Group> allGroups)
        {
            // Check if component is directly in this group
            if (group.Objects().Contains(component))
                return true;

            // Check if component is in any child group
            foreach (var obj in group.Objects())
            {
                if (obj is GH_Group childGroup)
                {
                    if (IsComponentInGroupOrChildren(childGroup, component, allGroups))
                        return true;
                }
            }

            return false;
        }

        private bool IsConnectionInAnyGroup(IGH_DocumentObject fromComponent, IGH_DocumentObject toComponent, Dictionary<Guid, GH_Group> allGroups)
        {
            // Check if this connection belongs to any group
            foreach (var group in allGroups.Values)
            {
                if (IsConnectionInGroup(group, fromComponent, toComponent, allGroups))
                {
                    return true;
                }
            }
            return false;
        }

        private GH_Group FindCommonAncestor(GH_Group group1, GH_Group group2, List<GH_Group> allGroups)
        {
            if (group1 == null || group2 == null)
                return null;

            // Get all ancestors of both groups
            var ancestors1 = new List<GH_Group>();
            var ancestors2 = new List<GH_Group>();

            var current = group1;
            while (current != null)
            {
                ancestors1.Add(current);
                current = allGroups.FirstOrDefault(g => g.Objects().Contains(current));
            }

            current = group2;
            while (current != null)
            {
                ancestors2.Add(current);
                current = allGroups.FirstOrDefault(g => g.Objects().Contains(current));
            }

            // Find the first common ancestor
            foreach (var ancestor in ancestors1)
            {
                if (ancestors2.Contains(ancestor))
                    return ancestor;
            }

            return null; // No common ancestor found
        }

        private string BuildComponentPath(IGH_DocumentObject component, GH_Group group, Dictionary<Guid, GH_Group> allGroups)
        {
            var path = new List<string>();
            var currentGroup = group;

            // Build the path from the component up to the canvas
            while (currentGroup != null)
            {
                path.Insert(0, currentGroup.InstanceGuid.GetHashCode().ToString());
                currentGroup = allGroups.Values.FirstOrDefault(g => g.Objects().Contains(currentGroup));
            }

            // Add the component ID at the end
            path.Add(component.InstanceGuid.GetHashCode().ToString());

            return string.Join("/", path);
        }

        protected override void SolveInstance(IGH_DataAccess DA)
        {
            string savePath = string.Empty;
            bool refresh = false;
            if (!DA.GetData(0, ref savePath)) return;
            if (!DA.GetData(1, ref refresh)) return;

            try
            {
                var debugOutput = new System.Text.StringBuilder();
                // First, validate and prepare the save path
                if (string.IsNullOrEmpty(savePath))
                {
                    savePath = "selectedcontext.json";
                }
                else if (Directory.Exists(savePath))
                {
                    savePath = Path.Combine(savePath, "selectedcontext.json");
                }
                else if (!savePath.EndsWith(".json", StringComparison.OrdinalIgnoreCase))
                {
                    savePath = Path.Combine(Path.GetDirectoryName(savePath), "selectedcontext.json");
                }

                string directory = Path.GetDirectoryName(savePath);
                if (!string.IsNullOrEmpty(directory) && !Directory.Exists(directory))
                {
                    Directory.CreateDirectory(directory);
                }

                var doc = OnPingDocument();
                if (doc == null)
                {
                    DA.SetData(0, "Error: No active document");
                    DA.SetData(1, 0);
                    return;
                }

                // Get all selected objects
                var selectedObjects = doc.Objects.Where(obj => obj.Attributes.Selected).ToList();
                debugOutput.AppendLine($"Found {selectedObjects.Count} selected objects");

                if (selectedObjects.Count == 0)
                {
                    DA.SetData(0, "No components selected");
                    DA.SetData(1, 0);
                    return;
                }

                var root = new JObject();
                var groups = new JObject();
                root["groups"] = groups;

                // Get all groups in the document
                var allGroups = doc.Objects.OfType<GH_Group>().ToDictionary(g => g.InstanceGuid);
                debugOutput.AppendLine($"Found {allGroups.Count} total groups in document");

                // Get selected groups and components
                var selectedGroups = selectedObjects.OfType<GH_Group>().ToList();
                var selectedComponents = selectedObjects.Where(obj => obj is IGH_Component || obj is IGH_Param).ToList();
                debugOutput.AppendLine($"Found {selectedGroups.Count} selected groups and {selectedComponents.Count} selected components");

                // Track all connections first
                var allConnections = new List<(IGH_DocumentObject from, IGH_DocumentObject to, string fromParam, string toParam)>();
                foreach (var obj in selectedObjects)
                {
                    if (obj is IGH_Component component)
                    {
                        debugOutput.AppendLine($"\nProcessing component: {component.Name} (Type: {component.GetType().Name})");
                        foreach (var param in component.Params.Input)
                        {
                            debugOutput.AppendLine($"  Checking input parameter: {param.Name}");
                            foreach (var source in param.Sources)
                            {
                                var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                                if (sourceComponent != null && selectedObjects.Contains(sourceComponent))
                                {
                                    debugOutput.AppendLine($"    Found source: {sourceComponent.Name} (Type: {sourceComponent.GetType().Name})");
                                    allConnections.Add((sourceComponent, component, source.Name, param.Name));
                                    debugOutput.AppendLine($"    Added connection: {sourceComponent.Name} -> {component.Name}");
                                }
                            }
                        }
                    }
                    else if (obj is IGH_Param param)
                    {
                        debugOutput.AppendLine($"\nProcessing parameter: {param.Name} (Type: {param.GetType().Name})");
                        foreach (var source in param.Sources)
                        {
                            var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                            if (sourceComponent != null && selectedObjects.Contains(sourceComponent))
                            {
                                debugOutput.AppendLine($"  Found source: {sourceComponent.Name} (Type: {sourceComponent.GetType().Name})");
                                allConnections.Add((sourceComponent, param, source.Name, param.Name));
                                debugOutput.AppendLine($"  Added connection: {sourceComponent.Name} -> {param.Name}");
                            }
                        }
                    }
                }

                // Create a map of components to their groups
                var componentToGroupMap = new Dictionary<IGH_DocumentObject, GH_Group>();
                debugOutput.AppendLine("\nMapping components to groups:");
                foreach (var group in selectedGroups)
                {
                    debugOutput.AppendLine($"\nProcessing group: {group.NickName}");
                    foreach (var obj in group.Objects())
                    {
                        if (obj is IGH_Component || obj is IGH_Param)
                        {
                            componentToGroupMap[obj] = group;
                            debugOutput.AppendLine($"  Mapped component {obj.Name} (Type: {obj.GetType().Name}) to group {group.NickName}");
                        }
                    }
                }

                // Create a map of groups to their connections
                var groupConnections = new Dictionary<GH_Group, JArray>();
                foreach (var group in selectedGroups)
                {
                    groupConnections[group] = new JArray();
                }
                var canvasConnections = new JArray();

                // Process each connection
                foreach (var (from, to, fromParam, toParam) in allConnections)
                {
                    debugOutput.AppendLine($"\nProcessing connection: {from.Name} -> {to.Name}");
                    debugOutput.AppendLine($"  From type: {from.GetType().Name}");
                    debugOutput.AppendLine($"  To type: {to.GetType().Name}");
                    
                    // Get the groups for both components
                    componentToGroupMap.TryGetValue(from, out var fromGroup);
                    componentToGroupMap.TryGetValue(to, out var toGroup);
                    
                    debugOutput.AppendLine($"  From component group: {(fromGroup != null ? fromGroup.NickName : "canvas")}");
                    debugOutput.AppendLine($"  To component group: {(toGroup != null ? toGroup.NickName : "canvas")}");

                    // If either component is not in a group, add to canvas
                    if (fromGroup == null || toGroup == null)
                    {
                        debugOutput.AppendLine("  One or both components not in a group, adding to canvas");
                        var connection = new JObject();
                        string fromPath = fromGroup != null 
                            ? BuildComponentPath(from, fromGroup, allGroups)
                            : from.InstanceGuid.GetHashCode().ToString();
                        string toPath = toGroup != null 
                            ? BuildComponentPath(to, toGroup, allGroups)
                            : to.InstanceGuid.GetHashCode().ToString();
                        
                        connection["from"] = $"{fromPath}:{fromParam}";
                        connection["to"] = $"{toPath}:{toParam}";
                        canvasConnections.Add(connection);
                        debugOutput.AppendLine($"  Added canvas connection: {fromPath}:{fromParam} -> {toPath}:{toParam}");
                        continue;
                    }

                    // If both components are in the same group
                    if (fromGroup == toGroup)
                    {
                        debugOutput.AppendLine($"  Both components in same group: {fromGroup.NickName}");
                        var connection = new JObject();
                        string fromPath = BuildComponentPath(from, fromGroup, allGroups);
                        string toPath = BuildComponentPath(to, toGroup, allGroups);
                        connection["from"] = $"{fromPath}:{fromParam}";
                        connection["to"] = $"{toPath}:{toParam}";
                        groupConnections[fromGroup].Add(connection);
                        debugOutput.AppendLine($"  Added connection to group: {fromGroup.NickName}");
                    }
                    // If components are in different groups, find common ancestor
                    else
                    {
                        debugOutput.AppendLine("  Components in different groups, finding common ancestor");
                        var commonAncestor = FindCommonAncestor(fromGroup, toGroup, allGroups.Values.ToList());
                        if (commonAncestor != null)
                        {
                            debugOutput.AppendLine($"  Found common ancestor: {commonAncestor.NickName}");
                            var connection = new JObject();
                            string fromPath = BuildComponentPath(from, fromGroup, allGroups);
                            string toPath = BuildComponentPath(to, toGroup, allGroups);
                            connection["from"] = $"{fromPath}:{fromParam}";
                            connection["to"] = $"{toPath}:{toParam}";
                            groupConnections[commonAncestor].Add(connection);
                            debugOutput.AppendLine($"  Added connection to common ancestor: {commonAncestor.NickName}");
                        }
                        else
                        {
                            debugOutput.AppendLine("  No common ancestor found, adding to canvas");
                            var connection = new JObject();
                            string fromPath = BuildComponentPath(from, fromGroup, allGroups);
                            string toPath = BuildComponentPath(to, toGroup, allGroups);
                            connection["from"] = $"{fromPath}:{fromParam}";
                            connection["to"] = $"{toPath}:{toParam}";
                            canvasConnections.Add(connection);
                            debugOutput.AppendLine($"  Added canvas connection: {fromPath}:{fromParam} -> {toPath}:{toParam}");
                        }
                    }
                }

                // Create group objects for all processed groups
                foreach (var group in selectedGroups)
                {
                    debugOutput.AppendLine($"\nProcessing group: {group.NickName}");
                    var groupObj = CreateGroupObject(group, allGroups.Values.ToList());
                    
                    // Get all components in this group
                    var components = new JObject();
                    foreach (var obj in group.Objects())
                    {
                        // Only add components that are directly in this group (not in child groups)
                        if ((obj is IGH_Component || obj is IGH_Param) && !IsComponentInChildGroup(obj, group, allGroups))
                        {
                            debugOutput.AppendLine($"  Adding component to group: {obj.Name} (Type: {obj.GetType().Name})");
                            components[obj.InstanceGuid.GetHashCode().ToString()] = CreateComponentObject(obj);
                        }
                    }
                    groupObj["components"] = components;
                    
                    // Add connections for this group
                    groupObj["connections"] = groupConnections[group];
                    groups[group.InstanceGuid.GetHashCode().ToString()] = groupObj;
                }

                // Create ungrouped components section (canvas)
                var ungroupedComponents = new JObject();
                var ungroupedConnections = new JArray();

                // Add components that weren't claimed by any group
                foreach (var component in selectedComponents)
                {
                    if (!componentToGroupMap.ContainsKey(component))
                    {
                        debugOutput.AppendLine($"\nAdding canvas component: {component.Name} (Type: {component.GetType().Name})");
                        ungroupedComponents[component.InstanceGuid.GetHashCode().ToString()] = CreateComponentObject(component);

                        // Add connections for canvas components
                        if (component is IGH_Component comp)
                        {
                            foreach (var param in comp.Params.Input)
                            {
                                foreach (var source in param.Sources)
                                {
                                    var sourceComponent = source.Attributes.GetTopLevel.DocObject;
                                    if (sourceComponent != null)
                                    {
                                        var sourceGroup = componentToGroupMap.ContainsKey(sourceComponent) 
                                            ? componentToGroupMap[sourceComponent] 
                                            : null;

                                        // Only add connections that don't belong to any other group
                                        if (!IsConnectionInAnyGroup(sourceComponent, comp, allGroups))
                                        {
                                            var connection = new JObject();
                                            string fromPath = sourceGroup != null 
                                                ? BuildComponentPath(sourceComponent, sourceGroup, allGroups)
                                                : sourceComponent.InstanceGuid.GetHashCode().ToString();
                                            string toPath = component.InstanceGuid.GetHashCode().ToString();

                                            connection["from"] = $"{fromPath}:{source.Name}";
                                            connection["to"] = $"{toPath}:{param.Name}";
                                            ungroupedConnections.Add(connection);
                                            debugOutput.AppendLine($"  Added canvas connection: {fromPath}:{source.Name} -> {toPath}:{param.Name}");
                                        }
                                    }
                                }
                            }
                        }
                    }
                }

                // Add canvas components as the canvas group
                var canvasGroup = new JObject();
                canvasGroup["name"] = "Canvas";
                canvasGroup["type"] = "canvas";
                canvasGroup["parent"] = null;
                
                // Add all top-level groups as children of the canvas
                var canvasChildren = new JArray();
                foreach (var group in selectedGroups)
                {
                    // Only add groups that don't have a parent in the selected groups
                    bool hasParent = false;
                    foreach (var otherGroup in selectedGroups)
                    {
                        if (otherGroup.Objects().Contains(group))
                        {
                            hasParent = true;
                            break;
                        }
                    }
                    if (!hasParent)
                    {
                        canvasChildren.Add(group.InstanceGuid.GetHashCode().ToString());
                    }
                }
                canvasGroup["children"] = canvasChildren;
                
                canvasGroup["position"] = new JArray(0, 0);
                canvasGroup["bounds"] = new JArray(0, 0, 0, 0);
                canvasGroup["components"] = ungroupedComponents;
                canvasGroup["connections"] = canvasConnections;
                groups["canvas"] = canvasGroup;

                // Generate JSON with custom formatting
                var settings = new JsonSerializerSettings
                {
                    Formatting = Formatting.Indented,
                    Converters = new[] { new CompactArrayConverter() }
                };

                // Format specific arrays directly in the JObject
                FormatCompactArrays(root);

                var json = JsonConvert.SerializeObject(root, settings);
                debugOutput.AppendLine($"Generated JSON: {json}");
                
                // Write to file - this is the critical part
                try
                {
                    debugOutput.AppendLine($"Attempting to write to: {savePath}");
                    File.WriteAllText(savePath, json, System.Text.Encoding.UTF8);
                    debugOutput.AppendLine("File write completed successfully");
                    
                    // Verify the file was written
                    if (File.Exists(savePath))
                    {
                        var fileContent = File.ReadAllText(savePath);
                        debugOutput.AppendLine($"File written successfully. File size: {fileContent.Length} bytes");
                    }
                    else
                    {
                        throw new Exception("File does not exist after write attempt");
                    }
                }
                catch (Exception ex)
                {
                    debugOutput.AppendLine($"Error writing file: {ex.Message}");
                    throw;
                }
                
                DA.SetData(0, $"Selected components exported successfully to: {savePath}\n\nDebug Info:\n{debugOutput}");
                DA.SetData(1, selectedObjects.Count);
            }
            catch (Exception ex)
            {
                var errorOutput = new System.Text.StringBuilder();
                errorOutput.AppendLine($"Error exporting components: {ex.Message}");
                errorOutput.AppendLine($"Stack trace: {ex.StackTrace}");
                DA.SetData(0, errorOutput.ToString());
                DA.SetData(1, 0);
            }
        }

        private void FormatCompactArrays(JObject obj)
        {
            foreach (var property in obj.Properties().ToList())
            {
                if (property.Value is JObject childObj)
                {
                    FormatCompactArrays(childObj);
                }
                else if (property.Value is JArray array)
                {
                    if (property.Name.Equals("position", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("bounds", StringComparison.OrdinalIgnoreCase) ||
                        property.Name.Equals("color", StringComparison.OrdinalIgnoreCase))
                    {
                        property.Value = JToken.Parse(array.ToString(Formatting.None));
                    }
                }
            }
        }

        public override GH_Exposure Exposure => GH_Exposure.primary;

        protected override System.Drawing.Bitmap Icon => null;

        private System.Drawing.Color GetSelectedColor(IGH_Attributes attributes)
        {
            return System.Drawing.Color.FromArgb(255, 0, 0, 255); // Blue color for selected
        }

        private System.Drawing.Color GetUnselectedColor(IGH_Attributes attributes)
        {
            return System.Drawing.Color.FromArgb(255, 0, 0, 0); // Black color for unselected
        }

        private bool IsComponentInChildGroup(IGH_DocumentObject component, GH_Group parentGroup, Dictionary<Guid, GH_Group> allGroups)
        {
            // Check if the component is in any child group of the parent group
            foreach (var obj in parentGroup.Objects())
            {
                if (obj is GH_Group childGroup)
                {
                    if (childGroup.Objects().Contains(component))
                        return true;
                    
                    // Recursively check child groups
                    if (IsComponentInChildGroup(component, childGroup, allGroups))
                        return true;
                }
            }
            return false;
        }
    }

    // Custom converter to make arrays more compact
    public class CompactArrayConverter : JsonConverter
    {
        private static readonly HashSet<string> CompactProperties = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "position",
            "bounds",
            "color"
        };

        public override bool CanConvert(Type objectType)
        {
            return objectType == typeof(JArray);
        }

        public override object ReadJson(JsonReader reader, Type objectType, object existingValue, JsonSerializer serializer)
        {
            return JArray.Load(reader);
        }

        public override void WriteJson(JsonWriter writer, object value, JsonSerializer serializer)
        {
            var array = (JArray)value;
            var parent = array.Parent as JProperty;
            
            if (parent != null && CompactProperties.Contains(parent.Name))
            {
                writer.WriteRawValue(array.ToString(Formatting.None));
            }
            else
            {
                array.WriteTo(writer);
            }
        }
    }
} 