#!/bin/sh
# Docker entrypoint script for runtime environment variable injection
# Generates a JavaScript config file from environment variables at container startup

set -e

# Generate runtime configuration JavaScript file
cat > /usr/share/nginx/html/config.js << EOF
window.ENV = {
  VITE_API_URL: "${VITE_API_URL:-http://localhost:9801/api}"
};
EOF

echo "Runtime configuration generated:"
echo "  API_URL: ${VITE_API_URL:-http://localhost:9801/api}"

# Execute the CMD (start nginx)
exec "$@"
