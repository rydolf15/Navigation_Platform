import { createBrowserRouter } from "react-router-dom";
import { LoginPage } from "./pages/LoginPage";
import { JourneyListPage } from "./pages/JourneyListPage";
import { JourneyDetailsPage } from "./pages/JourneyDetailsPage";
import { RequireAuth } from "./auth/RequireAuth";
import { LandingPage } from "./pages/LandingPage";
import { AdminPage } from "./pages/AdminPage";
import { RequireAdmin } from "./auth/RequireAdmin";

export const router = createBrowserRouter([
  { path: "/login", element: <LoginPage /> },
  {
    path: "/",
    element: (
      <RequireAuth>
        <LandingPage />
      </RequireAuth>
    ),
  },
  {
    path: "/journeys",
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
  {
    path: "/admin",
    element: (
      <RequireAdmin>
        <AdminPage />
      </RequireAdmin>
    ),
  },
]);
