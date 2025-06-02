from typing import Dict, List, Any, Optional
import pinecone
from rhinomcp import get_rhino_connection
import json
import os
from dotenv import load_dotenv

# Load environment variables
load_dotenv()

class ComponentRegistry:
    def __init__(self):
        """Initialize the component registry with Pinecone."""
        # Initialize Pinecone
        pinecone.init(
            api_key=os.getenv("PINECONE_API_KEY"),
            environment=os.getenv("PINECONE_ENVIRONMENT")
        )
        
        # Create or get the index
        index_name = "grasshopper-components"
        if index_name not in pinecone.list_indexes():
            pinecone.create_index(
                name=index_name,
                dimension=1536,  # Using OpenAI's text-embedding-ada-002 model dimension
                metric="cosine"
            )
        
        self.index = pinecone.Index(index_name)
        
    def extract_component_info(self) -> List[Dict[str, Any]]:
        """Extract information about all available Grasshopper components."""
        rhino = get_rhino_connection()
        script = """
import Grasshopper as gh
import json

def get_param_info(param):
    info = {
        "name": param.Name,
        "description": param.Description if hasattr(param, "Description") else "",
        "type": param.TypeName if hasattr(param, "TypeName") else "",
        "access": str(param.Access) if hasattr(param, "Access") else "",
        "optional": param.Optional if hasattr(param, "Optional") else False
    }
    return info

components = []
for obj in gh.Instances.ComponentServer.ObjectProxies:
    try:
        comp_info = {
            "name": obj.Name,
            "description": obj.Description,
            "guid": str(obj.Guid),
            "category": obj.Category,
            "subcategory": obj.SubCategory,
            "inputs": [],
            "outputs": []
        }
        
        # Get input parameters
        if hasattr(obj, "CreateInstance"):
            instance = obj.CreateInstance()
            if hasattr(instance, "Params"):
                for param in instance.Params.Input:
                    comp_info["inputs"].append(get_param_info(param))
                for param in instance.Params.Output:
                    comp_info["outputs"].append(get_param_info(param))
        
        components.append(comp_info)
    except Exception as e:
        print(f"Error processing component {obj.Name}: {str(e)}")
        continue

print(json.dumps(components))
"""
        result = rhino.send_command("execute_rhinoscript_python_code", {"code": script})
        
        if isinstance(result, dict) and 'result' in result:
            result_str = result['result']
            if 'Print output:' in result_str:
                json_str = result_str.split('Print output:')[1].strip()
                return json.loads(json_str)
        return []
    
    def register_components(self):
        """Register all components in Pinecone."""
        components = self.extract_component_info()
        
        # Prepare vectors for Pinecone
        vectors = []
        for comp in components:
            # Create a text representation for embedding
            text = f"""
            Component: {comp['name']}
            Description: {comp['description']}
            Category: {comp['category']} > {comp['subcategory']}
            Inputs: {', '.join([f"{inp['name']} ({inp['type']})" for inp in comp['inputs']])}
            Outputs: {', '.join([f"{out['name']} ({out['type']})" for out in comp['outputs']])}
            """
            
            # Store the component info as metadata
            metadata = {
                "name": comp['name'],
                "description": comp['description'],
                "guid": comp['guid'],
                "category": comp['category'],
                "subcategory": comp['subcategory'],
                "inputs": json.dumps(comp['inputs']),
                "outputs": json.dumps(comp['outputs'])
            }
            
            # Add to vectors list
            vectors.append({
                "id": comp['guid'],
                "values": self._get_embedding(text),  # You'll need to implement this
                "metadata": metadata
            })
        
        # Upsert to Pinecone
        self.index.upsert(vectors=vectors)
    
    def search_components(self, query: str, top_k: int = 5) -> List[Dict[str, Any]]:
        """Search for components using semantic search."""
        # Get query embedding
        query_embedding = self._get_embedding(query)
        
        # Search Pinecone
        results = self.index.query(
            vector=query_embedding,
            top_k=top_k,
            include_metadata=True
        )
        
        # Process results
        components = []
        for match in results.matches:
            metadata = match.metadata
            components.append({
                "name": metadata["name"],
                "description": metadata["description"],
                "guid": metadata["guid"],
                "category": metadata["category"],
                "subcategory": metadata["subcategory"],
                "inputs": json.loads(metadata["inputs"]),
                "outputs": json.loads(metadata["outputs"]),
                "score": match.score
            })
        
        return components
    
    def _get_embedding(self, text: str) -> List[float]:
        """Get embedding for text using OpenAI's API."""
        import openai
        
        response = openai.Embedding.create(
            input=text,
            model="text-embedding-ada-002"
        )
        return response['data'][0]['embedding']

if __name__ == "__main__":
    # Initialize registry
    registry = ComponentRegistry()
    
    # Register components
    print("Registering components...")
    registry.register_components()
    
    # Test search
    print("\nTesting search...")
    results = registry.search_components("Create a point in 3D space")
    for comp in results:
        print(f"\nComponent: {comp['name']}")
        print(f"Description: {comp['description']}")
        print(f"GUID: {comp['guid']}")
        print(f"Category: {comp['category']} > {comp['subcategory']}")
        print("Inputs:")
        for inp in comp['inputs']:
            print(f"  - {inp['name']} ({inp['type']}): {inp['description']}")
        print("Outputs:")
        for out in comp['outputs']:
            print(f"  - {out['name']} ({out['type']}): {out['description']}")
        print(f"Score: {comp['score']}") 