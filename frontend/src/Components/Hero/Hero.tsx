import React from 'react';
import { Globe, Database, Upload, MapPin } from 'lucide-react';
import { Link } from 'react-router-dom';

const Hero = () => {
  return (
    <div className="font-sans">
      {/* Hero Section */}
      <section className="bg-gradient-to-br from-emerald-600 to-teal-700 text-white py-20">
        <div className="container mx-auto px-4">
          <div className="flex flex-col md:flex-row items-center justify-between">
            <div className="md:w-1/2 mb-8 md:mb-0">
              <h1 className="text-5xl font-bold mb-4">Unify Your Geographic Data</h1>
              <p className="text-xl mb-8">Transform messy location data into clean, standardized datasets with intelligent geographical deduplication.</p>
              <div className="flex flex-wrap gap-4">
                <Link to="/upload" className="bg-white text-emerald-700 py-2 px-6 rounded-lg text-lg font-semibold hover:bg-emerald-50 transition duration-300 flex items-center">
                  <Upload className="mr-2 h-5 w-5" />
                  Get Started Now
                </Link>
                <button className="border-2 border-white text-white py-2 px-6 rounded-lg text-lg font-semibold hover:bg-white/10 transition duration-300">
                  Learn More
                </button>
              </div>
            </div>
            <div className="md:w-1/2 flex justify-center">
              <div className="relative">
                <Globe className="h-64 w-64 text-white/90" />
                <div className="absolute top-1/2 left-1/2 transform -translate-x-1/2 -translate-y-1/2">
                  <MapPin className="h-16 w-16 text-emerald-300" />
                </div>
              </div>
            </div>
          </div>
        </div>
      </section>

      {/* Features Section */}
      <section className="py-20 bg-gray-50">
        <div className="container mx-auto px-4">
          <h2 className="text-3xl font-bold mb-12 text-center text-gray-800">Why Choose GeoConsolidate?</h2>
          <div className="grid grid-cols-1 md:grid-cols-3 gap-8">
            {features.map((feature, index) => (
              <div key={index} className="bg-white p-8 rounded-xl shadow-sm hover:shadow-md transition duration-300">
                <div className="text-emerald-600 mb-4">
                  {feature.icon}
                </div>
                <h3 className="text-xl font-semibold mb-4 text-gray-800">{feature.title}</h3>
                <p className="text-gray-600">{feature.description}</p>
              </div>
            ))}
          </div>
        </div>
      </section>

      {/* CTA Section */}
      <section className="bg-emerald-700 text-white py-16">
        <div className="container mx-auto px-4 text-center">
          <h2 className="text-3xl font-bold mb-6">Ready to Clean Your Location Data?</h2>
          <p className="text-xl mb-8 max-w-2xl mx-auto">Use GeoConsolidate to standardize and deduplicate your geographical datasets.</p>
          <Link to="/upload" className="bg-white text-emerald-700 py-3 px-8 rounded-lg text-lg font-semibold hover:bg-emerald-50 transition duration-300">
            Get Started Now
          </Link>
        </div>
      </section>
    </div>
  );
};

const features = [
  {
    icon: <Database className="h-12 w-12" />,
    title: "Intelligent Deduplication",
    description: "Advanced algorithms identify and merge duplicate locations within your datasets, maintaining data integrity while eliminating redundancy."
  },
  {
    icon: <Globe className="h-12 w-12" />,
    title: "Smart Matching",
    description: "Advanced pattern matching algorithms using vector embeddings to identify similar locations even when they're written differently, ensuring accurate consolidation."
  },
  {
    icon: <Upload className="h-12 w-12" />,
    title: "Easy Integration",
    description: "Simply upload your dataset and let GeoConsolidate handle the rest. Compatible with CSV, Excel, and other common file formats."
  }
];

export default Hero;