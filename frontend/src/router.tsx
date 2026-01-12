import { createBrowserRouter } from "react-router-dom";
import { LoginPage } from "./pages/LoginPage";
import { JourneyListPage } from "./pages/JourneyListPage";
import { JourneyDetailsPage } from "./pages/JourneyDetailsPage";
import { RequireAuth } from "./auth/RequireAuth";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    element: (
      <RequireAuth>
        <JourneyListPage />
      </RequireAuth>
    ),
  },
  {
    path: "/journeys/:id",
    element: (
      <RequireAuth>
        <JourneyDetailsPage />
      </RequireAuth>
    ),
  },
]);
