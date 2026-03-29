# TravelAI.Core — AKS Deployment Guide

Step-by-step from zero to a live HTTPS API on Azure Kubernetes Service.

---

## Step 1 — Install tools

```bash
# Azure CLI
brew install azure-cli          # macOS
# OR: curl -sL https://aka.ms/InstallAzureCLIDeb | sudo bash   (Ubuntu/WSL)
# OR: winget install Microsoft.AzureCLI                         (Windows)

# kubectl + helm
brew install kubectl helm
# OR: az aks install-cli        (any OS, installs kubectl via Azure CLI)

# Verify all tools
az --version && kubectl version --client && helm version && dotnet --version && docker --version
```

---

## Step 2 — Azure login

```bash
az login
az account list --output table
az account set --subscription "YOUR_SUBSCRIPTION_NAME"
az account show --output table
```

---

## Step 3 — Configure the provisioning script

Open `deploy/scripts/provision-azure.sh` and edit the top section:

```bash
SUBSCRIPTION_ID="YOUR_SUBSCRIPTION_ID"    # az account show --query id -o tsv
ACR_NAME="acrtravelaiyourinitials"         # globally unique, e.g. acrtravelaijsmith
```

---

## Step 4 — Provision all Azure resources

```bash
chmod +x deploy/scripts/provision-azure.sh
bash deploy/scripts/provision-azure.sh
```

Takes ~15 minutes. Creates: resource group, ACR, AKS cluster, Azure OpenAI (GPT-4o),
Azure AI Search + index, nginx ingress controller, cert-manager.

Save the keys and IP printed at the end — you need them in Step 6.

---

## Step 5 — Push to GitHub

```bash
git init
git add .
git commit -m "feat: initial TravelAI.Core implementation"
git remote add origin https://github.com/YOUR_USERNAME/TravelAI.Core.git
git branch -M main
git push -u origin main
```

Then in GitHub: Settings → Environments → New environment → name it `production`
→ enable Required reviewers → add yourself.

---

## Step 6 — Add GitHub Secrets

Repo → Settings → Secrets and variables → Actions → New repository secret

| Secret name | Value |
|---|---|
| `AZURE_CREDENTIALS` | JSON from `create-service-principal.sh` (run below) |
| `AZURE_OPENAI_ENDPOINT` | Printed by provision-azure.sh |
| `AZURE_OPENAI_KEY` | Printed by provision-azure.sh |
| `AZURE_SEARCH_ENDPOINT` | Printed by provision-azure.sh |
| `AZURE_SEARCH_KEY` | Printed by provision-azure.sh |

```bash
chmod +x deploy/scripts/create-service-principal.sh
bash deploy/scripts/create-service-principal.sh
# Copy the entire JSON output → paste as AZURE_CREDENTIALS secret
```

---

## Step 7 — Configure domain (optional)

Add a DNS A record pointing to the ingress IP from Step 4, then update:
- `deploy/k8s/05-ingress.yaml` — replace `api.travelai.yourdomain.com`
- `deploy/k8s/07-clusterissuer.yaml` — replace `your-email@yourdomain.com`

---

## Step 8 — Test Docker build locally

```bash
chmod +x deploy/scripts/local-docker-test.sh
bash deploy/scripts/local-docker-test.sh

# In another terminal:
curl http://localhost:8080/health/live
```

---

## Step 9 — Deploy

```bash
git add deploy/
git commit -m "feat: add AKS deployment configuration"
git push origin main
```

In GitHub Actions: watch the pipeline → approve the production deployment → wait for rollout.

---

## Step 10 — Verify

```bash
kubectl get pods --namespace travelai
kubectl get ingress --namespace travelai

curl https://api.travelai.yourdomain.com/health/live
curl "https://api.travelai.yourdomain.com/api/destinations/search?q=warm+beach&maxResults=5"
```

---

## Useful commands

```bash
# Stream logs
kubectl logs --namespace travelai --selector app=travelai-api --follow

# Roll back
kubectl rollout undo deployment/travelai-api --namespace travelai

# Scale manually
kubectl scale deployment travelai-api --namespace travelai --replicas=4

# Rotate secrets
kubectl create secret generic travelai-secrets \
  --namespace travelai \
  --from-literal=azure-openai-key="NEW_KEY" \
  --from-literal=azure-openai-endpoint="https://..." \
  --from-literal=azure-search-endpoint="https://..." \
  --from-literal=azure-search-key="NEW_KEY" \
  --dry-run=client -o yaml | kubectl apply -f -
kubectl rollout restart deployment/travelai-api --namespace travelai

# Tear down (deletes everything)
az group delete --name rg-travelai-prod --yes --no-wait
```
