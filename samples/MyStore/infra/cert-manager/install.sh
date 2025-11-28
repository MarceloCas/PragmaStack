#!/bin/bash

set -e

SCRIPT_DIR="$(cd "$(dirname "${BASH_SOURCE[0]}")" && pwd)"
VALUES_FILE="$SCRIPT_DIR/values.yaml"
NAMESPACE="cert-manager"
RELEASE_NAME="cert-manager"
CHART_VERSION="v1.16.2"

echo "🔐 Installing cert-manager on MyStore cluster..."

# Check prerequisites
if ! command -v helm &> /dev/null; then
    echo "❌ Helm not found. Please install Helm first:"
    echo "   https://helm.sh/docs/intro/install/"
    exit 1
fi

if ! command -v kubectl &> /dev/null; then
    echo "❌ kubectl not found. Please install kubectl first:"
    echo "   https://kubernetes.io/docs/tasks/tools/"
    exit 1
fi

# Verify cluster is running
if ! kubectl cluster-info &> /dev/null; then
    echo "❌ Kubernetes cluster is not accessible."
    echo "   Make sure your k3d cluster is running:"
    echo "   cd ../k3d && ./create-cluster.sh"
    exit 1
fi

# Check if already installed
if helm list -n "$NAMESPACE" | grep -q "$RELEASE_NAME"; then
    echo "⚠️  cert-manager is already installed."
    read -p "Do you want to upgrade it? (y/N): " -n 1 -r
    echo
    if [[ $REPLY =~ ^[Yy]$ ]]; then
        echo "🔄 Upgrading cert-manager..."

        # Add/update Helm repository
        echo "📦 Adding jetstack Helm repository..."
        helm repo add jetstack https://charts.jetstack.io --force-update
        helm repo update

        # Upgrade release
        helm upgrade "$RELEASE_NAME" jetstack/cert-manager \
            --namespace "$NAMESPACE" \
            --version "$CHART_VERSION" \
            --values "$VALUES_FILE" \
            --wait \
            --timeout 5m

        echo "✅ cert-manager upgraded successfully!"
    else
        echo "Aborting."
        exit 0
    fi
else
    # Fresh installation
    echo "📝 Installing cert-manager from Helm chart..."

    # Create namespace
    echo "📁 Creating namespace: $NAMESPACE"
    kubectl create namespace "$NAMESPACE" --dry-run=client -o yaml | kubectl apply -f -

    # Add Helm repository
    echo "📦 Adding jetstack Helm repository..."
    helm repo add jetstack https://charts.jetstack.io --force-update
    helm repo update

    # Install cert-manager
    echo "🚀 Installing cert-manager Helm chart (version $CHART_VERSION)..."
    helm install "$RELEASE_NAME" jetstack/cert-manager \
        --namespace "$NAMESPACE" \
        --version "$CHART_VERSION" \
        --values "$VALUES_FILE" \
        --wait \
        --timeout 5m

    echo "✅ cert-manager installed successfully!"
fi

echo ""
echo "⏳ Waiting for cert-manager to be ready..."
kubectl wait --for=condition=ready pod \
    -l app.kubernetes.io/instance=cert-manager \
    -n "$NAMESPACE" \
    --timeout=300s

echo ""
echo "📊 cert-manager Status:"
echo "===================="
helm list -n "$NAMESPACE"
echo ""
echo "🔧 Pods:"
kubectl get pods -n "$NAMESPACE"
echo ""
echo "📦 CRDs Installed:"
kubectl get crd | grep cert-manager
echo ""
echo "💡 cert-manager Info:"
echo "   - Version: $CHART_VERSION"
echo "   - Namespace: $NAMESPACE"
echo "   - Prometheus metrics: enabled on port 9402"
echo ""
echo "════════════════════════════════════════════════════════════════"
echo "🔒 HTTPS Setup - Choose Your Certificate Strategy"
echo "════════════════════════════════════════════════════════════════"
echo ""

# Check if mkcert is installed
if command -v mkcert &> /dev/null; then
    echo "✅ mkcert is installed: $(mkcert -version)"
    echo ""
    echo "🎯 RECOMMENDED: Setup mkcert for trusted HTTPS (green padlock 🔒)"
    echo ""
    read -p "Do you want to setup mkcert now? (Y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        echo ""
        echo "🚀 Setting up mkcert..."
        bash "$SCRIPT_DIR/setup-mkcert.sh"
        echo ""
        echo "✅ mkcert setup completed!"
        echo ""
        echo "🎯 Next steps:"
        echo "   1. Test with example certificate:"
        echo "      kubectl apply -f example-certificate-mkcert.yaml"
        echo ""
        echo "   2. Add to /etc/hosts (Linux/Mac) or C:\\Windows\\System32\\drivers\\etc\\hosts (Windows):"
        echo "      127.0.0.1 example.mystore.local"
        echo ""
        echo "   3. Open in browser: https://example.mystore.local"
        echo "      You should see a GREEN PADLOCK 🔒"
        echo ""
    else
        echo ""
        echo "⚡ Using self-signed certificates instead"
        echo ""
        echo "🎯 Next steps:"
        echo "   1. Create self-signed ClusterIssuer:"
        echo "      kubectl apply -f clusterissuer-selfsigned.yaml"
        echo ""
        echo "   2. Test with example certificate:"
        echo "      kubectl apply -f example-certificate.yaml"
        echo ""
        echo "   3. View certificates:"
        echo "      kubectl get certificates -A"
        echo ""
        echo "   ⚠️  Note: Browsers will show 'Not Secure' with self-signed certs"
        echo "   💡 Install mkcert later for trusted HTTPS: ./setup-mkcert.sh"
        echo ""
    fi
else
    echo "⚠️  mkcert is NOT installed"
    echo ""
    echo "🔒 For TRUSTED HTTPS (green padlock), install mkcert:"
    echo ""
    echo "   Windows (PowerShell as Admin):"
    echo "      # Download direto"
    echo "      Invoke-WebRequest -Uri https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-windows-amd64.exe -OutFile mkcert.exe"
    echo "      Move-Item mkcert.exe C:\\Windows\\System32\\mkcert.exe -Force"
    echo ""
    echo "      # OU com winget"
    echo "      winget install FiloSottile.mkcert"
    echo ""
    echo "      # OU com scoop"
    echo "      scoop install mkcert"
    echo ""
    echo "   macOS (Homebrew):"
    echo "      brew install mkcert"
    echo ""
    echo "   Linux:"
    echo "      wget https://github.com/FiloSottile/mkcert/releases/download/v1.4.4/mkcert-v1.4.4-linux-amd64"
    echo "      chmod +x mkcert-v1.4.4-linux-amd64"
    echo "      sudo mv mkcert-v1.4.4-linux-amd64 /usr/local/bin/mkcert"
    echo ""
    echo "   After installing mkcert, run: ./setup-mkcert.sh"
    echo ""
    echo "════════════════════════════════════════════════════════════════"
    echo ""
    read -p "Use self-signed certificates instead? (Y/n): " -n 1 -r
    echo
    if [[ ! $REPLY =~ ^[Nn]$ ]]; then
        echo ""
        echo "📝 Creating self-signed ClusterIssuer..."
        kubectl apply -f "$SCRIPT_DIR/clusterissuer-selfsigned.yaml"
        echo ""
        echo "✅ Self-signed ClusterIssuer created!"
        echo ""
        echo "🎯 Next steps:"
        echo "   1. Test with example certificate:"
        echo "      kubectl apply -f example-certificate.yaml"
        echo ""
        echo "   2. View certificates:"
        echo "      kubectl get certificates -A"
        echo ""
        echo "   ⚠️  Note: Browsers will show 'Not Secure' with self-signed certs"
        echo "   💡 For E2E tests, consider installing mkcert later"
        echo ""
    else
        echo ""
        echo "Skipped ClusterIssuer creation."
        echo "You can create it later with:"
        echo "   kubectl apply -f clusterissuer-selfsigned.yaml"
        echo ""
    fi
fi

echo "════════════════════════════════════════════════════════════════"
echo "✅ cert-manager installation completed!"
echo "════════════════════════════════════════════════════════════════"
echo ""
echo "📚 Useful commands:"
echo "   - View ClusterIssuers: kubectl get clusterissuers"
echo "   - View Certificates: kubectl get certificates -A"
echo "   - View cert-manager logs: kubectl logs -n $NAMESPACE -l app.kubernetes.io/name=cert-manager"
echo "   - Describe ClusterIssuer: kubectl describe clusterissuer <name>"
echo ""
