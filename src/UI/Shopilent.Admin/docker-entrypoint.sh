#!/bin/sh
# Docker entrypoint script for runtime environment variable injection
# Generates a JavaScript config file from environment variables at container startup

set -e

# Generate runtime configuration JavaScript file
cat > /usr/share/nginx/html/config.js << EOF
window.ENV = {
  VITE_API_URL: "${VITE_API_URL:-http://localhost:9801/api}",
  VITE_S3_URL: "${VITE_S3_URL:-http://localhost:9858}"
};
EOF

echo "Runtime configuration generated:"
echo "  API_URL: ${VITE_API_URL:-http://localhost:9801/api}"
echo "  S3_URL: ${VITE_S3_URL:-http://localhost:9858}"

# Execute the CMD (start nginx)
exec "$@"
