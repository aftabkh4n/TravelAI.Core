#!/usr/bin/env bash
# =============================================================================
# local-docker-test.sh
# Builds and runs the Docker image locally to verify it starts correctly.
# Run from the project ROOT folder (TravelAI.Core/).
#
# Usage: bash deploy/scripts/local-docker-test.sh
# =============================================================================
set -euo pipefail

IMAGE="travelai-api:local"

echo "Building Docker image..."
docker build \
  -f deploy/Dockerfile \
  -t "$IMAGE" \
  .

echo ""
echo "Starting container on http://localhost:8080 ..."
echo "Press Ctrl+C to stop."
echo ""

docker run --rm -it \
  -p 8080:8080 \
  -e "TravelAI__AzureOpenAI__Endpoint=https://placeholder.openai.azure.com/" \
  -e "TravelAI__AzureOpenAI__ApiKey=placeholder" \
  -e "TravelAI__AzureSearch__Endpoint=https://placeholder.search.windows.net" \
  -e "TravelAI__AzureSearch__ApiKey=placeholder" \
  -e "TravelAI__ItineraryGeneration__DeploymentName=gpt-4o" \
  -e "TravelAI__DestinationSearch__IndexName=destinations" \
  -e "ASPNETCORE_ENVIRONMENT=Development" \
  "$IMAGE"

# In a second terminal while this is running, test with:
#   curl http://localhost:8080/health/live
#   curl http://localhost:8080/health/ready
