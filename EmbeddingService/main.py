from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from typing import List, Dict, Optional, Any, Union
from sentence_transformers import SentenceTransformer
import json
import uuid
import numpy as np
from torch import cosine_similarity
import uvicorn
from sklearn.cluster import DBSCAN
from collections import defaultdict


app = FastAPI(title="Location Embedding Service")

# Load sentence transformer model
model = SentenceTransformer('all-MiniLm-L6-v2')

# Match .NET Web API models
class LocationMetadata(BaseModel):
    Latitude: Optional[float] = None
    Longitude: Optional[float] = None
    LabeledLocation: str

class VectorEmbedding(BaseModel):
    LocationID: str
    Embedding: List[float]
    Metadata: LocationMetadata

class LocationData(BaseModel):
    data: Dict[str, Any]
    id: int

@app.post("/process-locations", response_model=List[VectorEmbedding])
async def process_locations(file: UploadFile = File(...)):
    try:
        # Read and parse JSON file
        print(f"Received file: {file.filename}")
        content = await file.read()
        lines = content.decode('utf-8').strip().split('\n')
        print(f"Content first 100 chars: {lines[:100]}")
        print(f"Number of lines: {len(lines)}")
        location_entries = [json.loads(line) for line in lines]

        # Process each location and generate vector embeddings
        embeddings = []

        for entry in location_entries:
            # Get the location name to be encoded
            location_text = entry['data'].get('label', '')

            # If the location name doesn't exist for an entry, skip it
            if not location_text:
                continue

            # Generate the text embedding of the location name
            text_embedding = model.encode(location_text)

            # Check if there are latitude and longitude coordinates for this location
            if entry['data'].get('lat') is not None and entry['data'].get('lon') is not None:
                # Normalize latitude and longitude coordinates to match scale of location embeddings
                norm_lat = float(entry['data']['lat']) / 90.0 # Latitude ranges from -90 degrees to 90 degrees
                norm_lon = float(entry['data']['lon']) / 180.0 # Longitude ranges from -180 degrees to 180 degrees

                # Append the text embeddings with the coordinate embeddings
                embedding_vector = np.append(text_embedding, [norm_lat, norm_lon])
            else:
                # If there are no latitude and longitude coordinates for the location, fill in the coordinates with 0.0, 0.0
                embedding_vector = np.append(text_embedding, [0.0, 0.0])
            
            # Create the metadata of the location entry
            metadata = LocationMetadata(
                Latitude = entry['data'].get('lat'),
                Longitude = entry['data'].get('lon'),
                LabeledLocation = location_text
            )

            # Create the embedding object
            vector_embedding = VectorEmbedding(
                LocationID = str(entry['id']),
                Embedding = embedding_vector.tolist(),
                Metadata = metadata
            )

            embeddings.append(vector_embedding)

        return embeddings
    
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error processing file: {str(e)}")
    
@app.post("/process-similarity")
async def process_similarity(embeddings: List[VectorEmbedding]):
    try:
        # Convert embeddings to numpy arrays
        vectors = np.array([e.Embedding for e in embeddings])
        
        # DBSCAN used for clustering similar embeddings
        # eps - maximum distance between samples (similarity threshold)
        # min_samples - minimum number of samples in a cluster
        clustering = DBSCAN(eps=0.05, min_samples=2, metric='cosine').fit(vectors)
        
        # Group embeddings by cluster
        clusters = defaultdict(list)
        for idx, label in enumerate(clustering.labels_):
            # -1 means noise (no cluster)
            if label != -1:
                clusters[label].append({
                    'id': embeddings[idx].LocationID,
                    'location': embeddings[idx].Metadata.LabeledLocation,
                    'latitude': embeddings[idx].Metadata.Latitude,
                    'longitude': embeddings[idx].Metadata.Longitude
                })
        
        # Filtering out single-item clusters and formating the response here
        duplicate_groups = [
            {
                'group_id': f'group_{group_id}',
                'locations': locations
            }
            for group_id, locations in clusters.items()
            if len(locations) > 1
        ]
        
        return {
            'duplicate_groups': duplicate_groups,
            'total_groups': len(duplicate_groups),
            'total_duplicates': sum(len(group['locations']) for group in duplicate_groups)
        }
    except Exception as e:
        raise HTTPException(status_code=500, detail=f"Error processing similarities: {str(e)}")

@app.get("/health")
async def health_check():
    return {"status": "healthy"}

if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)