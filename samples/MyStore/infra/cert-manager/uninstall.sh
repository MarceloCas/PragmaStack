#!/bin/bash

set -e

NAMESPACE="cert-manager"
RELEASE_NAME="cert-manager"

echo "🗑️  Uninstalling cert-manager from MyStore cluster..."

if ! command -v helm &> /dev/null; then
    echo "❌ Helm not found."
    exit 1
fi

if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl not found."
    exit 1
fi

# Check if installed
if ! helm list -n "$NAMESPACE" | grep -q "$RELEASE_NAME"; then
    echo "⚠️  cert-manager is not installed."
    exit 0
fi

echo "⚠️  This will remove cert-manager and all its resources."
read -p "Are you sure you want to continue? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborting."
    exit 0
fi

# Delete all Certificate resources first
echo "🧹 Deleting all Certificate resources..."
kubectl delete certificates --all -A --ignore-not-found=true

# Delete all CertificateRequest resources
echo "🧹 Deleting all CertificateRequest resources..."
kubectl delete certificaterequests --all -A --ignore-not-found=true

# Delete all ClusterIssuer resources
echo "🧹 Deleting all ClusterIssuer resources..."
kubectl delete clusterissuers --all --ignore-not-found=true

# Delete all Issuer resources
echo "🧹 Deleting all Issuer resources..."
kubectl delete issuers --all -A --ignore-not-found=true

# Uninstall Helm release
echo "📦 Uninstalling Helm release..."
helm uninstall "$RELEASE_NAME" -n "$NAMESPACE" --wait

# Delete namespace
echo "📁 Deleting namespace: $NAMESPACE"
kubectl delete namespace "$NAMESPACE" --ignore-not-found=true --timeout=60s

# Optionally delete CRDs (commented out by default for safety)
# echo "🧹 Deleting cert-manager CRDs..."
# kubectl delete crd certificaterequests.cert-manager.io
# kubectl delete crd certificates.cert-manager.io
# kubectl delete crd challenges.acme.cert-manager.io
# kubectl delete crd clusterissuers.cert-manager.io
# kubectl delete crd issuers.cert-manager.io
# kubectl delete crd orders.acme.cert-manager.io

echo ""
echo "✅ cert-manager uninstalled successfully!"
echo ""
echo "📝 Note: CRDs were NOT deleted for safety."
echo "   If you want to remove CRDs manually:"
echo "   kubectl get crd | grep cert-manager | awk '{print \$1}' | xargs kubectl delete crd"
echo ""
