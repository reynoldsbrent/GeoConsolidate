from fastapi import FastAPI, UploadFile, File, HTTPException
from fastapi.responses import JSONResponse
from pydantic import BaseModel
from typing import List, Dict, Optional, Any, Union
from sentence_transformers import SentenceTransformer
import json
import uuid
import numpy as np
import uvicorn


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
        content = await file.read()
        lines = content.decode('utf-8').strip.split('\n')
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

                # Append the coordinate embeddings with the latitude embedding
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

@app.get("/health")
async def health_check():
    return {"status": "healthy"}

if __name__ == "__main__":
    uvicorn.run("main:app", host="0.0.0.0", port=8000, reload=True)