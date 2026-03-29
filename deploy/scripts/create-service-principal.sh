#!/usr/bin/env bash
# =============================================================================
# create-service-principal.sh
# Creates an Azure Service Principal for GitHub Actions authentication.
# Run ONCE after provisioning, before your first push.
#
# Usage: bash deploy/scripts/create-service-principal.sh
# =============================================================================
set -euo pipefail

SUBSCRIPTION_ID="YOUR_SUBSCRIPTION_ID"   # same value as provision-azure.sh
RESOURCE_GROUP="rg-travelai-prod"
SP_NAME="sp-travelai-github-actions"

echo "Creating service principal: $SP_NAME..."

AZURE_CREDENTIALS=$(az ad sp create-for-rbac \
  --name "$SP_NAME" \
  --role Contributor \
  --scopes "/subscriptions/${SUBSCRIPTION_ID}/resourceGroups/${RESOURCE_GROUP}" \
  --sdk-auth)

echo ""
echo "════════════════════════════════════════════════════════════════"
echo "  Add this JSON as GitHub Secret named: AZURE_CREDENTIALS"
echo "  Repo -> Settings -> Secrets -> Actions -> New repository secret"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "$AZURE_CREDENTIALS"
echo ""
echo "  Do NOT commit this JSON to the repository."
