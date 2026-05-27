export const API_BASE_URL =
  "https://marketagent-api-d6cqe0bncfhyhmh6.eastus-01.azurewebsites.net";

const API_PATH_PREFIX = "/api";

export const buildApiUrl = (path: string) => {
  const normalizedPath = path.startsWith("/") ? path.slice(1) : path;

  return `${API_BASE_URL}${API_PATH_PREFIX}/${normalizedPath}`;
};
