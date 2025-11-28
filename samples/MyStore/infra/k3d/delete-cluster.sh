#!/bin/bash

set -e

CLUSTER_NAME="mystore-cluster"

echo "🗑️  Deleting MyStore k3d cluster..."

if ! command -v k3d &> /dev/null; then
    echo "❌ k3d not found."
    exit 1
fi

if ! k3d cluster list | grep -q "$CLUSTER_NAME"; then
    echo "⚠️  Cluster '$CLUSTER_NAME' not found."
    k3d cluster list
    exit 0
fi

read -p "Are you sure you want to delete the cluster '$CLUSTER_NAME'? (y/N): " -n 1 -r
echo
if [[ ! $REPLY =~ ^[Yy]$ ]]; then
    echo "Aborting."
    exit 0
fi

echo "🔥 Deleting cluster..."
k3d cluster delete "$CLUSTER_NAME"

echo "✅ Cluster deleted successfully!"
echo ""
echo "📊 Remaining clusters:"
k3d cluster list
