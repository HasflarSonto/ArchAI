using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using Grasshopper.Kernel;
using Grasshopper.Kernel.Data;
using Grasshopper.Kernel.Types;
using Grasshopper.Kernel.Special;
using GH_IO.Serialization;
using GH_IO.Types;
using System.Drawing;

namespace GHUI.Classes
{
    public class GHFileLoader
    {
        private static string debugOutput = "";

        public static void LoadGHFile(GH_Document doc, string ghFilePath)
        {
            debugOutput = ""; // Reset debug output
            try
            {
                if (doc == null)
                {
                    debugOutput += "Error: Document is null\n";
                    return;
                }

                debugOutput += $"Starting to load from {ghFilePath}\n";
                
                if (string.IsNullOrEmpty(ghFilePath))
                {
                    debugOutput += "Error: File path is null or empty\n";
                    return;
                }

                if (!File.Exists(ghFilePath))
                {
                    debugOutput += $"Error: File not found at {ghFilePath}\n";
                    return;
                }

                // Get the file name without extension for the group name
                string groupName = Path.GetFileNameWithoutExtension(ghFilePath);
                debugOutput += $"Will create group named: {groupName}\n";

                debugOutput += "File exists, creating new document for conversion...\n";
                
                // Try using GH_DocumentIO directly first
                debugOutput += "Attempting to load using GH_DocumentIO...\n";
                var docIO = new GH_DocumentIO();
                if (docIO.Open(ghFilePath))
                {
                    debugOutput += "Successfully opened file with GH_DocumentIO\n";
                    
                    try
                    {
                        // Get the loaded document
                        var loadedDoc = docIO.Document;
                        if (loadedDoc == null)
                        {
                            debugOutput += "Error: Loaded document is null\n";
                            return;
                        }

                        debugOutput += $"Loaded document has {loadedDoc.ObjectCount} objects\n";

                        // Create a list to store all objects before adding them
                        var objectsToAdd = new List<IGH_DocumentObject>();
                        
                        // Collect all objects first
                        foreach (var obj in loadedDoc.Objects)
                        {
                            if (obj == null) continue;
                            objectsToAdd.Add(obj);
                        }

                        // Create the group first
                        debugOutput += $"Creating group '{groupName}'...\n";
                        var group = new GH_Group();
                        group.NickName = groupName;
                        group.CreateAttributes();
                        
                        // Set group color to a nice blue color
                        group.Colour = Color.FromArgb(100, 100, 149, 237); // LightSkyBlue with transparency
                        debugOutput += "Set group color to LightSkyBlue with transparency\n";
                        
                        // Add the group to the document
                        doc.AddObject(group, false);
                        
                        // Now add all objects
                        foreach (var obj in objectsToAdd)
                        {
                            try
                            {
                                debugOutput += $"Adding object: {obj.Name}\n";
                                doc.AddObject(obj, false);
                                
                                // Add object to group using its InstanceGuid
                                if (obj.InstanceGuid != Guid.Empty)
                                {
                                    group.AddObject(obj.InstanceGuid);
                                    debugOutput += $"Added object {obj.Name} to group\n";
                                }
                            }
                            catch (Exception ex)
                            {
                                debugOutput += $"Warning: Failed to add object {obj.Name}: {ex.Message}\n";
                            }
                        }

                        // Schedule a solution refresh
                        debugOutput += "Scheduling solution refresh...\n";
                        doc.ScheduleSolution(1, d => {
                            debugOutput += "Document refresh scheduled\n";
                        });

                        debugOutput += "GH file loading completed successfully\n";
                        return;
                    }
                    catch (Exception ex)
                    {
                        debugOutput += $"Error during direct object copying: {ex.Message}\n";
                        debugOutput += "Falling back to clipboard method...\n";
                        
                        // Fall back to clipboard method
                        try
                        {
                            // Create the group first
                            debugOutput += $"Creating group '{groupName}'...\n";
                            var group = new GH_Group();
                            group.NickName = groupName;
                            group.CreateAttributes();
                            
                            // Set group color to a nice blue color
                            group.Colour = Color.FromArgb(180, 135, 206, 250); // LightSkyBlue with transparency
                            debugOutput += "Set group color to LightSkyBlue with transparency\n";
                            
                            doc.AddObject(group, false);

                            // Create a new document IO for the target document
                            var targetDocIO = new GH_DocumentIO(doc);
                            
                            // Copy to clipboard
                            debugOutput += "Copying to clipboard...\n";
                            docIO.Copy(GH_ClipboardType.Local);
                            
                            // Paste from clipboard
                            debugOutput += "Pasting from clipboard...\n";
                            if (!targetDocIO.Paste(GH_ClipboardType.Local))
                            {
                                debugOutput += "Error: Failed to paste content\n";
                                return;
                            }

                            // Add all pasted objects to the group
                            var pastedObjects = doc.Objects.ToList();
                            foreach (var obj in pastedObjects)
                            {
                                if (obj != null && obj.InstanceGuid != Guid.Empty)
                                {
                                    group.AddObject(obj.InstanceGuid);
                                    debugOutput += $"Added object {obj.Name} to group\n";
                                }
                            }

                            debugOutput += "Successfully pasted content into document\n";

                            // Schedule a solution refresh
                            debugOutput += "Scheduling solution refresh...\n";
                            doc.ScheduleSolution(1, d => {
                                debugOutput += "Document refresh scheduled\n";
                            });

                            debugOutput += "GH file loading completed successfully\n";
                            return;
                        }
                        catch (Exception clipboardEx)
                        {
                            debugOutput += $"Error during clipboard operations: {clipboardEx.Message}\n";
                            return;
                        }
                    }
                }
                
                debugOutput += "GH_DocumentIO failed, trying alternative method...\n";
                
                // If GH_DocumentIO fails, try the archive method
                var archive = new GH_Archive();
                debugOutput += "Attempting to read file with archive...\n";
                
                if (!archive.ReadFromFile(ghFilePath))
                {
                    debugOutput += "Error: Failed to read GH file\n";
                    return;
                }

                debugOutput += "File read successfully, getting root node...\n";
                var root = archive.GetRootNode;
                if (root == null)
                {
                    debugOutput += "Error: Failed to get root node from archive\n";
                    return;
                }

                debugOutput += $"Root node name: {root.Name}\n";
                
                // Analyze file structure
                debugOutput += "Analyzing file structure...\n";
                try
                {
                    debugOutput += "Document structure:\n";
                    foreach (var item in root.Items)
                    {
                        debugOutput += $"Item: {item.Name}, Type: {item.Type}\n";
                    }
                }
                catch (Exception ex)
                {
                    debugOutput += $"Warning: Could not analyze document structure: {ex.Message}\n";
                }
                
                debugOutput += "Attempting to read document from archive...\n";
                try
                {
                    // Create the group first
                    debugOutput += $"Creating group '{groupName}'...\n";
                    var group = new GH_Group();
                    group.NickName = groupName;
                    group.CreateAttributes();
                    
                    // Set group color to a nice blue color
                    group.Colour = Color.FromArgb(180, 135, 206, 250); // LightSkyBlue with transparency
                    debugOutput += "Set group color to LightSkyBlue with transparency\n";
                    
                    doc.AddObject(group, false);

                    // Create a new document
                    var newDoc = new GH_Document();
                    
                    // Try to read without version check
                    if (!newDoc.Read(root))
                    {
                        debugOutput += "Error: Failed to read document from archive\n";
                        return;
                    }

                    debugOutput += $"Successfully loaded GH file with {newDoc.ObjectCount} objects\n";

                    // Create a new document IO for clipboard operations
                    debugOutput += "Creating document IO for clipboard operations...\n";
                    var newDocIO = new GH_DocumentIO(newDoc);
                    
                    // Copy to clipboard
                    debugOutput += "Copying to clipboard...\n";
                    newDocIO.Copy(GH_ClipboardType.Local);
                    
                    // Create a new document IO for the target document
                    var targetDocIO = new GH_DocumentIO(doc);
                    
                    // Paste from clipboard
                    debugOutput += "Pasting from clipboard...\n";
                    if (!targetDocIO.Paste(GH_ClipboardType.Local))
                    {
                        debugOutput += "Error: Failed to paste content\n";
                        return;
                    }

                    // Add all pasted objects to the group
                    var pastedObjects = doc.Objects.ToList();
                    foreach (var obj in pastedObjects)
                    {
                        if (obj != null && obj.InstanceGuid != Guid.Empty)
                        {
                            group.AddObject(obj.InstanceGuid);
                            debugOutput += $"Added object {obj.Name} to group\n";
                        }
                    }

                    debugOutput += "Successfully pasted content into document\n";

                    // Schedule a solution refresh
                    debugOutput += "Scheduling solution refresh...\n";
                    doc.ScheduleSolution(1, d => {
                        debugOutput += "Document refresh scheduled\n";
                    });

                    debugOutput += "GH file loading completed successfully\n";
                }
                catch (Exception ex)
                {
                    debugOutput += $"Error processing document: {ex.Message}\n";
                    debugOutput += $"Stack trace: {ex.StackTrace}\n";
                    return;
                }
            }
            catch (Exception ex)
            {
                debugOutput += $"Error loading GH file: {ex.Message}\nStack trace: {ex.StackTrace}\n";
                throw;
            }
        }

        public static string GetDebugOutput()
        {
            return debugOutput;
        }
    }
} 