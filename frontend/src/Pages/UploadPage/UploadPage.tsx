import React, { useState, ChangeEvent } from 'react';
import { Upload, Loader, CheckCircle, AlertCircle, Globe } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';

// Fix for default marker icons in React-Leaflet
delete (L.Icon.Default.prototype as any)._getIconUrl;
L.Icon.Default.mergeOptions({
  iconRetinaUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon-2x.png',
  iconUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-icon.png',
  shadowUrl: 'https://cdnjs.cloudflare.com/ajax/libs/leaflet/1.7.1/images/marker-shadow.png',
});

interface MapData {
  original: Array<{
    latitude: number;
    longitude: number;
    name: string;
  }>;
  deduplicated: Array<{
    latitude: number;
    longitude: number;
    name: string;
  }>;
}

interface CustomAlertProps {
  message: string;
}

const CustomAlert: React.FC<CustomAlertProps> = ({ message }) => (
  <div className="bg-red-100 border border-red-400 text-red-700 px-4 py-3 rounded relative">
    <div className="flex items-center">
      <AlertCircle className="h-4 w-4 mr-2" />
      <span>{message}</span>
    </div>
  </div>
);

const MapComponent: React.FC<{ locations: Array<{ latitude: number; longitude: number; name: string }> }> = ({ locations }) => {
    return (
      <MapContainer
        center={[20, 0]}
        zoom={2}
        style={{ height: '100%', width: '100%', borderRadius: '0.5rem' }}
      >
        <TileLayer
          url="https://{s}.tile.openstreetmap.org/{z}/{x}/{y}.png"
          attribution='&copy; <a href="https://www.openstreetmap.org/copyright">OpenStreetMap</a> contributors'
        />
        {locations.map((location, index) => (
          <Marker
            key={index}
            position={[location.latitude, location.longitude]}
          >
            <Popup>
              {location.name}
            </Popup>
          </Marker>
        ))}
      </MapContainer>
    );
  };

const UploadPage = () => {
  const [file, setFile] = useState<File | null>(null);
  const [uploadStatus, setUploadStatus] = useState<'idle' | 'uploading' | 'processing' | 'complete' | 'error'>('idle');
  const [errorMessage, setErrorMessage] = useState<string>('');
  const [mapData, setMapData] = useState<MapData>({
    original: [],
    deduplicated: []
  });
  const [activeView, setActiveView] = useState<'original' | 'deduplicated'>('original');

  const handleFileSelect = (event: ChangeEvent<HTMLInputElement>) => {
    const selectedFile = event.target.files?.[0];
    if (selectedFile) {
      if (!selectedFile.name.toLowerCase().endsWith('.json')) {
        setErrorMessage('Please upload a JSON file');
        setUploadStatus('error');
        return;
      }
      setFile(selectedFile);
      setUploadStatus('idle');
      setErrorMessage('');
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    try {
      setUploadStatus('uploading');
      const formData = new FormData();
      formData.append('file', file);

      // Upload and process the file
      const uploadResponse = await fetch('/api/upload', {
        method: 'POST',
        body: formData
      });

      if (!uploadResponse.ok) throw new Error('Upload failed');
      
      const { sessionId } = await uploadResponse.json();
      setUploadStatus('processing');

      // Get deduplication results
      const resultsResponse = await fetch(`/api/deduplication/${sessionId}`);
      if (!resultsResponse.ok) throw new Error('Deduplication failed');

      const results = await resultsResponse.json();
      setMapData({
        original: results.originalLocations,
        deduplicated: results.deduplicatedLocations
      });
      setUploadStatus('complete');

    } catch (error: unknown) {
      setUploadStatus('error');
      if (error instanceof Error) {
        setErrorMessage(error.message);
      } else {
        setErrorMessage('An unexpected error occurred');
      }
    }
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header Section */}
      <section className="bg-gradient-to-br from-emerald-600 to-teal-700 text-white py-12">
        <div className="container mx-auto px-4">
          <h1 className="text-4xl font-bold mb-4">Upload Your Dataset</h1>
          <p className="text-xl">Start cleaning and deduplicating your geographical data</p>
        </div>
      </section>

      <div className="container mx-auto px-4 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-2 gap-8">
          {/* Upload Section */}
          <div className="bg-white rounded-xl shadow-sm p-8">
            <h2 className="text-2xl font-semibold mb-6 text-gray-800">Dataset Upload</h2>
            
            <div className="border-2 border-dashed border-gray-300 rounded-lg p-8 text-center">
              {uploadStatus === 'idle' && (
                <>
                  <Upload className="mx-auto h-12 w-12 text-gray-400 mb-4" />
                  <label className="block">
                    <span className="sr-only">Choose file</span>
                    <input 
                      type="file" 
                      className="block w-full text-sm text-gray-500
                        file:mr-4 file:py-2 file:px-4
                        file:rounded-full file:border-0
                        file:text-sm file:font-semibold
                        file:bg-emerald-50 file:text-emerald-700
                        hover:file:bg-emerald-100"
                      accept=".json"
                      onChange={handleFileSelect}
                    />
                  </label>
                  <p className="mt-2 text-sm text-gray-500">Please upload a JSON file containing location data</p>
                  {file && (
                    <button
                      onClick={handleUpload}
                      className="mt-4 bg-emerald-600 text-white py-2 px-6 rounded-lg hover:bg-emerald-700 transition duration-300"
                    >
                      Start Processing
                    </button>
                  )}
                </>
              )}

              {uploadStatus === 'uploading' && (
                <div className="text-center">
                  <Loader className="mx-auto h-12 w-12 text-emerald-600 animate-spin mb-4" />
                  <p className="text-gray-600">Uploading your dataset...</p>
                </div>
              )}

              {uploadStatus === 'processing' && (
                <div className="text-center">
                  <Loader className="mx-auto h-12 w-12 text-emerald-600 animate-spin mb-4" />
                  <p className="text-gray-600">Processing locations...</p>
                </div>
              )}

              {uploadStatus === 'complete' && (
                <div className="text-center">
                  <CheckCircle className="mx-auto h-12 w-12 text-green-600 mb-4" />
                  <p className="text-gray-600">Processing complete!</p>
                </div>
              )}

              {uploadStatus === 'error' && (
                <CustomAlert message={errorMessage || 'An error occurred during processing'} />
              )}
            </div>

            {uploadStatus === 'complete' && (
              <div className="mt-6">
                <div className="flex gap-4 mb-4">
                  <button
                    onClick={() => setActiveView('original')}
                    className={`flex-1 py-2 px-4 rounded-lg transition duration-300 ${
                      activeView === 'original'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                    }`}
                  >
                    Original Data
                  </button>
                  <button
                    onClick={() => setActiveView('deduplicated')}
                    className={`flex-1 py-2 px-4 rounded-lg transition duration-300 ${
                      activeView === 'deduplicated'
                        ? 'bg-emerald-600 text-white'
                        : 'bg-gray-100 text-gray-600 hover:bg-gray-200'
                    }`}
                  >
                    Deduplicated Data
                  </button>
                </div>
              </div>
            )}
          </div>

           {/* Map Section */}
           <div className="bg-white rounded-xl shadow-sm p-8">
            <h2 className="text-2xl font-semibold mb-6 text-gray-800">Location Visualization</h2>
            <div className="aspect-square rounded-lg bg-gray-100 overflow-hidden">
              <div className="w-full h-full">
                <MapComponent 
                  locations={activeView === 'original' ? mapData.original : mapData.deduplicated} 
                />
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default UploadPage;