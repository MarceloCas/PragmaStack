#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
CONFIG_FILE="$SCRIPT_DIR/k3d-config.yaml"

echo "🚀 Creating MyStore k3d cluster..."

if ! command -v k3d &> /dev/null; then
    echo "❌ k3d not found. Please install k3d first:"
    echo "   https://k3d.io/#installation"
    exit 1
fi

if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl not found. Please install kubectl first:"
    echo "   https://kubernetes.io/docs/tasks/tools/"
    exit 1
fi

if k3d cluster list | grep -q "mystore-cluster"; then
    echo "⚠️  Cluster 'mystore-cluster' already exists."
    read -p "Do you want to delete and recreate it? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "🗑️  Deleting existing cluster..."
        k3d cluster delete mystore-cluster
    else
        echo "Aborting."
        exit 0
    fi
fi

echo "📝 Creating cluster from config: $CONFIG_FILE"
k3d cluster create --config "$CONFIG_FILE"

echo "⏳ Waiting for cluster to be ready..."
kubectl wait --for=condition=ready node --all --timeout=300s

echo "✅ Cluster created successfully!"
echo ""
echo "📊 Cluster Info:"
k3d cluster list
echo ""
echo "🔧 Nodes:"
kubectl get nodes
echo ""
echo "📦 Default namespaces:"
kubectl get ns
echo ""
echo "💡 Tips:"
echo "   - Registry available at: localhost:5000"
echo "   - HTTP port: 80"
echo "   - HTTPS port: 443"
echo "   - Additional port: 8080"
echo ""
echo "🎯 Next steps:"
echo "   1. Install cert-manager: cd ../cert-manager && ./install.sh"
echo "   2. Install Harbor: cd ../harbor && ./install.sh"
echo "   3. Check cluster: kubectl cluster-info"
