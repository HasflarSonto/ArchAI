using System;
using System.Collections.Generic;
using System.Linq;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Parameters;
using Grasshopper.Kernel.Special;
using Grasshopper;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;

namespace GHUI.Classes
{
    public class JSONComponentLoader
    {
        private static readonly Dictionary<string, string> fuzzyPairs = new Dictionary<string, string>()
        {
            { "Extrusion", "Extrude" },
            { "Text Panel", "Panel" }
        };

        private static readonly Dictionary<string, IGH_DocumentObject> createdComponents = new Dictionary<string, IGH_DocumentObject>();
        private static readonly Dictionary<string, GH_Group> createdGroups = new Dictionary<string, GH_Group>();
        private static string debugOutput = "";
        private static readonly Dictionary<string, string> groupIdToName = new Dictionary<string, string>();
        private static readonly Dictionary<string, string> groupNameToId = new Dictionary<string, string>();
        private static readonly Dictionary<string, Guid> componentIdToGuid = new Dictionary<string, Guid>();

        public static void LoadComponentsFromJSON(GH_Document doc, string jsonPath)
        {
            debugOutput = ""; // Reset debug output
            try
            {
                debugOutput += $"Starting to load from {jsonPath}\n";
                string jsonContent = System.IO.File.ReadAllText(jsonPath);
                debugOutput += $"Found {jsonContent.Length} bytes of JSON data\n";
                
                var root = JObject.Parse(jsonContent);
                var groups = root["groups"] as JObject;
                if (groups == null)
                {
                    debugOutput += "Error: No groups found in JSON\n";
                    return;
                }
                debugOutput += $"Found {groups.Count} groups in JSON\n";

                // Build group ID mappings and data
                var groupData = new Dictionary<string, JObject>(); // groupId -> group data
                var groupNameToId = new Dictionary<string, string>(); // groupName -> groupId
                var groupIdToName = new Dictionary<string, string>(); // groupId -> groupName

                foreach (var group in groups)
                {
                    if (group.Key == "canvas") continue;

                    var groupObj = group.Value as JObject;
                    if (groupObj == null) continue;

                    string groupId = group.Key;
                    string groupName = groupObj["name"]?.ToString();
                    
                    if (!string.IsNullOrEmpty(groupName))
                    {
                        groupData[groupId] = groupObj;
                        groupNameToId[groupName] = groupId;
                        groupIdToName[groupId] = groupName;
                    }
                }

                // First pass: Create all components
                debugOutput += "\nCreating components:\n";
                foreach (var group in groups)
                {
                    var groupObj = group.Value as JObject;
                    if (groupObj == null) continue;

                    var components = groupObj["components"] as JObject;
                    if (components == null) continue;

                    foreach (var component in components)
                    {
                        try
                        {
                            var componentObj = component.Value as JObject;
                            if (componentObj == null) continue;

                            debugOutput += $"  Creating component: {component.Key} (Type: {componentObj["type"]})\n";
                            var newComponent = CreateComponent(doc, componentObj);
                            if (newComponent == null)
                            {
                                debugOutput += $"    Failed to create component\n";
                                continue;
                            }

                            // Set component position
                            var position = componentObj["position"]?.ToObject<double[]>();
                            if (position != null && position.Length >= 2)
                            {
                                newComponent.Attributes.Pivot = new System.Drawing.PointF(
                                    (float)position[0], (float)position[1]
                                );
                                debugOutput += $"    Set position to: {position[0]}, {position[1]}\n";
                            }

                            // Add component to document
                            doc.AddObject(newComponent, false);
                            createdComponents[component.Key] = newComponent;
                            componentIdToGuid[component.Key] = newComponent.InstanceGuid;
                            debugOutput += $"    Component created with GUID: {newComponent.InstanceGuid}\n";
                        }
                        catch (Exception ex)
                        {
                            debugOutput += $"    Error creating component: {ex.Message}\n";
                        }
                    }
                }

                // Second pass: Create all connections
                debugOutput += "\nCreating connections:\n";
                foreach (var group in groups)
                {
                    try
                    {
                        CreateConnections(doc, group.Value as JObject);
                    }
                    catch (Exception ex)
                    {
                        debugOutput += $"Error creating connections for group {group.Key}: {ex.Message}\n";
                    }
                }

                // Third pass: Create groups in top-down order
                debugOutput += "\nCreating groups in top-down order:\n";

                // Calculate group depths
                var groupDepths = new Dictionary<string, int>();

                foreach (var group in groups)
                {
                    if (group.Key == "canvas") continue;

                    var groupObj = group.Value as JObject;
                    if (groupObj == null) continue;

                    string groupId = group.Key;
                    groupData[groupId] = groupObj;
                    groupDepths[groupId] = CalculateGroupDepth(groupId, groupObj, groupData);
                }

                // Sort groups by depth (shallowest first)
                var sortedGroups = groupDepths.OrderBy(x => x.Value).Select(x => x.Key).ToList();

                // First pass: Create all groups
                foreach (var groupId in sortedGroups)
                {
                    if (!groupData.TryGetValue(groupId, out var groupObj))
                    {
                        debugOutput += $"Warning: Group data not found for {groupId}\n";
                        continue;
                    }

                    debugOutput += $"\nCreating group: {groupId} (Depth: {groupDepths[groupId]})\n";

                    // Create the group
                    var ghGroup = new GH_Group();
                    ghGroup.NickName = groupIdToName.ContainsKey(groupId) ? groupIdToName[groupId] : groupId;
                    ghGroup.CreateAttributes();

                    // Set group properties
                    var position = groupObj["position"]?.ToObject<double[]>();
                    var bounds = groupObj["bounds"]?.ToObject<double[]>();
                    
                    if (position != null && position.Length >= 2)
                    {
                        ghGroup.Attributes.Pivot = new System.Drawing.PointF(
                            (float)position[0], (float)position[1]
                        );
                    }
                    
                    if (bounds != null && bounds.Length >= 4)
                    {
                        ghGroup.Attributes.Bounds = new System.Drawing.RectangleF(
                            (float)bounds[0], (float)bounds[1],
                            (float)(bounds[2] - bounds[0]), (float)(bounds[3] - bounds[1])
                        );
                    }

                    ghGroup.Colour = System.Drawing.Color.FromArgb(100, 0, 0, 255);

                    // Add group to document
                    doc.AddObject(ghGroup, false);
                    createdGroups[groupId] = ghGroup;
                }

                // Second pass: Populate groups with components and child groups
                foreach (var groupId in sortedGroups)
                {
                    if (!groupData.TryGetValue(groupId, out var groupObj))
                        continue;

                    debugOutput += $"\nPopulating group: {groupId}\n";
                    var ghGroup = createdGroups[groupId];

                    // Add components to the group
                    var components = groupObj["components"] as JObject;
                    if (components != null)
                    {
                        foreach (var component in components)
                        {
                            if (componentIdToGuid.TryGetValue(component.Key, out Guid componentGuid))
                            {
                                try
                                {
                                    ghGroup.AddObject(componentGuid);
                                    debugOutput += $"    Added component {component.Key} to group\n";
                                }
                                catch (Exception ex)
                                {
                                    debugOutput += $"    Error adding component {component.Key} to group: {ex.Message}\n";
                                }
                            }
                            else
                            {
                                debugOutput += $"    Warning: Component {component.Key} not found in component mapping\n";
                            }
                        }
                    }

                    // Add child groups to this group
                    var children = groupObj["children"] as JArray;
                    if (children != null)
                    {
                        foreach (var childId in children)
                        {
                            string childGroupId = childId.ToString();
                            if (createdGroups.TryGetValue(childGroupId, out var childGroup))
                            {
                                try
                                {
                                    ghGroup.AddObject(childGroup.InstanceGuid);
                                    debugOutput += $"    Added child group {childGroupId} to parent group\n";
                                }
                                catch (Exception ex)
                                {
                                    debugOutput += $"    Error adding child group {childGroupId}: {ex.Message}\n";
                                }
                            }
                            else
                            {
                                debugOutput += $"    Warning: Child group {childGroupId} not found in created groups\n";
                            }
                        }
                    }

                    // Force group to update
                    ghGroup.ExpireCaches();
                }

                debugOutput += $"\nCreated {createdGroups.Count} groups successfully\n";
                debugOutput += "\nComponent loading completed successfully\n";

                // Schedule a solution refresh
                doc.ScheduleSolution(1, d => {
                    debugOutput += "Document refresh scheduled\n";
                });
            }
            catch (Exception ex)
            {
                debugOutput += $"Error loading components: {ex.Message}\n{ex.StackTrace}\n";
                throw;
            }
        }

        private static void CreateGroup(GH_Document doc, JObject groupObj)
        {
            if (groupObj == null)
            {
                debugOutput += "Error: Group object is null\n";
                return;
            }

            string groupId = groupObj["name"]?.ToString();
            if (string.IsNullOrEmpty(groupId))
            {
                debugOutput += "Error: Group name is missing or empty\n";
                return;
            }

            string parentId = groupObj["parent"]?.ToString();
            
            debugOutput += $"Creating group: {groupId} (Parent: {parentId})\n";

            // Create the group
            var group = new GH_Group();
            group.NickName = groupId;
            
            // Create attributes for the group
            group.CreateAttributes();
            
            // Set group position and bounds
            var position = groupObj["position"]?.ToObject<double[]>();
            var bounds = groupObj["bounds"]?.ToObject<double[]>();
            
            if (position != null && position.Length >= 2)
            {
                debugOutput += $"Setting group position to: {position[0]}, {position[1]}\n";
                group.Attributes.Pivot = new System.Drawing.PointF((float)position[0], (float)position[1]);
            }
            else
            {
                debugOutput += $"Warning: Invalid position data for group {groupId}\n";
            }
            
            if (bounds != null && bounds.Length >= 4)
            {
                debugOutput += $"Setting group bounds: [{bounds[0]}, {bounds[1]}, {bounds[2]}, {bounds[3]}]\n";
                group.Attributes.Bounds = new System.Drawing.RectangleF(
                    (float)bounds[0], (float)bounds[1],
                    (float)(bounds[2] - bounds[0]), (float)(bounds[3] - bounds[1])
                );
            }
            else
            {
                debugOutput += $"Warning: Invalid bounds data for group {groupId}\n";
            }

            // Set group color if available
            if (groupObj["color"] != null)
            {
                var color = groupObj["color"].ToObject<int[]>();
                if (color != null && color.Length >= 4)
                {
                    debugOutput += $"Setting group color: [{color[0]}, {color[1]}, {color[2]}, {color[3]}]\n";
                    group.Colour = System.Drawing.Color.FromArgb(
                        color[3], color[0], color[1], color[2]
                    );
                }
                else
                {
                    debugOutput += $"Warning: Invalid color data for group {groupId}\n";
                }
            }

            // Add components to the group
            var components = groupObj["components"] as JObject;
            if (components != null)
            {
                debugOutput += $"Adding {components.Count} components to group:\n";
                foreach (var component in components)
                {
                    if (createdComponents.TryGetValue(component.Key, out var componentObj))
                    {
                        debugOutput += $"  Adding component {component.Key} (GUID: {componentObj.InstanceGuid})\n";
                        try
                        {
                            group.AddObject(componentObj.InstanceGuid);
                            debugOutput += $"  Successfully added component to group\n";
                        }
                        catch (Exception ex)
                        {
                            debugOutput += $"  Error adding component to group: {ex.Message}\n";
                        }
                    }
                    else
                    {
                        debugOutput += $"  Warning: Component {component.Key} not found in created components\n";
                    }
                }
            }
            else
            {
                debugOutput += $"Warning: No components found for group {groupId}\n";
            }

            // Add group to document
            debugOutput += $"Adding group to document\n";
            doc.AddObject(group, false);
            debugOutput += $"Group added to document with GUID: {group.InstanceGuid}\n";

            // Store the group
            createdGroups[groupId] = group;
            debugOutput += $"Successfully created and populated group: {groupId} (GUID: {group.InstanceGuid})\n";

            // Force group to update
            group.ExpireCaches();
            debugOutput += $"Group caches expired\n";
        }

        private static void CreateComponentsInGroup(GH_Document doc, JObject groupObj)
        {
            string groupId = groupObj["name"].ToString();
            var components = groupObj["components"] as JObject;
            
            if (components == null) return;

            debugOutput += $"Creating components in group: {groupId}\n";

            foreach (var component in components)
            {
                string componentId = component.Key;
                var componentObj = component.Value as JObject;
                
                // Create the component
                var newComponent = CreateComponent(doc, componentObj);
                if (newComponent == null) continue;

                // Set component position
                var position = componentObj["position"].ToObject<double[]>();
                newComponent.Attributes.Pivot = new System.Drawing.PointF(
                    (float)position[0], (float)position[1]
                );

                // Add component to group
                if (groupId != "canvas")
                {
                    var group = createdGroups[groupId];
                    group.AddObject(newComponent.InstanceGuid);
                }
                else
                {
                    doc.AddObject(newComponent, false);
                }

                createdComponents[componentId] = newComponent;
                debugOutput += $"Component {componentId} created successfully\n";
            }
        }

        private static IGH_DocumentObject CreateComponent(GH_Document doc, JObject componentObj)
        {
            if (componentObj == null)
            {
                debugOutput += "Error: Component object is null\n";
                return null;
            }

            string name = componentObj["name"]?.ToString();

            if (string.IsNullOrEmpty(name))
            {
                debugOutput += "Error: Component name is missing or empty\n";
                return null;
            }

            debugOutput += $"Creating component: {name}\n";

            // Create the component using the display name
            IGH_DocumentObject component = null;
            IGH_ObjectProxy[] results = Array.Empty<IGH_ObjectProxy>();
            double[] resultWeights = new double[] { 0 };
            
            // First try with the display name
            debugOutput += $"Searching for component with name: {name}\n";
            Instances.ComponentServer.FindObjects(new string[] { name }, 10, ref results, ref resultWeights);

            var myProxies = results.Where(ghpo => ghpo.Kind == GH_ObjectType.CompiledObject);
            var myProxy = myProxies.FirstOrDefault();

            if (myProxy == null)
            {
                // If no exact match found, try searching with a broader query
                debugOutput += $"No exact match found for {name}, trying broader search...\n";
                Instances.ComponentServer.FindObjects(new string[] { "*" }, 100, ref results, ref resultWeights);
                
                debugOutput += "Available components:\n";
                foreach (var proxy in results.Where(ghpo => ghpo.Kind == GH_ObjectType.CompiledObject))
                {
                    debugOutput += $"  - {proxy.Desc.Name} (Category: {proxy.Desc.Category}, Subcategory: {proxy.Desc.SubCategory})\n";
                }
                
                myProxies = results.Where(ghpo => 
                    ghpo.Kind == GH_ObjectType.CompiledObject && 
                    ghpo.Desc.Name.ToLower().Contains(name.ToLower()));
                
                myProxy = myProxies.FirstOrDefault();
                
                if (myProxy != null)
                {
                    debugOutput += $"Found matching component: {myProxy.Desc.Name}\n";
                }
                else
                {
                    debugOutput += $"No matching component found for: {name}\n";
                }
            }

            if (myProxy != null)
            {
                try
                {
                    component = Instances.ComponentServer.EmitObject(myProxy.Guid);
                    if (component != null)
                    {
                        debugOutput += $"Successfully created component: {component.GetType().Name}\n";
                        // Set component name if provided
                        if (!string.IsNullOrEmpty(name))
                        {
                            component.NickName = name;
                        }

                        // Handle parameters during creation
                        if (componentObj["values"] != null)
                        {
                            var values = componentObj["values"] as JArray;
                            if (values != null)
                            {
                                if (component is Param_Point point)
                                {
                                    // Clear any existing data
                                    point.ClearData();
                                    
                                    // Add each point from the array
                                    for (int i = 0; i < values.Count; i++)
                                    {
                                        string pointStr = values[i].ToString();
                                        if (!string.IsNullOrEmpty(pointStr))
                                        {
                                            try
                                            {
                                                // Remove the curly braces and split by comma
                                                var cleanValue = pointStr.Trim('{', '}');
                                                var coords = cleanValue.Split(',')
                                                    .Select(s => s.Trim())
                                                    .Select(double.Parse)
                                                    .ToArray();
                                                    
                                                if (coords.Length >= 3)
                                                {
                                                    var point3d = new Rhino.Geometry.Point3d(coords[0], coords[1], coords[2]);
                                                    point.AddVolatileData(new GH_Path(0), i, new GH_Point(point3d));
                                                    debugOutput += $"Successfully set point coordinates: {point3d}\n";
                                                }
                                            }
                                            catch (Exception ex)
                                            {
                                                debugOutput += $"Warning: Error parsing point coordinates: {ex.Message}\n";
                                            }
                                        }
                                    }
                                }
                                else if (component is GH_Panel panel)
                                {
                                    panel.SetUserText(values[0].ToString());
                                }
                                else if (component is Param_Brep brep)
                                {
                                    try
                                    {
                                        var brepData = JObject.Parse(values[0].ToString());
                                        var newBrep = new Rhino.Geometry.Brep();
                                        
                                        if (brepData["faces"] != null)
                                        {
                                            var faces = brepData["faces"] as JArray;
                                            if (faces != null)
                                            {
                                                foreach (var face in faces)
                                                {
                                                    debugOutput += "Warning: Complex Brep face parsing not implemented yet\n";
                                                }
                                            }
                                        }
                                        
                                        if (newBrep.Faces.Count == 0)
                                        {
                                            var box = new Rhino.Geometry.Box(
                                                new Rhino.Geometry.Plane(
                                                    new Rhino.Geometry.Point3d(0, 0, 0),
                                                    new Rhino.Geometry.Vector3d(0, 0, 1)
                                                ),
                                                new Rhino.Geometry.Interval(-1, 1),
                                                new Rhino.Geometry.Interval(-1, 1),
                                                new Rhino.Geometry.Interval(-1, 1)
                                            );
                                            newBrep = box.ToBrep();
                                        }
                                        
                                        brep.PersistentData.Clear();
                                        brep.PersistentData.Append(new GH_Brep(newBrep));
                                    }
                                    catch (Exception ex)
                                    {
                                        debugOutput += $"Warning: Error creating Brep: {ex.Message}\n";
                                        var box = new Rhino.Geometry.Box(
                                            new Rhino.Geometry.Plane(
                                                new Rhino.Geometry.Point3d(0, 0, 0),
                                                new Rhino.Geometry.Vector3d(0, 0, 1)
                                            ),
                                            new Rhino.Geometry.Interval(-1, 1),
                                            new Rhino.Geometry.Interval(-1, 1),
                                            new Rhino.Geometry.Interval(-1, 1)
                                        );
                                        brep.PersistentData.Clear();
                                        brep.PersistentData.Append(new GH_Brep(box.ToBrep()));
                                    }
                                }
                                else if (component is Param_Number number)
                                {
                                    if (values.Count > 0 && double.TryParse(values[0].ToString(), out double num))
                                    {
                                        number.PersistentData.Clear();
                                        number.PersistentData.Append(new GH_Number(num));
                                    }
                                }
                                else if (component is Param_Boolean boolean)
                                {
                                    if (values.Count > 0 && bool.TryParse(values[0].ToString(), out bool boolVal))
                                    {
                                        boolean.PersistentData.Clear();
                                        boolean.PersistentData.Append(new GH_Boolean(boolVal));
                                    }
                                }
                                else if (component is Param_Integer integer)
                                {
                                    if (values.Count > 0 && int.TryParse(values[0].ToString(), out int intVal))
                                    {
                                        integer.PersistentData.Clear();
                                        integer.PersistentData.Append(new GH_Integer(intVal));
                                    }
                                }
                            }
                        }

                        // Handle Python components
                        if (component is IGH_Component ghComponent && 
                            (name == "Python3Component" || name == "PythonComponent"))
                        {
                            if (componentObj["script"] != null)
                            {
                                string script = componentObj["script"].ToString();
                                var codeProperty = component.GetType().GetProperty("Code", 
                                    System.Reflection.BindingFlags.Instance | 
                                    System.Reflection.BindingFlags.Public | 
                                    System.Reflection.BindingFlags.NonPublic);
                                if (codeProperty != null)
                                {
                                    codeProperty.SetValue(component, script);
                                }
                            }
                        }

                        // Set component position
                        var position = componentObj["position"]?.ToObject<double[]>();
                        if (position != null && position.Length >= 2)
                        {
                            // Create attributes if they don't exist
                            if (component.Attributes == null)
                            {
                                component.CreateAttributes();
                            }

                            // Set the pivot point
                            component.Attributes.Pivot = new System.Drawing.PointF(
                                (float)position[0], (float)position[1]
                            );

                            // Force the component to update its position
                            component.Attributes.ExpireLayout();
                            component.Attributes.PerformLayout();
                            
                            debugOutput += $"Set component position to: {position[0]}, {position[1]}\n";
                        }

                        // Set data handling options for inputs and outputs
                        if (component is IGH_Component ghComp)
                        {
                            // Handle input parameters
                            var inputs = componentObj["inputs"] as JObject;
                            if (inputs != null)
                            {
                                foreach (var input in inputs)
                                {
                                    var param = ghComp.Params.Input.FirstOrDefault(p => p.Name == input.Key);
                                    if (param != null)
                                    {
                                        var dataHandling = input.Value as JArray;
                                        if (dataHandling != null)
                                        {
                                            // Reset data mapping
                                            param.DataMapping = GH_DataMapping.None;
                                            
                                            // Apply data handling options
                                            foreach (var option in dataHandling)
                                            {
                                                string optionStr = option.ToString().ToLower();
                                                switch (optionStr)
                                                {
                                                    case "flatten":
                                                        param.DataMapping |= GH_DataMapping.Flatten;
                                                        break;
                                                    case "graft":
                                                        param.DataMapping |= GH_DataMapping.Graft;
                                                        break;
                                                    case "simplify":
                                                        param.Simplify = true;
                                                        break;
                                                    case "reverse":
                                                        param.Reverse = true;
                                                        break;
                                                }
                                            }
                                            debugOutput += $"Set data handling for input {input.Key}: {string.Join(", ", dataHandling)}\n";
                                        }
                                    }
                                }
                            }

                            // Handle output parameters
                            var outputs = componentObj["outputs"] as JObject;
                            if (outputs != null)
                            {
                                foreach (var output in outputs)
                                {
                                    var param = ghComp.Params.Output.FirstOrDefault(p => p.Name == output.Key);
                                    if (param != null)
                                    {
                                        var dataHandling = output.Value as JArray;
                                        if (dataHandling != null)
                                        {
                                            // Reset data mapping
                                            param.DataMapping = GH_DataMapping.None;
                                            
                                            // Apply data handling options
                                            foreach (var option in dataHandling)
                                            {
                                                string optionStr = option.ToString().ToLower();
                                                switch (optionStr)
                                                {
                                                    case "flatten":
                                                        param.DataMapping |= GH_DataMapping.Flatten;
                                                        break;
                                                    case "graft":
                                                        param.DataMapping |= GH_DataMapping.Graft;
                                                        break;
                                                    case "simplify":
                                                        param.Simplify = true;
                                                        break;
                                                    case "reverse":
                                                        param.Reverse = true;
                                                        break;
                                                }
                                            }
                                            debugOutput += $"Set data handling for output {output.Key}: {string.Join(", ", dataHandling)}\n";
                                        }
                                    }
                                }
                            }
                        }

                        // Trigger the component
                        if (component is IGH_Component)
                        {
                            ((IGH_Component)component).ExpireSolution(false);
                        }

                        // Add component to document
                        doc.AddObject(component, false);
                        createdComponents[component.InstanceGuid.ToString()] = component;
                        debugOutput += $"Component {component.InstanceGuid} created successfully\n";
                    }
                    else
                    {
                        debugOutput += $"Failed to emit component of type: {name}\n";
                    }
                }
                catch (Exception ex)
                {
                    debugOutput += $"Error creating component: {ex.Message}\n";
                    return null;
                }
            }
            else
            {
                debugOutput += $"No proxy found for component type: {name}\n";
            }

            return component;
        }

        private static void CreateConnections(GH_Document doc, JObject groupObj)
        {
            if (groupObj == null)
            {
                debugOutput += "Error: Group object is null\n";
                return;
            }

            var connections = groupObj["connections"] as JArray;
            if (connections == null)
            {
                debugOutput += "No connections found in group\n";
                return;
            }

            debugOutput += $"Creating connections in group: {groupObj["name"]}\n";

            foreach (var connection in connections)
            {
                try
                {
                    string fromPath = connection["from"]?.ToString();
                    string toPath = connection["to"]?.ToString();

                    if (string.IsNullOrEmpty(fromPath) || string.IsNullOrEmpty(toPath))
                    {
                        debugOutput += "Error: Invalid connection path\n";
                        continue;
                    }

                    // Split the paths to get component IDs and parameter names
                    var fromParts = fromPath.Split(':');
                    var toParts = toPath.Split(':');

                    if (fromParts.Length < 2 || toParts.Length < 2)
                    {
                        debugOutput += $"Error: Invalid connection format: {fromPath} -> {toPath}\n";
                        continue;
                    }

                    // Extract component IDs from the paths
                    string fromComponentId = fromParts[0].Split('/').Last();
                    string fromParamName = fromParts[1];
                    string toComponentId = toParts[0].Split('/').Last();
                    string toParamName = toParts[1];

                    debugOutput += $"Looking for components: {fromComponentId} -> {toComponentId}\n";

                    // Get the components
                    if (!createdComponents.TryGetValue(fromComponentId, out var fromComponent))
                    {
                        debugOutput += $"Failed to find source component: {fromComponentId}\n";
                        continue;
                    }

                    if (!createdComponents.TryGetValue(toComponentId, out var toComponent))
                    {
                        debugOutput += $"Failed to find target component: {toComponentId}\n";
                        continue;
                    }

                    // Get the parameters
                    IGH_Param fromParam = null;
                    IGH_Param toParam = null;

                    if (fromComponent is IGH_Component fromComp)
                    {
                        fromParam = fromComp.Params.Output.FirstOrDefault(p => p.Name == fromParamName);
                        if (fromParam == null)
                        {
                            debugOutput += $"Failed to find output parameter: {fromParamName} in component {fromComponentId}\n";
                            continue;
                        }
                    }
                    else if (fromComponent is IGH_Param fromParamOnly)
                    {
                        fromParam = fromParamOnly;
                    }

                    if (toComponent is IGH_Component toComp)
                    {
                        toParam = toComp.Params.Input.FirstOrDefault(p => p.Name == toParamName);
                        if (toParam == null)
                        {
                            debugOutput += $"Failed to find input parameter: {toParamName} in component {toComponentId}\n";
                            continue;
                        }
                    }
                    else if (toComponent is IGH_Param toParamOnly)
                    {
                        toParam = toParamOnly;
                    }

                    if (fromParam == null || toParam == null)
                    {
                        debugOutput += $"Failed to find parameters for connection: {fromPath} -> {toPath}\n";
                        continue;
                    }

                    // Create the connection
                    try
                    {
                        toParam.AddSource(fromParam);
                        toParam.ExpireSolution(false);
                        debugOutput += $"Connection created successfully: {fromPath} -> {toPath}\n";
                    }
                    catch (Exception ex)
                    {
                        debugOutput += $"Error creating connection: {ex.Message}\n";
                    }
                }
                catch (Exception ex)
                {
                    debugOutput += $"Error processing connection: {ex.Message}\n";
                }
            }
        }

        public static string GetDebugOutput()
        {
            return debugOutput;
        }

        // Helper function to calculate group depth
        private static int CalculateGroupDepth(string groupId, JObject groupObj, Dictionary<string, JObject> groupData)
        {
            int depth = 0;
            string currentId = groupId;
            
            while (true)
            {
                if (!groupData.TryGetValue(currentId, out var currentGroup))
                    break;
                    
                string parentId = currentGroup["parent"]?.ToString();
                if (string.IsNullOrEmpty(parentId) || parentId == "canvas")
                    break;
                    
                depth++;
                currentId = parentId;
            }
            
            return depth;
        }
    }
} 