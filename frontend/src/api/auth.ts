export const login = () => {
  window.location.href = "/api/auth/login";
};

export const logout = async () => {
  await fetch("/api/auth/logout", {
    method: "POST",
    credentials: "include",
  });

  window.location.href = "/";
};
