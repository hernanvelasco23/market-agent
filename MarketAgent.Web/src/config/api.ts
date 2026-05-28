export const API_BASE_URL =
  import.meta.env.VITE_API_BASE_URL || "http://localhost:5215";
const API_PATH_PREFIX = "/api";

export const buildApiUrl = (path: string) => {
  const normalizedPath = path.startsWith("/") ? path.slice(1) : path;

  return `${API_BASE_URL}${API_PATH_PREFIX}/${normalizedPath}`;
};
