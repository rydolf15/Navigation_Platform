export const login = () => {
  window.location.href = "/api/auth/login";
};

export const logout = async () => {
  try {
    await fetch("/api/auth/logout", {
      method: "POST",
      credentials: "include",
    });
  } catch {
    // ignore
  } finally {
    // Always route back to the sign-in screen.
    window.location.href = "/login";
  }
};
