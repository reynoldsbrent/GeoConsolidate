import { createBrowserRouter } from "react-router-dom";
import App from "../App";
import HomePage from "../Pages/HomePage/HomePage";
import UploadPage from "../Pages/UploadPage/UploadPage";

export const router = createBrowserRouter([
    {
      path: "/",
      element: <App />,
      children: [
        {path: "", element: <HomePage />},
        {path: "upload", element: <UploadPage />},
      ],
    },
  ]);