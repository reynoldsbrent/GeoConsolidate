import React, { useState, ChangeEvent } from 'react';
import { Upload, Loader, CheckCircle, AlertCircle, Globe, Download } from 'lucide-react';
import { MapContainer, TileLayer, Marker, Popup } from 'react-leaflet';
import 'leaflet/dist/leaflet.css';
import L from 'leaflet';

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

interface DeduplicationStats {
  originalCount: number;
  deduplicatedCount: number;
  removedCount: number;
}

interface CustomAlertProps {
  message: string;
  type?: 'error' | 'success' | 'info';
}

const CustomAlert: React.FC<CustomAlertProps> = ({ message, type = 'error' }) => {
  const bgColor = type === 'error' ? 'bg-red-100' : type === 'success' ? 'bg-green-100' : 'bg-blue-100';
  const textColor = type === 'error' ? 'text-red-700' : type === 'success' ? 'text-green-700' : 'text-blue-700';
  const borderColor = type === 'error' ? 'border-red-400' : type === 'success' ? 'border-green-400' : 'border-blue-400';
  const Icon = type === 'error' ? AlertCircle : type === 'success' ? CheckCircle : Globe;

  return (
    <div className={`${bgColor} border ${borderColor} ${textColor} px-4 py-3 rounded relative`}>
      <div className="flex items-center">
        <Icon className="h-4 w-4 mr-2" />
        <span>{message}</span>
      </div>
    </div>
  );
};

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
  const [successMessage, setSuccessMessage] = useState<string>('');
  const [sessionId, setSessionId] = useState<string>('');
  const [mapData, setMapData] = useState<MapData>({
    original: [],
    deduplicated: []
  });
  const [activeView, setActiveView] = useState<'original' | 'deduplicated'>('original');
  const [deduplicationStats, setDeduplicationStats] = useState<DeduplicationStats | null>(null);

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
      setSuccessMessage('');
    }
  };

  const handleUpload = async () => {
    if (!file) return;

    try {
      setUploadStatus('uploading');
      const formData = new FormData();
      formData.append('file', file);

      // Upload the file
      const uploadResponse = await fetch('https://localhost:7189/api/Upload', {
        method: 'POST',
        body: formData
      });

      if (!uploadResponse.ok) throw new Error('Upload failed');
      
      const { sessionId } = await uploadResponse.json();
      setSessionId(sessionId);
      setUploadStatus('processing');

      // Process deduplication
      await processDuplicates(sessionId);

      // Get the visualization data
      await getVisualizationData(sessionId);

      setUploadStatus('complete');
      setSuccessMessage('File processed successfully!');

    } catch (error: unknown) {
      setUploadStatus('error');
      if (error instanceof Error) {
        setErrorMessage(error.message);
      } else {
        setErrorMessage('An unexpected error occurred');
      }
    }
  };

  const processDuplicates = async (sessionId: string) => {
    try {
      // Start deduplication process
      const deduplicateResponse = await fetch(`https://localhost:7189/api/Upload/deduplicate/${sessionId}`, {
        method: 'POST'
      });
      
      if (!deduplicateResponse.ok) throw new Error('Deduplication failed');
      
      const deduplicationResult = await deduplicateResponse.json();
      
      // Store deduplication stats
      setDeduplicationStats({
        originalCount: deduplicationResult.original_count || 0,
        deduplicatedCount: deduplicationResult.deduplicated_count || 0,
        removedCount: deduplicationResult.removed_count || 0
      });
      
      return deduplicationResult;
    } catch (error) {
      console.error('Error during deduplication:', error);
      throw error;
    }
  };

  const getVisualizationData = async (sessionId: string) => {
    try {
      // Get original and deduplicated data from the Deduplication controller
      const response = await fetch(`https://localhost:7189/api/Deduplication/${sessionId}`);
      if (!response.ok) throw new Error('Failed to get location data');
      
      const data = await response.json();
      
      // Use both datasets from the response for the map data
      setMapData({
        original: data.originalLocations || [],
        deduplicated: data.deduplicatedLocations || []
      });
      
    } catch (error) {
      console.error('Error getting visualization data:', error);
      throw error;
    }
  };

  const handleDownload = () => {
    if (!sessionId) return;
    
    // Create download link to the deduplicated file
    const downloadUrl = `https://localhost:7189/api/Upload/deduplicated/${sessionId}`;
    
    const a = document.createElement('a');
    a.href = downloadUrl;
    a.download = `deduplicated_locations.json`;
    document.body.appendChild(a);
    a.click();
    document.body.removeChild(a);
  };

  return (
    <div className="min-h-screen bg-gray-50">
      {/* Header Section */}
      <section className="bg-gradient-to-br from-emerald-600 to-teal-700 text-white py-12">
        <div className="container mx-auto px-4">
          <h1 className="text-4xl font-bold mb-4">GeoConsolidate</h1>
          <p className="text-xl">Upload, clean, and deduplicate your geographical location data</p>
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
                  <p className="text-gray-600">Processing and deduplicating locations...</p>
                </div>
              )}

              {uploadStatus === 'complete' && (
                <div className="text-center">
                  <CheckCircle className="mx-auto h-12 w-12 text-green-600 mb-4" />
                  <p className="text-gray-600">{successMessage}</p>
                  {deduplicationStats && (
                    <div className="mt-4 text-left bg-gray-50 p-4 rounded-lg">
                      <h3 className="text-lg font-semibold mb-2">Deduplication Results:</h3>
                      <ul className="text-sm text-gray-700">
                        <li className="py-1">Original locations: {deduplicationStats.originalCount}</li>
                        <li className="py-1">Deduplicated locations: {deduplicationStats.deduplicatedCount}</li>
                        <li className="py-1">Duplicates removed: {deduplicationStats.removedCount}</li>
                      </ul>
                      <button
                        onClick={handleDownload}
                        className="mt-4 bg-blue-600 text-white py-2 px-6 rounded-lg hover:bg-blue-700 transition duration-300 flex items-center justify-center w-full"
                      >
                        <Download className="h-4 w-4 mr-2" />
                        Download Deduplicated Data
                      </button>
                    </div>
                  )}
                </div>
              )}

              {uploadStatus === 'error' && (
                <CustomAlert message={errorMessage || 'An error occurred during processing'} type="error" />
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
                {mapData && (activeView === 'original' ? mapData.original.length > 0 : mapData.deduplicated.length > 0) ? (
                  <MapComponent 
                    locations={activeView === 'original' ? mapData.original : mapData.deduplicated} 
                  />
                ) : (
                  <div className="w-full h-full flex items-center justify-center">
                    <p className="text-gray-500">Upload a file to visualize locations</p>
                  </div>
                )}
              </div>
            </div>
          </div>
        </div>
      </div>
    </div>
  );
};

export default UploadPage;