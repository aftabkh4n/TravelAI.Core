#!/usr/bin/env bash
# =============================================================================
# provision-azure.sh
# Creates ALL Azure resources needed for TravelAI from scratch.
# Run once before your first deployment.
#
# Prerequisites: Azure CLI installed and logged in (az login)
# Usage:         bash deploy/scripts/provision-azure.sh
# =============================================================================
set -euo pipefail

# ── EDIT THESE BEFORE RUNNING ─────────────────────────────────────────────────
SUBSCRIPTION_ID="97644c3d-cea2-4a4a-b956-22148576051b"         # az account show --query id -o tsv
LOCATION="uksouth"
RESOURCE_GROUP="rg-travelai-prod"

AKS_CLUSTER="aks-travelai-prod"
AKS_NODE_COUNT=2
AKS_NODE_VM="Standard_D2pds_v6"

ACR_NAME="aftabkh4ntravelaiprod"                     # globally unique, lowercase only

OPENAI_NAME="openai-travelai-prod"
OPENAI_DEPLOYMENT="gpt-4o"
OPENAI_CAPACITY=10

SEARCH_NAME="search-travelai-prod"
SEARCH_SKU="basic"
SEARCH_INDEX="destinations"
# ─────────────────────────────────────────────────────────────────────────────

echo "Setting subscription..."
az account set --subscription "$SUBSCRIPTION_ID"

echo "Creating resource group: $RESOURCE_GROUP..."
az group create --name "$RESOURCE_GROUP" --location "$LOCATION"

echo "Creating Azure Container Registry: $ACR_NAME..."
az acr create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$ACR_NAME" \
  --sku Basic \
  --admin-enabled false

ACR_LOGIN_SERVER=$(az acr show --name "$ACR_NAME" --query loginServer -o tsv)

echo "Creating AKS cluster (takes ~5 minutes)..."
az aks create \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_CLUSTER" \
  --node-count "$AKS_NODE_COUNT" \
  --node-vm-size "$AKS_NODE_VM" \
  --attach-acr "$ACR_NAME" \
  --enable-managed-identity \
  --enable-oidc-issuer \
  --enable-workload-identity \
  --network-plugin azure \
  --generate-ssh-keys \
  --tier free

echo "Getting AKS credentials..."
az aks get-credentials \
  --resource-group "$RESOURCE_GROUP" \
  --name "$AKS_CLUSTER" \
  --overwrite-existing

echo "Creating Azure OpenAI resource..."
az cognitiveservices account create \
  --name "$OPENAI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --kind OpenAI \
  --sku S0 \
  --yes

echo "Deploying GPT-4o model..."
az cognitiveservices account deployment create \
  --name "$OPENAI_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --deployment-name "$OPENAI_DEPLOYMENT" \
  --model-name "gpt-4o" \
  --model-version "2024-11-20" \
  --model-format OpenAI \
  --sku-capacity "$OPENAI_CAPACITY" \
  --sku-name "Standard"

OPENAI_ENDPOINT=$(az cognitiveservices account show \
  --name "$OPENAI_NAME" --resource-group "$RESOURCE_GROUP" \
  --query properties.endpoint -o tsv)

OPENAI_KEY=$(az cognitiveservices account keys list \
  --name "$OPENAI_NAME" --resource-group "$RESOURCE_GROUP" \
  --query key1 -o tsv)

echo "Creating Azure AI Search..."
az search service create \
  --name "$SEARCH_NAME" \
  --resource-group "$RESOURCE_GROUP" \
  --location "$LOCATION" \
  --sku "$SEARCH_SKU"

SEARCH_ENDPOINT="https://${SEARCH_NAME}.search.windows.net"
SEARCH_KEY=$(az search admin-key show \
  --service-name "$SEARCH_NAME" --resource-group "$RESOURCE_GROUP" \
  --query primaryKey -o tsv)

echo "Creating destinations search index..."
curl -s -X PUT \
  "${SEARCH_ENDPOINT}/indexes/${SEARCH_INDEX}?api-version=2024-07-01" \
  -H "Content-Type: application/json" \
  -H "api-key: ${SEARCH_KEY}" \
  -d '{
    "name": "destinations",
    "fields": [
      {"name": "destinationId", "type": "Edm.String", "key": true, "searchable": false},
      {"name": "name",          "type": "Edm.String", "searchable": true, "filterable": true},
      {"name": "country",       "type": "Edm.String", "searchable": true, "filterable": true},
      {"name": "description",   "type": "Edm.String", "searchable": true, "analyzer": "en.microsoft"},
      {"name": "tags",          "type": "Collection(Edm.String)", "searchable": true, "filterable": true},
      {"name": "averageWeeklyPriceGbp", "type": "Edm.Double", "filterable": true, "sortable": true},
      {"name": "connectedAirports", "type": "Collection(Edm.String)", "filterable": true},
      {"name": "contentVector", "type": "Collection(Edm.Single)", "searchable": true,
        "dimensions": 1536, "vectorSearchProfile": "vector-profile"}
    ],
    "vectorSearch": {
      "profiles": [{"name": "vector-profile", "algorithm": "hnsw-config"}],
      "algorithms": [{"name": "hnsw-config", "kind": "hnsw"}]
    },
    "semantic": {
      "configurations": [{
        "name": "travel-semantic-config",
        "prioritizedFields": {
          "contentFields": [{"fieldName": "description"}],
          "keywordsFields": [{"fieldName": "tags"}, {"fieldName": "name"}]
        }
      }]
    }
  }'

echo "Installing nginx ingress controller..."
helm repo add ingress-nginx https://kubernetes.github.io/ingress-nginx
helm repo update
helm upgrade --install ingress-nginx ingress-nginx/ingress-nginx \
  --namespace ingress-nginx --create-namespace \
  --set controller.service.annotations."service\.beta\.kubernetes\.io/azure-load-balancer-health-probe-request-path"=/healthz

echo "Waiting for ingress controller..."
kubectl wait --namespace ingress-nginx \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/component=controller \
  --timeout=120s

INGRESS_IP=$(kubectl get service ingress-nginx-controller \
  --namespace ingress-nginx \
  --output jsonpath='{.status.loadBalancer.ingress[0].ip}')

echo "Installing cert-manager..."
helm repo add jetstack https://charts.jetstack.io
helm repo update
helm upgrade --install cert-manager jetstack/cert-manager \
  --namespace cert-manager --create-namespace \
  --set installCRDs=true \
  --version v1.14.0

kubectl wait --namespace cert-manager \
  --for=condition=ready pod \
  --selector=app.kubernetes.io/instance=cert-manager \
  --timeout=120s

echo ""
echo "════════════════════════════════════════════════════════════════"
echo "  ALL DONE — copy these values into GitHub Secrets"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "  Resource Group : $RESOURCE_GROUP"
echo "  AKS Cluster    : $AKS_CLUSTER"
echo "  ACR            : $ACR_LOGIN_SERVER"
echo "  Ingress IP     : $INGRESS_IP"
echo ""
echo "  AZURE_OPENAI_ENDPOINT  = $OPENAI_ENDPOINT"
echo "  AZURE_OPENAI_KEY       = $OPENAI_KEY"
echo "  AZURE_SEARCH_ENDPOINT  = $SEARCH_ENDPOINT"
echo "  AZURE_SEARCH_KEY       = $SEARCH_KEY"
echo ""
echo "  Next: Point DNS A record -> $INGRESS_IP"
echo "  Then update deploy/k8s/05-ingress.yaml and 07-clusterissuer.yaml"
echo ""
