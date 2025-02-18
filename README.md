# GeoConsolidate

GeoConsolidate is a web application capable of deduplicating geographical data to give a consolidated view of geographical locations. Due to the wide range of data sources on geographical locations, there occurs some level of duplication in the location data. GeoConsolidate can take a JSON file as input and return a deduplicated version of that JSON file. GeoConsolidate is built with a .NET Web API and Fast API backend, a React JS frontend, and a Redis database to store vector embeddings of the location data. 

## How to Run the Web App
### Prerequisites
- Git installed
- Node.js and npm installed (for React frontend)
- .NET 8 SDK installed
- Redis installed (instructions below)
- A code editor (e.g. Visual Studio Code)
- Clone the Repository
    - `git clone https://github.com/reynoldsbrent/GeoConsolidate.git`

### Setup Instructions (Frontend)

1. Setup the Frontend
    - Change directories to the `frontend` folder
1. Install npm Packages
    - You can run `npm install` in VS Code
1. Start the React App
    - Run `npm start`

The frontend should now be running.

### Setup Instructions (.NET Web API)
1. Setup the .NET Web API
    - Change directories to the backend API using `cd WebAPI/WebAPI`
1. Restore NuGet Packages
    - If you are in VS Code you can use `dotnet restore`
    - If you are in Visual Studio, the NuGet packages are automatically restored
1. Run the Backend
    - If you are in VS Code, you can use `dotnet watch run`
    - If you are using Visual Studio, you can use the green play button to start the API

The .NET Web API should now be running.

### Setup Instructions (Fast API)
1. Set up the Fast API

### Setup Instructions (Redis)
1. Install Redis For Windows
    - Install Windows Subsystem for Linux
    - Create your Unix username
    - Open the Windows Subsystem for Linux terminal and install Redis
    - Type in `sudo apt update`
    - Once that is finished running, type in `sudo apt install redis-server`
    - Once that completes, type in `sudo service redis-server start`
    - Redis should now be started
    - To check if Redis is running, type in `redis-cli ping`
    - `pong` should be returned
    