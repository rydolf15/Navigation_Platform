import { createBrowserRouter } from "react-router-dom";
import { LoginPage } from "./pages/LoginPage";
import { JourneyListPage } from "./pages/JourneyListPage";
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
]);
