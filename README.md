# GeoConsolidate

GeoConsolidate is a web application capable of deduplication geographical data to give a consolidated view of geographical locations. Due to the wide range of data sources on geographical locations, there occurs some level of duplication in the location data. GeoConsolidate can take a JSON file as input and return a deduplicated version of that JSON file. GeoConsolidate is built with a .NET Web API and Fast API backend, a React JS frontend, and a Redis database to store vector embeddings of the location data.